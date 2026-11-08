using M = System.Math;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;

public enum EtsAggregation { Average = 1, Count = 2, CountA = 3, Max = 4, Median = 5, Min = 6, Sum = 7 }

public sealed record EtsRequest(
    IReadOnlyList<double> Timeline,
    IReadOnlyList<double> Values,
    int SamplesInPeriod,
    bool DataCompletion,
    EtsAggregation Aggregation,
    bool Additive);

public interface IEtsForecaster
{
    EtsOutcome Prepare(EtsRequest request, double? target, bool targetAfterEnd);
}

public sealed class EtsForecaster(IDateSystem dates) : IEtsForecaster
{
    public EtsOutcome Prepare(EtsRequest request, double? target, bool targetAfterEnd)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Timeline.Count != request.Values.Count) return EtsOutcome.Failed(Values.CellError.Value);
        var range = request.Timeline.Zip(request.Values).Select(pair => (pair.First, pair.Second)).OrderBy(point => point.First).ToList();
        if (range.Count < 2) return EtsOutcome.Failed(Values.CellError.NotAvailable);
        if (target is { } wanted && (targetAfterEnd ? wanted < range[^1].First : wanted < range[0].First))
            return EtsOutcome.Failed(Values.CellError.Number);

        var calculation = new HoltWintersForecast(range, request.Additive, request.SamplesInPeriod == 0, dates);
        var error = calculation.Preprocess(request);
        return error is { } failure ? EtsOutcome.Failed(failure) : new EtsOutcome(calculation, Values.CellError.None);
    }
}

public sealed class HoltWintersForecast
{
    private const double MinimumResolution = 0.001;
    private const int Scenarios = 1000;
    private const int MaximumScenarioSteps = 10_000;
    private const double MaximumForecastSteps = 1e15;

    private readonly List<(double X, double Y)> _range;
    private readonly bool _additive;
    private readonly IDateSystem _dates;
    private double[] _base = [];
    private double[] _trend = [];
    private double[] _periodic = [];
    private double[] _forecast = [];
    private int _count;
    private int _monthDay;
    private bool _initialised;
    private bool _seasonless;

    internal HoltWintersForecast(List<(double X, double Y)> range, bool additive, bool seasonless, IDateSystem dates)
    {
        _seasonless = seasonless;
        _range = range;
        _count = range.Count;
        _additive = additive;
        _dates = dates;
    }

    public int SamplesInPeriod { get; private set; }

    public double StepSize { get; private set; }

    public double Alpha { get; private set; }

    public double Beta { get; private set; }

    public double Gamma { get; private set; }

    public double MeanAbsoluteError { get; private set; }

    public double MeanAbsoluteScaledError { get; private set; }

    public double RootMeanSquaredError { get; private set; }

    public double SymmetricMeanAbsolutePercentageError { get; private set; }

    private double MeanSquaredError { get; set; }

    internal Values.CellError? Preprocess(EtsRequest request)
    {
        DetectMonths();
        StepSize = double.MaxValue;
        for (var i = 1; i < _count; i++)
        {
            var step = _range[i].X - _range[i - 1].X;
            if (step == 0)
            {
                Aggregate(i, request.Aggregation);
                step = i < _count - 1 ? _range[i].X - _range[i - 1].X : StepSize;
            }
            if (step > 0 && step < StepSize) StepSize = step;
        }
        if (_count < 2 || StepSize == double.MaxValue) return Values.CellError.NotAvailable;

        var gap = false;
        for (var i = 1; i < _count && !gap; i++)
        {
            var step = _range[i].X - _range[i - 1].X;
            if (step == StepSize) continue;
            if (step % StepSize != 0) return Values.CellError.Value;
            gap = true;
        }
        if (gap && !FillGaps(request.DataCompletion)) return Values.CellError.Value;

        if (request.SamplesInPeriod != 1) SamplesInPeriod = request.SamplesInPeriod;
        else
        {
            SamplesInPeriod = PeriodLength();
            if (SamplesInPeriod == 1) _seasonless = true;
        }

        _base = new double[_count];
        _trend = new double[_count];
        _periodic = _seasonless ? [] : new double[_count];
        _forecast = new double[_count];
        _forecast[0] = _range[0].Y;

        if (_seasonless) _trend[0] = (_range[_count - 1].Y - _range[0].Y) / (_count - 1);
        else
        {
            if (_count < 2 * SamplesInPeriod) return Values.CellError.Value;
            var sum = 0.0;
            for (var i = 0; i < SamplesInPeriod; i++) sum += _range[i + SamplesInPeriod].Y - _range[i].Y;
            _trend[0] = sum / ((double)SamplesInPeriod * SamplesInPeriod);
            if (PrefillPeriodic() is { } periodicError) return periodicError;
        }
        _base[0] = _seasonless ? _range[0].Y : _range[0].Y / _periodic[0];
        return null;
    }

    private void DetectMonths()
    {
        var first = _dates.FromSerial(M.Truncate(_range[0].X));
        _monthDay = first.Day;
        for (var i = 1; i < _count && _monthDay != 0; i++)
        {
            var other = _dates.FromSerial(M.Truncate(_range[i].X));
            if (other != first && other.Day != _monthDay) _monthDay = 0;
        }
        if (_monthDay == 0) return;
        for (var i = 0; i < _count; i++)
        {
            var date = _dates.FromSerial(M.Truncate(_range[i].X));
            _range[i] = (date.Year * 12 + date.Month, _range[i].Y);
        }
    }

    private void Aggregate(int index, EtsAggregation aggregation)
    {
        var x = _range[index - 1].X;
        var group = new List<double> { _range[index - 1].Y };
        while (index < _count && _range[index].X == x)
        {
            group.Add(_range[index].Y);
            _range.RemoveAt(index);
            _count--;
        }
        var value = aggregation switch
        {
            EtsAggregation.Sum => group.Sum(),
            EtsAggregation.Count or EtsAggregation.CountA => group.Count,
            EtsAggregation.Max => group.Max(),
            EtsAggregation.Min => group.Min(),
            EtsAggregation.Median => Median(group),
            _ => group[0],
        };
        _range[index - 1] = (x, value);
    }

    private static double Median(List<double> values)
    {
        values.Sort();
        var middle = values.Count / 2;
        return values.Count % 2 == 1 ? values[middle] : (values[middle] + values[middle - 1]) / 2;
    }

    private bool FillGaps(bool completion)
    {
        var missing = 0;
        var original = (double)_count;
        for (var i = 1; i < _count; i++)
        {
            if (_range[i].X - _range[i - 1].X <= StepSize) continue;
            var fill = (_range[i].Y + _range[i - 1].Y) / 2;
            for (var x = _range[i - 1].X + StepSize; x < _range[i].X; x += StepSize)
            {
                _range.Insert(i, (x, completion ? fill : 0));
                i++;
                _count++;
                missing++;
                if (missing / original > 0.3) return false;
            }
        }
        return true;
    }

    private Values.CellError? PrefillPeriodic()
    {
        var periods = _count / SamplesInPeriod;
        var averages = new double[periods];
        for (var i = 0; i < periods; i++)
        {
            for (var j = 0; j < SamplesInPeriod; j++) averages[i] += _range[i * SamplesInPeriod + j].Y;
            averages[i] /= SamplesInPeriod;
            if (averages[i] == 0) return Values.CellError.DivideByZero;
        }
        for (var j = 0; j < SamplesInPeriod; j++)
        {
            var sum = 0.0;
            for (var i = 0; i < periods; i++)
            {
                var expected = averages[i] + (j - 0.5 * (SamplesInPeriod - 1)) * _trend[0];
                var actual = _range[i * SamplesInPeriod + j].Y;
                sum += _additive ? actual - expected : actual / expected;
            }
            _periodic[j] = sum / periods;
        }
        if (SamplesInPeriod < _count) _periodic[SamplesInPeriod] = 0;
        return null;
    }

    private int PeriodLength()
    {
        var best = _count;
        var bestError = double.MaxValue;
        for (var length = _count / 2; length >= 1; length--)
        {
            var sum = 0.0;
            var periods = _count / length;
            var start = _count - periods * length + 1;
            for (var i = start; i < _count - length; i++)
                sum += M.Abs(_range[i].Y - _range[i - 1].Y - (_range[length + i].Y - _range[length + i - 1].Y));
            var mean = sum / ((periods - 1) * length - 1);
            if (mean <= bestError || mean == 0)
            {
                best = length;
                bestError = mean;
            }
        }
        return best;
    }

    private void Initialise()
    {
        if (_initialised) return;
        Optimise(value => Alpha = value, _seasonless ? OptimiseGamma : OptimiseBetaGamma);
        Accuracy();
        _initialised = true;
    }

    private void OptimiseBetaGamma() => Optimise(value => Beta = value, OptimiseGamma);

    private void OptimiseGamma() => Optimise(value => Gamma = value, () => { });

    private void Optimise(Action<double> set, Action inner)
    {
        void Try(double value)
        {
            set(value);
            inner();
            Refill();
        }

        double low = 0, high = 1, middle = 0.5;
        Try(low);
        var lowError = MeanSquaredError;
        Try(high);
        var highError = MeanSquaredError;
        Try(middle);
        if (lowError == MeanSquaredError && MeanSquaredError == highError)
        {
            Try(0);
            return;
        }
        while (high - middle > MinimumResolution)
        {
            if (highError > lowError)
            {
                high = middle;
                highError = MeanSquaredError;
                middle = (low + middle) / 2;
            }
            else
            {
                low = middle;
                lowError = MeanSquaredError;
                middle = (middle + high) / 2;
            }
            Try(middle);
        }
        if (highError > lowError)
        {
            if (lowError < MeanSquaredError) Try(low);
        }
        else if (highError < MeanSquaredError) Try(high);
    }

    private void Refill()
    {
        for (var i = 1; i < _count; i++)
        {
            if (_seasonless)
            {
                _base[i] = Alpha * _range[i].Y + (1 - Alpha) * (_base[i - 1] + _trend[i - 1]);
                _trend[i] = Gamma * (_base[i] - _base[i - 1]) + (1 - Gamma) * _trend[i - 1];
                _forecast[i] = _base[i - 1] + _trend[i - 1];
                continue;
            }
            int index;
            if (_additive)
            {
                index = i > SamplesInPeriod ? i - SamplesInPeriod : i;
                _base[i] = Alpha * (_range[i].Y - _periodic[index]) + (1 - Alpha) * (_base[i - 1] + _trend[i - 1]);
                _periodic[i] = Beta * (_range[i].Y - _base[i]) + (1 - Beta) * _periodic[index];
            }
            else
            {
                index = i >= SamplesInPeriod ? i - SamplesInPeriod : i;
                _base[i] = Alpha * (_range[i].Y / _periodic[index]) + (1 - Alpha) * (_base[i - 1] + _trend[i - 1]);
                _periodic[i] = Beta * (_range[i].Y / _base[i]) + (1 - Beta) * _periodic[index];
            }
            _trend[i] = Gamma * (_base[i] - _base[i - 1]) + (1 - Gamma) * _trend[i - 1];
            _forecast[i] = _additive
                ? _base[i - 1] + _trend[i - 1] + _periodic[index]
                : (_base[i - 1] + _trend[i - 1]) * _periodic[index];
        }
        Accuracy();
    }

    private void Accuracy()
    {
        double absolute = 0, squared = 0, percent = 0, divisor = 0;
        for (var i = 1; i < _count; i++)
        {
            var error = _forecast[i] - _range[i].Y;
            absolute += M.Abs(error);
            squared += error * error;
            percent += M.Abs(error) / (M.Abs(_forecast[i]) + M.Abs(_range[i].Y));
        }
        for (var i = 2; i < _count; i++) divisor += M.Abs(_range[i].Y - _range[i - 1].Y);
        var calculated = _count - 1;
        MeanAbsoluteError = absolute / calculated;
        var scale = calculated * divisor / (calculated - 1);
        MeanAbsoluteScaledError = scale == 0 ? 0 : absolute / scale;
        MeanSquaredError = squared / calculated;
        RootMeanSquaredError = M.Sqrt(MeanSquaredError);
        SymmetricMeanAbsolutePercentageError = percent * 2 / calculated;
    }

    public double Forecast(double target)
    {
        Initialise();
        target = Months(target);
        if (target <= _range[_count - 1].X)
        {
            var position = (int)((target - _range[0].X) / StepSize);
            var fraction = (target - _range[0].X) % StepSize;
            var value = _range[position].Y;
            return fraction >= MinimumResolution ? value + fraction / StepSize * (_forecast[position + 1] - value) : value;
        }
        var distance = (target - _range[_count - 1].X) / StepSize;
        if (!(distance < MaximumForecastSteps)) return double.NaN;
        var steps = (long)distance;
        var rest = (target - _range[_count - 1].X) % StepSize;
        var forecast = Ahead(steps);
        return rest >= MinimumResolution ? forecast + rest / StepSize * (Ahead(steps + 1) - forecast) : forecast;
    }

    private double Ahead(long steps)
    {
        var level = _base[_count - 1] + steps * _trend[_count - 1];
        if (_seasonless) return level;
        var seasonal = _periodic[_count - 1 - SamplesInPeriod + (int)(steps % SamplesInPeriod)];
        return _additive ? level + seasonal : level * seasonal;
    }

    private double Months(double x)
    {
        if (_monthDay == 0) return x;
        var date = _dates.FromSerial(M.Truncate(x));
        var length = DateTime.DaysInMonth(date.Year, date.Month);
        return 12.0 * date.Year + date.Month + (date.Day - _monthDay) / (double)length;
    }

    public double? Statistic(int kind)
    {
        Initialise();
        return kind switch
        {
            1 => Alpha,
            2 => Gamma,
            3 => Beta,
            4 => MeanAbsoluteScaledError,
            5 => SymmetricMeanAbsolutePercentageError,
            6 => MeanAbsoluteError,
            7 => RootMeanSquaredError,
            8 => StepSize,
            9 => SamplesInPeriod,
            _ => null,
        };
    }

    public double? PredictionInterval(double target, double level, bool seasonal)
    {
        Initialise();
        var distance = Months(target) - _range[_count - 1].X;
        if (!(distance / StepSize < int.MaxValue - 1)) return null;
        var size = (int)(distance / StepSize);
        if (distance % StepSize != 0) size++;
        if (size <= 0 || (seasonal && size > MaximumScenarioSteps)) return null;

        var stepIndex = (int)(distance / StepSize) - 1;
        if (stepIndex < 0) return null;
        var factor = distance % StepSize;
        var interpolated = factor != 0 && stepIndex + 1 < size;
        var (width, next) = seasonal
            ? ScenarioWidths(size, stepIndex, level)
            : (AnalyticWidth(stepIndex, level), interpolated ? AnalyticWidth(stepIndex + 1, level) : 0);
        return interpolated ? width + factor * (next - width) : width;
    }

    private double AnalyticWidth(int step, double level)
    {
        var z = SpreadsheetMath.StandardNormalInverse((1 + level) / 2);
        var o = 1 - level;
        double Coefficient(double i) =>
            M.Sqrt(1 + level / M.Pow(1 + o, 3) * (1 + 4 * o + 5 * o * o + 2.0 * i * level * (1 + 3 * o) + 2.0 * i * i * level * level));
        return z * RootMeanSquaredError * Coefficient(step) / Coefficient(0);
    }

    private (double Width, double Next) ScenarioWidths(int size, int stepIndex, double level)
    {
        var random = new Random(SamplesInPeriod * 7919 + _count);
        var atStep = new double[Scenarios];
        var atNext = new double[Scenarios];
        var range = new double[size];
        var levelPath = new double[size];
        var trendPath = new double[size];
        var periodicPath = new double[size];
        double Deviation() => RootMeanSquaredError * SpreadsheetMath.StandardNormalInverse(0.5 + random.NextDouble() / 2);

        for (var k = 0; k < Scenarios; k++)
        {
            for (var i = 0; i < size; i++)
            {
                var previousLevel = i == 0 ? _base[_count - 1] : levelPath[i - 1];
                var previousTrend = i == 0 ? _trend[_count - 1] : trendPath[i - 1];
                var periodic = i < SamplesInPeriod ? _periodic[_count + i - SamplesInPeriod] : periodicPath[i - SamplesInPeriod];
                range[i] = (_additive ? previousLevel + previousTrend + periodic : (previousLevel + previousTrend) * periodic) + Deviation();
                if (i == stepIndex) atStep[k] = range[i];
                if (i == stepIndex + 1) atNext[k] = range[i];
                levelPath[i] = Alpha * (_additive ? range[i] - periodic : range[i] / periodic) + (1 - Alpha) * (previousLevel + previousTrend);
                trendPath[i] = Gamma * (levelPath[i] - previousLevel) + (1 - Gamma) * previousTrend;
                periodicPath[i] = Beta * (_additive ? range[i] - levelPath[i] : range[i] / levelPath[i]) + (1 - Beta) * periodic;
            }
        }

        double Width(double[] values) => Percentile(values, (1 + level) / 2) - Percentile(values, 0.5);
        return (Width(atStep), stepIndex + 1 < size ? Width(atNext) : 0);
    }

    private static double Percentile(double[] values, double fraction)
    {
        var sorted = values.OrderBy(value => value).ToArray();
        var position = fraction * (sorted.Length - 1);
        var lower = (int)M.Floor(position);
        return lower + 1 >= sorted.Length ? sorted[^1] : sorted[lower] + (position - lower) * (sorted[lower + 1] - sorted[lower]);
    }
}

public sealed record EtsOutcome(HoltWintersForecast? Calculation, Values.CellError Error)
{
    public static EtsOutcome Failed(Values.CellError error) => new(null, error);
}

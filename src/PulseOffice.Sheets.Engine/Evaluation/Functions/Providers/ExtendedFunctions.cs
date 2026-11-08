using System.Globalization;
using PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;
using PulseOffice.Sheets.Values;
using M = System.Math;
using F = PulseOffice.Sheets.Engine.Evaluation.Functions.FunctionBuilder;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class ExtendedFunctions(IDateSystem dates, IRangeMaterializer ranges, WorkdayCalculator workdays, IGuardedRegex patterns) : IFunctionProvider
{
    private readonly ThaiBahtText _baht = new();

    private readonly EuroCurrencies _currencies = new();

    public string Category => "Varie";

    private static readonly Dictionary<int, string> WeekendCodes = new()
    {
        [1] = "0000011",
        [2] = "1000001",
        [3] = "1100000",
        [4] = "0110000",
        [5] = "0011000",
        [6] = "0001100",
        [7] = "0000110",
        [11] = "0000001",
        [12] = "1000000",
        [13] = "0100000",
        [14] = "0010000",
        [15] = "0001000",
        [16] = "0000100",
        [17] = "0000010",
    };

    public IEnumerable<FunctionDefinition> GetFunctions() => new FunctionBuilder(Category)
        .Raw("NETWORKDAYS.INTL", 2, 4, r => Workdays(r, count: true))
        .Raw("WORKDAY.INTL", 2, 4, r => Workdays(r, count: false))

        .Raw("WEEKNUM_OOO", 2, 2, r =>
        {
            var date = dates.FromSerial(r.Number(0));
            var mode = r.Integer(1);
            if (r.HasError) return r.Error;

            if (mode == 2) return CellValue.Number(ISOWeek.GetWeekOfYear(date));

            return CellValue.Number(CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                date, CalendarWeekRule.FirstDay, DayOfWeek.Sunday));
        })

        .Raw("REGEX", 2, 4, r =>
        {
            var text = r.Text(0);
            var pattern = r.Text(1);
            var flags = r.Text(3, string.Empty);
            if (r.HasError) return r.Error;

            if (patterns.Compile(pattern, flags.Contains('i', StringComparison.OrdinalIgnoreCase)) is not { } regex) return F.Invalid();
            var replaceAll = flags.Contains('g', StringComparison.OrdinalIgnoreCase);

            if (!r.Has(2))
                return patterns.Matches(regex, text) switch
                {
                    null => F.Invalid(),
                    [] => CellValue.FromError(CellError.NotAvailable),
                    var found => CellValue.Text(found[0].Value),
                };

            var replacement = r.Text(2);
            return patterns.Replace(regex, text, replacement, (index, _) => replaceAll || index == 0) is { } replaced
                ? CellValue.Text(replaced)
                : F.Invalid();
        })

        .Raw("B", 3, 4, r =>
        {
            var trials = M.Floor(r.Number(0));
            var probability = r.Number(1);
            var start = M.Floor(r.Number(2));
            if (r.HasError) return r.Error;
            if (trials < 0 || probability is < 0 or > 1 || start < 0) return F.OutOfDomain();

            var end = r.Has(3) ? M.Floor(r.Number(3)) : start;
            if (end < start || end > trials) return F.OutOfDomain();

            return F.Number(BinomialDistribution.Range(start, end, trials, probability));
        })

        .ArrayResult("FOURIER", 2, 5, r =>
        {
            var grid = ranges.Materialize(r.Raw(0), r.Context);
            var groupedByColumns = r.Boolean(1, false);
            var inverse = r.Boolean(2, false);
            var polar = r.Boolean(3, false);
            var minimumMagnitude = r.Number(4, 0);
            if (r.HasError) return EvalValue.Scalar(r.Error);
            if (grid.IsEmpty || (groupedByColumns ? grid.ColumnCount : grid.RowCount) > 2) return EvalValue.Scalar(F.Invalid());

            var length = groupedByColumns ? grid.RowCount : grid.ColumnCount;
            var complexInput = (groupedByColumns ? grid.ColumnCount : grid.RowCount) == 2;
            var samples = new System.Numerics.Complex[length];
            for (var i = 0; i < length; i++)
            {
                var real = groupedByColumns ? grid[i, 0] : grid[0, i];
                var imaginary = complexInput ? (groupedByColumns ? grid[i, 1] : grid[1, i]) : CellValue.Number(0);
                if (real.IsError) return EvalValue.Scalar(real);
                if (imaginary.IsError) return EvalValue.Scalar(imaginary);
                if (real.Kind != ValueKind.Number || imaginary.Kind != ValueKind.Number) return EvalValue.Error(CellError.Value);
                samples[i] = new System.Numerics.Complex(real.AsNumber(), imaginary.AsNumber());
            }

            var transform = DiscreteFourier.TransformUnscaled(samples, inverse);
            var scale = inverse ? 1.0 / length : 1.0;
            return new ArrayValue([.. transform.Select(value =>
            {
                if (!polar) return (IReadOnlyList<CellValue>)[F.Number(value.Real * scale + 0.0), F.Number(value.Imaginary * scale + 0.0)];
                var magnitude = value.Magnitude;
                return magnitude < minimumMagnitude
                    ? [CellValue.Number(0), CellValue.Number(0)]
                    : [F.Number(magnitude * scale), F.Number(System.Math.Atan2(value.Imaginary, value.Real))];
            })]);
        })

        .Raw("EUROCONVERT", 3, 5, r =>
        {
            var value = r.Number(0);
            var from = r.Text(1);
            var to = r.Text(2);
            var fullPrecision = r.Boolean(3, false);
            var triangulation = r.Has(4) ? M.Floor(r.Number(4)) : 0;
            if (r.HasError) return r.Error;
            if (r.Has(4) && triangulation < 3) return F.Invalid();
            if (_currencies.Find(from) is not { } source || _currencies.Find(to) is not { } target) return F.Invalid();
            if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) return F.Number(value);

            var euros = value / source.Rate;
            if (triangulation > 0 && !string.Equals(from, "EUR", StringComparison.OrdinalIgnoreCase))
                euros = M.Round(euros, (int)triangulation, MidpointRounding.AwayFromZero);
            var converted = euros * target.Rate;
            return F.Number(fullPrecision ? converted : M.Round(converted, target.Decimals, MidpointRounding.AwayFromZero));
        })

        .Raw("CONVERT_OOO", 3, 3, r =>
        {
            var value = r.Number(0);
            var from = r.Text(1);
            var to = r.Text(2);
            if (r.HasError) return r.Error;
            if (_currencies.Find(from) is not { } source || _currencies.Find(to) is not { } target) return F.Invalid();
            return F.Number(value / source.Rate * target.Rate);
        })

        .Numeric1("BAHTTEXT", value => CellValue.Text(_baht.Spell(value)))

        .Build();

    private CellValue Workdays(ArgumentReader r, bool count)
    {
        var weekend = ResolveWeekend(r, 2);
        if (weekend is null)
            return CellValue.FromError(r.Has(2) && r.Raw(2) is not ArrayValue && r.Value(2) is { Kind: ValueKind.Number } code && code.AsNumber() is >= 1 and <= 17 ? CellError.Number : CellError.Value);
        if (!count && weekend == "1111111") return CellValue.FromError(CellError.Value);

        return workdays.Evaluate(r, count, new WorkWeek(weekend), 3);
    }

    private static string? ResolveWeekend(ArgumentReader r, int index)
    {
        if (!r.Has(index)) return WeekendCodes[1];

        var value = r.Value(index);
        if (value.Kind == ValueKind.Text)
        {
            var mask = value.AsText();
            return mask.Length == 7 && mask.All(c => c is '0' or '1') ? mask : null;
        }

        return value.TryCoerceToNumber(out var code) && WeekendCodes.TryGetValue((int)code, out var resolved)
            ? resolved : null;
    }
}

public sealed record EuroCurrency(double Rate, int Decimals);

public sealed class EuroCurrencies
{
    private readonly Dictionary<string, EuroCurrency> _currencies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["EUR"] = new(1, 2), ["ATS"] = new(13.7603, 2), ["BEF"] = new(40.3399, 0), ["DEM"] = new(1.95583, 2),
        ["ESP"] = new(166.386, 0), ["FIM"] = new(5.94573, 2), ["FRF"] = new(6.55957, 2), ["IEP"] = new(0.787564, 2),
        ["ITL"] = new(1936.27, 0), ["LUF"] = new(40.3399, 0), ["NLG"] = new(2.20371, 2), ["PTE"] = new(200.482, 2),
        ["GRD"] = new(340.750, 2), ["SIT"] = new(239.640, 2), ["MTL"] = new(0.429300, 2), ["CYP"] = new(0.585274, 2),
        ["SKK"] = new(30.1260, 2), ["EEK"] = new(15.6466, 2), ["LVL"] = new(0.702804, 2), ["LTL"] = new(3.45280, 2),
        ["HRK"] = new(7.53450, 2), ["BGN"] = new(1.95583, 2),
    };

    public EuroCurrency? Find(string code) => _currencies.GetValueOrDefault(code);
}

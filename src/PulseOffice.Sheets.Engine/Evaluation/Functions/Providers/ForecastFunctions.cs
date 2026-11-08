using PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;
using PulseOffice.Sheets.Values;
using F = PulseOffice.Sheets.Engine.Evaluation.Functions.FunctionBuilder;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class ForecastFunctions(IRangeMaterializer ranges, IEtsForecaster forecaster) : IFunctionProvider
{
    private const int MaximumSeasonality = 8760;

    public string Category => "Statistica";

    public IEnumerable<FunctionDefinition> GetFunctions()
    {
        var builder = new FunctionBuilder(Category);

        foreach (var (name, additive) in ((string, bool)[])[("FORECAST.ETS.ADD", true), ("FORECAST.ETS.MULT", false)])
            builder.ArrayResult(name, 3, 6, r => Forecast(r, additive));

        foreach (var (name, additive) in ((string, bool)[])[("FORECAST.ETS.PI.ADD", true), ("FORECAST.ETS.PI.MULT", false)])
            builder.ArrayResult(name, 3, 7, r => Interval(r, additive));

        foreach (var (name, additive) in ((string, bool)[])[("FORECAST.ETS.STAT.ADD", true), ("FORECAST.ETS.STAT.MULT", false)])
            builder.ArrayResult(name, 3, 6, r => Statistic(r, additive));

        builder.Raw("FORECAST.ETS.SEASONALITY", 2, 4, r =>
        {
            var request = Request(r, valuesAt: 0, timelineAt: 1, seasonalityAt: null, completionAt: 2, aggregationAt: 3, additive: true);
            if (request.Error is not CellError.None) return CellValue.FromError(request.Error);
            var outcome = forecaster.Prepare(request.Request!, null, false);
            return outcome.Calculation is { } calculation ? CellValue.Number(calculation.SamplesInPeriod) : CellValue.FromError(outcome.Error);
        });

        return builder.Build();
    }

    private EvalValue Forecast(ArgumentReader r, bool additive)
    {
        var targets = Grid(r, 0);
        var request = Request(r, valuesAt: 1, timelineAt: 2, seasonalityAt: 3, completionAt: 4, aggregationAt: 5, additive);
        if (request.Error is not CellError.None) return EvalValue.Error(request.Error);
        if (targets is null) return EvalValue.Error(CellError.Value);

        var outcome = forecaster.Prepare(request.Request!, targets.Min(row => row.Min()), false);
        return outcome.Calculation is { } calculation
            ? Shaped(targets, target => calculation.Forecast(target))
            : EvalValue.Error(outcome.Error);
    }

    private EvalValue Interval(ArgumentReader r, bool additive)
    {
        var targets = Grid(r, 0);
        var level = r.Number(3, 0.95);
        var request = Request(r, valuesAt: 1, timelineAt: 2, seasonalityAt: 4, completionAt: 5, aggregationAt: 6, additive);
        if (request.Error is not CellError.None) return EvalValue.Error(request.Error);
        if (r.HasError) return EvalValue.Error(CellError.Value);
        if (targets is null) return EvalValue.Error(CellError.Value);
        if (level is < 0 or > 1) return EvalValue.Scalar(F.Invalid());

        var outcome = forecaster.Prepare(request.Request!, targets.Min(row => row.Min()), true);
        if (outcome.Calculation is not { } calculation) return EvalValue.Error(outcome.Error);
        var seasonal = request.Request!.SamplesInPeriod != 0;
        return Shaped(targets, target => calculation.PredictionInterval(target, level, seasonal));
    }

    private EvalValue Statistic(ArgumentReader r, bool additive)
    {
        var kinds = Grid(r, 2);
        var request = Request(r, valuesAt: 0, timelineAt: 1, seasonalityAt: 3, completionAt: 4, aggregationAt: 5, additive);
        if (request.Error is not CellError.None) return EvalValue.Error(request.Error);
        if (kinds is null || kinds.SelectMany(row => row).Any(kind => (int)kind is < 1 or > 9)) return EvalValue.Scalar(F.Invalid());

        var outcome = forecaster.Prepare(request.Request!, null, false);
        return outcome.Calculation is { } calculation
            ? Shaped(kinds, kind => calculation.Statistic((int)kind))
            : EvalValue.Error(outcome.Error);
    }

    private (EtsRequest? Request, CellError Error) Request(
        ArgumentReader r, int valuesAt, int timelineAt, int? seasonalityAt, int completionAt, int aggregationAt, bool additive)
    {
        var aggregation = r.Number(aggregationAt, 1);
        var completion = r.Number(completionAt, 1);
        var seasonality = seasonalityAt is { } at ? r.Number(at, 1) : 1;
        if (r.HasError) return (null, CellError.Value);
        if ((int)aggregation is < 1 or > 7) return (null, CellError.Number);
        if ((int)completion is not (0 or 1)) return (null, CellError.Number);
        if (seasonality % 1 != 0 || seasonality < 0 || seasonality > MaximumSeasonality) return (null, CellError.Number);

        var values = Series(r, valuesAt);
        var timeline = Series(r, timelineAt);
        if (values is null || timeline is null || values.Count != timeline.Count) return (null, CellError.Value);
        return (new EtsRequest(timeline, values, (int)seasonality, (int)completion == 1, (EtsAggregation)(int)aggregation, additive), CellError.None);
    }

    private List<double>? Series(ArgumentReader r, int index)
    {
        var values = new List<double>();
        foreach (var value in ranges.Materialize(r.Raw(index), r.Context).InReadingOrder())
        {
            if (value.IsError || value.Kind != ValueKind.Number) return null;
            values.Add(value.AsNumber());
        }
        return values;
    }

    private List<List<double>>? Grid(ArgumentReader r, int index)
    {
        if (!r.Has(index)) return null;
        var grid = ranges.Materialize(r.Raw(index), r.Context);
        var rows = new List<List<double>>();
        for (var row = 0; row < grid.RowCount; row++)
        {
            var line = new List<double>();
            foreach (var value in grid.Row(row))
            {
                if (!value.TryCoerceToNumber(out var number)) return null;
                line.Add(number);
            }
            rows.Add(line);
        }
        return rows.Count == 0 || rows[0].Count == 0 ? null : rows;
    }

    private EvalValue Shaped(List<List<double>> inputs, Func<double, double?> compute)
    {
        var rows = inputs.Select(row => (IReadOnlyList<CellValue>)[.. row.Select(input => compute(input) is { } value && double.IsFinite(value)
            ? CellValue.Number(value)
            : CellValue.FromError(CellError.Number))]).ToList();
        return rows is [[var single]] ? EvalValue.Scalar(single) : new ArrayValue(rows);
    }
}

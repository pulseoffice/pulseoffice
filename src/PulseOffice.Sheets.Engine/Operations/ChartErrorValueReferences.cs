using PulseOffice.Charts;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

public interface IChartErrorValueReferences
{
    ChartDefinition Map(ChartDefinition chart, Func<string, string> reference);
}

public sealed class ChartErrorValueReferences : IChartErrorValueReferences
{
    public ChartDefinition Map(ChartDefinition chart, Func<string, string> reference)
    {
        ArgumentNullException.ThrowIfNull(chart);
        ArgumentNullException.ThrowIfNull(reference);
        if (chart.SeriesFormats is not { } formats) return chart;

        var mapped = formats.Select(format => format is null ? null : Map(format, reference)).ToList();
        return mapped.SequenceEqual(formats, ReferenceEqualityComparer.Instance) ? chart : chart with { SeriesFormats = mapped };
    }

    private ChartSeriesFormat Map(ChartSeriesFormat format, Func<string, string> reference)
    {
        var bars = Map(format.ErrorBars, reference);
        var xBars = Map(format.XErrorBars, reference);
        return ReferenceEquals(bars, format.ErrorBars) && ReferenceEquals(xBars, format.XErrorBars)
            ? format
            : format with { ErrorBars = bars, XErrorBars = xBars };
    }

    private ChartErrorBars? Map(ChartErrorBars? bars, Func<string, string> reference)
    {
        if (bars is not { Kind: ErrorBarKind.Custom }) return bars;
        var plus = Map(bars.Plus, reference);
        var minus = Map(bars.Minus, reference);
        return ReferenceEquals(plus, bars.Plus) && ReferenceEquals(minus, bars.Minus) ? bars : bars with { Plus = plus, Minus = minus };
    }

    private ChartErrorValues? Map(ChartErrorValues? values, Func<string, string> reference)
    {
        if (values?.Reference is not { } text) return values;
        var rewritten = reference(text);
        return string.Equals(rewritten, text, StringComparison.Ordinal) ? values : values with { Reference = rewritten };
    }
}

public interface IChartErrorValueRefresh
{
    ChartDefinition Refresh(Workbook workbook, ChartDefinition chart);
}

public sealed class ChartErrorValueRefresh : IChartErrorValueRefresh
{
    private readonly ChartSourceReferences _references = new();
    private readonly SelectionWorkArea _workArea = new();

    public ChartDefinition Refresh(Workbook workbook, ChartDefinition chart)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(chart);
        if (chart.InlineData is not null || chart.SeriesFormats is not { } formats) return chart;

        var refreshed = formats.Select(format => format is null ? null : Refresh(workbook, format)).ToList();
        return refreshed.SequenceEqual(formats) ? chart : chart with { SeriesFormats = refreshed };
    }

    private ChartSeriesFormat Refresh(Workbook workbook, ChartSeriesFormat format) =>
        format with { ErrorBars = Refresh(workbook, format.ErrorBars), XErrorBars = Refresh(workbook, format.XErrorBars) };

    private ChartErrorBars? Refresh(Workbook workbook, ChartErrorBars? bars) =>
        bars is not { Kind: ErrorBarKind.Custom } ? bars : bars with { Plus = Refresh(workbook, bars.Plus), Minus = Refresh(workbook, bars.Minus) };

    private ChartErrorValues? Refresh(Workbook workbook, ChartErrorValues? values)
    {
        if (values?.Reference is not { } text) return values;
        if (_references.Read(text) is not { } reference || workbook.FindSheet(reference.Sheet) is not { } sheet)
            return values;

        if ((_workArea.Bound(reference.Range, null) ?? _workArea.Bound(reference.Range, sheet.UsedRange())) is not { } range)
            return values with { Values = [] };
        var read = new List<double>();
        for (var row = range.Start.Row; row <= range.End.Row; row++)
            for (var column = range.Start.Column; column <= range.End.Column; column++)
            {
                var value = sheet[new CellAddress(row, column)].Value;
                read.Add(value.Kind == ValueKind.Number ? value.AsNumber() : double.NaN);
            }
        return values with { Values = read };
    }
}

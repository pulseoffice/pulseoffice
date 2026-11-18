using System.Text.RegularExpressions;

namespace PulseOffice.Charts;

public interface IChartInlineErrorValues
{
    ChartDefinition Refresh(ChartDefinition chart);
}

public sealed partial class ChartInlineErrorValues : IChartInlineErrorValues
{
    public ChartDefinition Refresh(ChartDefinition chart)
    {
        ArgumentNullException.ThrowIfNull(chart);
        if (chart is not { InlineData: { } data, SeriesFormats: { } formats, PivotSource: null } || chart.SourceRange.Length > 0) return chart;

        var refreshed = formats.Select(format => format is null ? null : Refresh(data, format)).ToList();
        return refreshed.SequenceEqual(formats) ? chart : chart with { SeriesFormats = refreshed };
    }

    private ChartSeriesFormat Refresh(ChartData data, ChartSeriesFormat format) =>
        format with { ErrorBars = Refresh(data, format.ErrorBars), XErrorBars = Refresh(data, format.XErrorBars) };

    private ChartErrorBars? Refresh(ChartData data, ChartErrorBars? bars) =>
        bars is not { Kind: ErrorBarKind.Custom } ? bars : bars with { Plus = Refresh(data, bars.Plus), Minus = Refresh(data, bars.Minus) };

    private ChartErrorValues? Refresh(ChartData data, ChartErrorValues? values)
    {
        if (values?.Reference is not { } text || Cells(text) is not { } range) return values;
        var (firstColumn, firstRow, lastColumn, lastRow) = range;
        if (firstColumn < 1 || lastColumn > data.Series.Count || firstRow < 2 || lastRow > data.Categories.Count + 1) return values;

        var read = new List<double>();
        for (var row = firstRow; row <= lastRow; row++)
            for (var column = firstColumn; column <= lastColumn; column++)
            {
                var series = data.Series[column - 1].Values;
                read.Add(row - 2 < series.Count ? series[row - 2] : double.NaN);
            }
        return read.SequenceEqual(values.Values) ? values : values with { Values = read };
    }

    private (int FirstColumn, int FirstRow, int LastColumn, int LastRow)? Cells(string reference)
    {
        var sides = reference.Split(':');
        if (sides.Length is < 1 or > 2) return null;
        if (Cell(sides[0]) is not { } first) return null;
        var last = sides.Length == 2 ? Cell(sides[1]) : first;
        if (last is not { } end) return null;
        return (Math.Min(first.Column, end.Column), Math.Min(first.Row, end.Row), Math.Max(first.Column, end.Column), Math.Max(first.Row, end.Row));
    }

    private (int Column, int Row)? Cell(string side)
    {
        var match = CellPattern().Match(side.Trim());
        if (!match.Success || !int.TryParse(match.Groups["row"].Value, out var row)) return null;
        var column = 0;
        foreach (var letter in match.Groups["column"].Value.ToUpperInvariant()) column = column * 26 + letter - 'A' + 1;
        return (column - 1, row);
    }

    [GeneratedRegex(@"(?:^|[!.])\$?(?<column>[A-Za-z]{1,3})\$?(?<row>[0-9]{1,7})$")]
    private static partial Regex CellPattern();
}

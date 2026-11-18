using System.Text.RegularExpressions;

namespace PulseOffice.Charts;

public interface IChartInlineErrorValues
{
    ChartDefinition Refresh(ChartDefinition chart);

    ChartDefinition Remap(ChartDefinition chart, IReadOnlyDictionary<int, int> columns);
}

public sealed partial class ChartInlineErrorValues : IChartInlineErrorValues
{
    public ChartDefinition Refresh(ChartDefinition chart)
    {
        ArgumentNullException.ThrowIfNull(chart);
        if (chart is not { InlineData: { } data, SeriesFormats: { } formats, PivotSource: null } || chart.SourceRange.Length > 0) return chart;

        var kept = Keep(data, formats);
        var refreshed = formats.Select(format => format is null ? null : Refresh(kept, format)).ToList();
        var unchanged = refreshed.SequenceEqual(formats);
        if (unchanged && ReferenceEquals(kept, data)) return chart;
        return chart with { InlineData = kept, SeriesFormats = unchanged ? formats : refreshed };
    }

    public ChartDefinition Remap(ChartDefinition chart, IReadOnlyDictionary<int, int> columns)
    {
        ArgumentNullException.ThrowIfNull(chart);
        ArgumentNullException.ThrowIfNull(columns);
        if (chart.SeriesFormats is not { } formats || columns.All(pair => pair.Key == pair.Value)) return chart;
        return chart with
        {
            SeriesFormats = [.. formats.Select(format => format is null ? null : format with
            {
                ErrorBars = Remap(format.ErrorBars, columns),
                XErrorBars = Remap(format.XErrorBars, columns),
            })],
        };
    }

    private ChartErrorBars? Remap(ChartErrorBars? bars, IReadOnlyDictionary<int, int> columns) =>
        bars is not { Kind: ErrorBarKind.Custom } ? bars : bars with { Plus = Remap(bars.Plus, columns), Minus = Remap(bars.Minus, columns) };

    private ChartErrorValues? Remap(ChartErrorValues? values, IReadOnlyDictionary<int, int> columns)
    {
        if (values?.Reference is not { } text) return values;
        var sides = text.Split(':').Select(side => Remap(side, columns)).ToList();
        return sides.Any(side => side is null) ? values : values with { Reference = string.Join(':', sides) };
    }

    private string? Remap(string side, IReadOnlyDictionary<int, int> columns)
    {
        var match = CellPattern().Match(side);
        if (!match.Success) return null;
        var group = match.Groups["column"];
        if (!columns.TryGetValue(Column(group.Value), out var target)) return side;
        return side[..group.Index] + Letters(target) + side[(group.Index + group.Length)..];
    }

    private ChartData Keep(ChartData data, IReadOnlyList<ChartSeriesFormat?> formats)
    {
        var hidden = new List<ChartHiddenColumn>(data.HiddenColumns ?? []);
        var added = false;
        foreach (var values in formats.OfType<ChartSeriesFormat>().SelectMany(Referenced))
        {
            if (values.Reference is not { } text || Cells(text) is not { } range || !Inside(data, range.FirstRow, range.LastRow)) continue;
            var (firstColumn, firstRow, lastColumn, lastRow) = range;
            var width = lastColumn - firstColumn + 1;
            for (var column = Math.Max(firstColumn, 1); column <= lastColumn; column++)
            {
                if (column <= data.Series.Count || hidden.Any(item => item.Column == column)) continue;
                var cells = Enumerable.Repeat(double.NaN, data.Categories.Count).ToArray();
                for (var row = firstRow; row <= lastRow; row++)
                {
                    var position = (row - firstRow) * width + column - firstColumn;
                    if (position < values.Values.Count) cells[row - 2] = values.Values[position];
                }
                hidden.Add(new ChartHiddenColumn(column, cells));
                added = true;
            }
        }
        return added ? data with { HiddenColumns = [.. hidden.OrderBy(item => item.Column)] } : data;
    }

    private IEnumerable<ChartErrorValues> Referenced(ChartSeriesFormat format) =>
        new[] { format.ErrorBars, format.XErrorBars }
            .OfType<ChartErrorBars>()
            .Where(bars => bars.Kind == ErrorBarKind.Custom)
            .SelectMany(bars => new[] { bars.Plus, bars.Minus })
            .OfType<ChartErrorValues>();

    private bool Inside(ChartData data, int firstRow, int lastRow) => firstRow >= 2 && lastRow <= data.Categories.Count + 1;

    private ChartSeriesFormat Refresh(ChartData data, ChartSeriesFormat format) =>
        format with { ErrorBars = Refresh(data, format.ErrorBars), XErrorBars = Refresh(data, format.XErrorBars) };

    private ChartErrorBars? Refresh(ChartData data, ChartErrorBars? bars) =>
        bars is not { Kind: ErrorBarKind.Custom } ? bars : bars with { Plus = Refresh(data, bars.Plus), Minus = Refresh(data, bars.Minus) };

    private ChartErrorValues? Refresh(ChartData data, ChartErrorValues? values)
    {
        if (values?.Reference is not { } text || Cells(text) is not { } range) return values;
        var (firstColumn, firstRow, lastColumn, lastRow) = range;
        if (firstColumn < 1 || !Inside(data, firstRow, lastRow)) return values;
        var lanes = Enumerable.Range(firstColumn, lastColumn - firstColumn + 1).Select(column => Lane(data, column)).ToList();
        if (lanes.Any(lane => lane is null)) return values;

        var read = new List<double>();
        for (var row = firstRow; row <= lastRow; row++)
            foreach (var lane in lanes)
                read.Add(row - 2 < lane!.Count ? lane[row - 2] : double.NaN);
        return read.SequenceEqual(values.Values) ? values : values with { Values = read };
    }

    private IReadOnlyList<double>? Lane(ChartData data, int column) =>
        column >= 1 && column <= data.Series.Count ? data.Series[column - 1].Values : data.HiddenColumn(column)?.Values;

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
        return (Column(match.Groups["column"].Value), row);
    }

    private int Column(string letters)
    {
        var column = 0;
        foreach (var letter in letters.ToUpperInvariant()) column = column * 26 + letter - 'A' + 1;
        return column - 1;
    }

    private string Letters(int column)
    {
        var letters = string.Empty;
        for (var number = column + 1; number > 0; number = (number - 1) / 26)
            letters = (char)('A' + (number - 1) % 26) + letters;
        return letters;
    }

    [GeneratedRegex(@"(?:^|[!.])\$?(?<column>[A-Za-z]{1,3})\$?(?<row>[0-9]{1,7})$")]
    private static partial Regex CellPattern();
}

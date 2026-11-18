using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PulseOffice.Charts;
using PulseOffice.Formats.Odf.TextCodecs;

namespace PulseOffice.Formats.Odf;

internal sealed class InlineChartDocumentReader(OdfChartVocabulary vocabulary, IOdfParagraphText paragraphs) : OdfChartDocumentReader(vocabulary, paragraphs)
{
    private const int MostColumns = 1024;

    private const int MostValues = 1 << 20;

    private readonly Regex _cell = new(@"\.\$?(?<column>[A-Za-z]{1,3})\$?[0-9]{1,7}", RegexOptions.CultureInvariant);

    private readonly ChartInlineErrorValues _errors = new();

    private IEnumerable<XElement> Expanded(XElement row) =>
        row.Elements(Table + "table-cell")
            .SelectMany(cell => Enumerable.Repeat(cell, int.TryParse((string?)cell.Attribute(Table + "number-columns-repeated"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var repeated)
                ? Math.Clamp(repeated, 1, MostColumns) : 1))
            .Take(MostColumns);

    protected override ChartDefinition Source(XElement chart, XElement plot, ChartDefinition common)
    {
        if (chart.Element(Table + "table") is not { } table) return common with { InlineData = new ChartData([], []) };

        var header = table.Element(Table + "table-header-rows")?.Element(Table + "table-row") is { } first ? Expanded(first).ToList() : [];
        var rows = Rows(table);
        var width = rows.Select(row => row.Count).Append(header.Count).DefaultIfEmpty(0).Max();
        if (width == 0) return common with { InlineData = new ChartData([], []) };

        var categories = Columns((string?)plot.Elements(Chart + "axis").Elements(Chart + "categories").FirstOrDefault()?.Attribute(Table + "cell-range-address"));
        var leaf = categories?.Last ?? 0;
        var levels = LevelColumns(table, categories);
        var series = SeriesColumns(plot, leaf, levels, width);
        var taken = new HashSet<int>(series.Append(leaf).Concat(levels));
        var hidden = Enumerable.Range(0, width).Where(column => !taken.Contains(column) && rows.Any(row => column < row.Count && (row[column].HasElements || row[column].Attribute(Office + "value") is not null))).ToList();

        var map = new Dictionary<int, int>();
        for (var index = 0; index < series.Count; index++) map[series[index]] = index + 1;
        var next = series.Count + 1;
        foreach (var column in hidden)
        {
            next = Math.Max(next, column);
            map[column] = next++;
        }

        var data = new ChartData(
            [.. rows.Select(row => LabelAt(row, leaf))],
            [.. series.Select((column, index) => new ChartSeries(column < header.Count && Paragraphs.Read(header[column]) is { Length: > 0 } name ? name : $"Serie {index + 1}",
                [.. rows.Select(row => NumberAt(row, column))]))])
        {
            HiddenColumns = hidden.Count == 0 ? null : [.. hidden.Select(column => new ChartHiddenColumn(map[column], [.. rows.Select(row => NumberAt(row, column))]))],
        };
        data = data.WithCategoryLevels(levels.Count == 0 ? null : [.. levels.Select(column => (IReadOnlyList<string>)rows.Select(row => LabelAt(row, column)).ToList())]);
        return _errors.Refresh(_errors.Remap(common with { InlineData = data }, map));
    }

    private List<List<XElement>> Rows(XElement table)
    {
        var rows = new List<List<XElement>>();
        long cells = 0;
        foreach (var row in table.Element(Table + "table-rows")?.Elements(Table + "table-row") ?? [])
        {
            var expanded = Expanded(row).ToList();
            if (expanded.Count == 0) continue;
            cells += expanded.Count;
            if (cells > MostValues) break;
            rows.Add(expanded);
        }
        return rows;
    }

    private List<int> LevelColumns(XElement table, (int First, int Last)? categories)
    {
        if (int.TryParse((string?)table.Attribute(PulseChart + "category-level-column"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var first)
            && int.TryParse((string?)table.Attribute(PulseChart + "category-level-count"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
            && first > 0 && count > 0 && first + count <= MostColumns)
            return [.. Enumerable.Range(first, count)];
        return categories is { } range && range.Last > range.First ? [.. Enumerable.Range(range.First, range.Last - range.First)] : [];
    }

    private List<int> SeriesColumns(XElement plot, int leaf, IReadOnlyList<int> levels, int width)
    {
        var declared = plot.Elements(Chart + "series").Select(series => Columns((string?)series.Attribute(Chart + "values-cell-range-address"))).ToList();
        if (declared.Count > 0 && declared.All(range => range is not null))
        {
            var columns = declared.Select(range => range!.Value.First).ToList();
            if (columns.Distinct().Count() == columns.Count && columns.All(column => column != leaf && !levels.Contains(column))) return columns;
        }
        return [.. Enumerable.Range(0, width).Where(column => column != leaf && !levels.Contains(column))];
    }

    private (int First, int Last)? Columns(string? address)
    {
        if (string.IsNullOrWhiteSpace(address) || !address.Contains("local-table", StringComparison.OrdinalIgnoreCase)) return null;
        var matches = _cell.Matches(address);
        if (matches.Count == 0) return null;
        var first = Column(matches[0].Groups["column"].Value);
        var last = Column(matches[^1].Groups["column"].Value);
        return (Math.Min(first, last), Math.Max(first, last));
    }

    private int Column(string letters)
    {
        var column = 0;
        foreach (var letter in letters.ToUpperInvariant()) column = column * 26 + letter - 'A' + 1;
        return column - 1;
    }

    private string LabelAt(List<XElement> row, int column) => column < row.Count ? Paragraphs.Read(row[column]) : string.Empty;

    private double NumberAt(List<XElement> row, int column)
    {
        if (column >= row.Count) return double.NaN;
        var text = (string?)row[column].Attribute(Office + "value") ?? row[column].Value;
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number) ? number : double.NaN;
    }
}

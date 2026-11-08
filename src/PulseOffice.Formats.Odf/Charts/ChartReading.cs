using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Charts;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;

namespace PulseOffice.Formats.Odf;

internal abstract class OdfChartDocumentReader(OdfChartVocabulary vocabulary) : OdfChartXml
{
    public ChartDefinition? Read(XElement chart, string id)
    {
        if (chart.Element(Chart + "plot-area") is not { } plot) return null;

        var title = chart.Element(Chart + "title")?.Element(Text + "p")?.Value;
        var chartClass = (string?)chart.Attribute(Chart + "class");
        var type = vocabulary.Declared((string?)chart.Attribute(PulseChart + "type"), chartClass,
            vocabulary.Refine(vocabulary.TypeOf(chartClass, Flags(chart, plot)), Traits(chart, plot)));
        var lines = plot.Elements(Chart + "series").Count(series => (string?)series.Attribute(Chart + "class") == "chart:line");
        var combined = type is ChartType.Column or ChartType.ColumnLine && chartClass == "chart:bar" && lines > 0 && lines < plot.Elements(Chart + "series").Count();
        return Source(chart, plot, new ChartDefinition
        {
            Id = id,
            Type = combined ? ChartType.ColumnLine : type == ChartType.ColumnLine ? ChartType.Column : type,
            LineSeriesCount = combined ? lines : 1,
            SourceSheet = string.Empty,
            SourceRange = string.Empty,
            ChartTitle = string.IsNullOrEmpty(title) ? null : title,
            Legend = vocabulary.LegendOf((string?)chart.Element(Chart + "legend")?.Attribute(Chart + "legend-position")),
            CategoryAxisTitle = AxisTitle(plot, "x"),
            ValueAxisTitle = AxisTitle(plot, "y"),
            ShowGridLines = Axis(plot, "y")?.Elements(Chart + "grid").Any() ?? false,
            ShowDataLabels = plot.Elements(Chart + "series").Any(series => HasLabels(chart, series)),
            SeriesNames = SeriesNames(plot),
            SeriesColors = vocabulary.IsCircular(type) ? null : SeriesColors(chart, plot),
            ThreeDimensional = (string?)PlotProperties(chart, plot)?.Attribute(Chart + "three-dimensional") == "true",
        });
    }

    private IReadOnlyList<string?>? SeriesColors(XElement chart, XElement plot)
    {
        var colors = plot.Elements(Chart + "series").Select(series => SeriesColor(chart, series)).ToList();
        return colors.Any(color => color is not null) ? colors : null;
    }

    private string? SeriesColor(XElement chart, XElement series)
    {
        var name = (string?)series.Attribute(Chart + "style-name");
        var graphic = Named(chart, name)?.Element(Style + "graphic-properties");
        if (graphic is null) return null;
        var line = (string?)series.Attribute(Chart + "class") == "chart:line" || (string?)chart.Attribute(Chart + "class") is "chart:line" or "chart:scatter";
        var color = line
            ? (string?)graphic.Attribute(Svg + "stroke-color") ?? (string?)graphic.Attribute(Draw + "fill-color")
            : (string?)graphic.Attribute(Draw + "fill") == "none" ? null : (string?)graphic.Attribute(Draw + "fill-color");
        return color is { Length: 7 } && color[0] == '#' ? color.ToUpperInvariant() : null;
    }

    private IReadOnlyList<string>? SeriesNames(XElement plot)
    {
        var names = plot.Elements(Chart + "series").Select(series => (string?)series.Attribute(Loext + "label-string") ?? string.Empty).ToList();
        return names.Any(name => name.Length > 0) ? names : null;
    }

    protected abstract ChartDefinition Source(XElement chart, XElement plot, ChartDefinition common);

    private XElement? Axis(XElement plot, string dimension) =>
        plot.Elements(Chart + "axis").FirstOrDefault(axis => (string?)axis.Attribute(Chart + "dimension") == dimension);

    private string? AxisTitle(XElement plot, string dimension) =>
        Axis(plot, dimension)?.Element(Chart + "title")?.Elements(Text + "p").Select(paragraph => paragraph.Value).FirstOrDefault(text => text.Length > 0);

    private bool HasLabels(XElement chart, XElement series)
    {
        var name = (string?)series.Attribute(Chart + "style-name");
        var properties = Named(chart, name)?.Elements().FirstOrDefault(element => element.Name.LocalName == "chart-properties");
        return properties is not null
            && (((string?)properties.Attribute(Chart + "data-label-number") ?? "none") != "none"
                || (string?)properties.Attribute(Chart + "data-label-text") == "true");
    }

    private XElement? PlotProperties(XElement chart, XElement plot)
    {
        var name = (string?)plot.Attribute(Chart + "style-name");
        return Named(chart, name)?.Elements().FirstOrDefault(element => element.Name.LocalName == "chart-properties");
    }

    private OdfChartTraits Traits(XElement chart, XElement plot)
    {
        var properties = PlotProperties(chart, plot);
        var series = plot.Elements(Chart + "series").ToList();
        var styles = series.Select(item => SeriesStyle(chart, item)).ToList();
        return new OdfChartTraits(
            (string?)properties?.Attribute(Chart + "interpolation"),
            (string?)properties?.Attribute(Chart + "japanese-candle-stick") == "true",
            series.Count,
            styles.Any(style => double.TryParse((string?)style?.Elements().FirstOrDefault(element => element.Name.LocalName == "chart-properties")?.Attribute(Chart + "pie-offset"),
                NumberStyles.Float, CultureInfo.InvariantCulture, out var offset) && offset > 0),
            styles.Count > 0 && styles.All(style => (string?)style?.Element(Style + "graphic-properties")?.Attribute(Draw + "stroke") != "none"));
    }

    private XElement? SeriesStyle(XElement chart, XElement series)
    {
        var name = (string?)series.Attribute(Chart + "style-name");
        return Named(chart, name);
    }

    private XElement? Named(XElement chart, string? name)
    {
        if (name is null || chart.Document is not { } document) return null;
        if (document.Annotation<ChartStyleIndex>() is not { } index)
        {
            index = new ChartStyleIndex(document.Descendants(Style + "style"), Style + "name");
            document.AddAnnotation(index);
        }
        return index.Find(name);
    }

    private ChartPlotFlags Flags(XElement chart, XElement plot)
    {
        if (PlotProperties(chart, plot) is not { } properties) return default;

        return new ChartPlotFlags(
            (string?)properties.Attribute(Chart + "vertical") == "true",
            (string?)properties.Attribute(Chart + "stacked") == "true",
            (string?)properties.Attribute(Chart + "percentage") == "true");
    }
}

internal sealed class ChartStyleIndex
{
    private readonly Dictionary<string, XElement> _styles = new(StringComparer.Ordinal);

    public ChartStyleIndex(IEnumerable<XElement> styles, XName nameAttribute)
    {
        foreach (var style in styles)
            if ((string?)style.Attribute(nameAttribute) is { } name) _styles.TryAdd(name, style);
    }

    public XElement? Find(string name) => _styles.GetValueOrDefault(name);
}

internal sealed class CellRangeAddresses
{
    private readonly OdfQualifiedRanges _ranges = new();

    public (string Sheet, string Range) Split(string address)
    {
        var found = _ranges.ReadAll(address);
        if (found.Count == 0) return (string.Empty, "A1");

        var range = Enclosing(found.Where(item => item.Sheet == found[0].Sheet).Select(item => item.Range));
        return (found[0].Sheet, range.IsSingleCell ? range.Start.ToA1() : $"{range.Start.ToA1()}:{range.End.ToA1()}");
    }

    public string Union(IEnumerable<string> addresses)
    {
        var found = addresses.SelectMany(_ranges.ReadAll).ToList();
        if (found.Count == 0) return string.Empty;

        return _ranges.Write(found[0].Sheet, Enclosing(found.Where(item => item.Sheet == found[0].Sheet).Select(item => item.Range)));
    }

    private CellRange Enclosing(IEnumerable<CellRange> ranges)
    {
        var list = ranges.ToList();
        return new CellRange(
            new CellAddress(list.Min(range => range.Start.Row), list.Min(range => range.Start.Column)),
            new CellAddress(list.Max(range => range.End.Row), list.Max(range => range.End.Column)));
    }
}

internal sealed class SheetChartDocumentReader(OdfChartVocabulary vocabulary, CellRangeAddresses addresses)
    : OdfChartDocumentReader(vocabulary)
{
    protected override ChartDefinition Source(XElement chart, XElement plot, ChartDefinition common)
    {
        var whole = (string?)plot.Attribute(Table + "cell-range-address") ?? addresses.Union(
            plot.Descendants().SelectMany(e => e.Attributes())
                .Where(a => a.Name.LocalName is "values-cell-range-address" or "label-cell-address" or "cell-range-address")
                .Select(a => a.Value));
        var declared = (string?)plot.Attribute(Chart + "data-source-has-labels");
        var (sheet, range) = addresses.Split(whole);
        var firstSeries = addresses.Split((string?)plot.Elements(Chart + "series").FirstOrDefault()?.Attribute(Chart + "values-cell-range-address") ?? string.Empty);
        var inRows = CellRange.TryParseA1(firstSeries.Range, out var values) && values.RowCount == 1 && values.ColumnCount > 1;

        return _layout.Settle(common with
        {
            SourceSheet = sheet,
            SourceRange = range,
            FirstRowIsHeader = declared is null ? inRows ? Categorised(plot) : Named(plot) : declared is "both" or "row",
            FirstColumnIsCategory = declared is null ? inRows ? Named(plot) : Categorised(plot) : declared is "both" or "column",
            SeriesInColumns = !inRows,
            ChartTitle = common.ChartTitle,
        }, Sources(plot));
    }

    private readonly SeriesSourceAddresses _sources = new();
    private readonly ChartSourceLayout _layout = new();

    private IReadOnlyList<ChartSeriesSource> Sources(XElement plot)
    {
        var categories = _sources.Model((string?)plot.Elements(Chart + "axis").Elements(Chart + "categories").FirstOrDefault()?.Attribute(Table + "cell-range-address"));
        var sources = plot.Elements(Chart + "series").Select(series => new ChartSeriesSource(
            _sources.Model((string?)series.Attribute(Chart + "label-cell-address")),
            _sources.Model((string?)series.Attribute(Chart + "values-cell-range-address")),
            categories)).ToList();
        return sources.Any(source => source.Values is not null) ? sources : [];
    }

    private bool Named(XElement plot) => plot.Elements(Chart + "series").Any(series => series.Attribute(Chart + "label-cell-address") is not null || series.Attribute(Loext + "label-string") is not null);

    private bool Categorised(XElement plot) => plot.Descendants(Chart + "categories").Any();
}

internal sealed class InlineChartDocumentReader(OdfChartVocabulary vocabulary) : OdfChartDocumentReader(vocabulary)
{
    private const int MostColumns = 1024;

    private const int MostValues = 1 << 20;

    private IEnumerable<XElement> Expanded(XElement row) =>
        row.Elements(Table + "table-cell")
            .SelectMany(cell => Enumerable.Repeat(cell, int.TryParse((string?)cell.Attribute(Table + "number-columns-repeated"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var repeated)
                ? Math.Clamp(repeated, 1, MostColumns) : 1))
            .Take(MostColumns);

    protected override ChartDefinition Source(XElement chart, XElement plot, ChartDefinition common) =>
        common with { InlineData = Data(chart.Element(Table + "table")) };

    private ChartData Data(XElement? table)
    {
        if (table is null) return new ChartData([], []);

        var names = table.Element(Table + "table-header-rows")?.Element(Table + "table-row")
            ?.Elements(Table + "table-cell").Skip(1).Select(cell => cell.Value).ToList() ?? [];
        var categories = new List<string>();
        var values = new List<List<double>>();

        foreach (var row in table.Element(Table + "table-rows")?.Elements(Table + "table-row") ?? [])
        {
            var cells = Expanded(row).ToList();
            if (cells.Count == 0) continue;
            if ((long)(categories.Count + 1) * Math.Max(values.Count, cells.Count - 1) > MostValues) break;

            categories.Add(cells[0].Value);
            for (var index = 1; index < cells.Count; index++)
            {
                while (values.Count < index) values.Add([.. Enumerable.Repeat(double.NaN, categories.Count - 1)]);
                var text = (string?)cells[index].Attribute(Office + "value") ?? cells[index].Value;
                values[index - 1].Add(double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number) ? number : double.NaN);
            }
            foreach (var series in values)
                while (series.Count < categories.Count) series.Add(double.NaN);
        }

        return new ChartData(categories,
            [.. values.Select((series, index) => new ChartSeries(index < names.Count ? names[index] : $"Serie {index + 1}", series))]);
    }
}



using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Charts;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Formats.Odf;

internal abstract class OdfChartXml
{
    protected static readonly XNamespace Office = OdfPackage.Namespaces.Office;
    protected static readonly XNamespace Chart = OdfPackage.Namespaces.Chart;
    protected static readonly XNamespace Style = OdfPackage.Namespaces.Style;
    protected static readonly XNamespace Table = OdfPackage.Namespaces.Table;
    protected static readonly XNamespace Text = OdfPackage.Namespaces.Text;
    protected static readonly XNamespace Svg = OdfPackage.Namespaces.Svg;
    protected static readonly XNamespace Xlink = OdfPackage.Namespaces.Xlink;
    protected static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;
    protected static readonly XNamespace Loext = OdfPackage.Namespaces.Loext;
    protected static readonly XNamespace PulseChart = "urn:pulseoffice:chart";

    protected const string PlotStyleName = "pulseoffice-plot";

    protected const string SeriesStylePrefix = "pulseoffice-series-";

    protected const string AxisStyleName = "pulseoffice-axis";

    protected const string ChartStyleName = "pulseoffice-chart";

    protected const string WallStyleName = "pulseoffice-wall";

    protected const string LegendStyleName = "pulseoffice-legend";

    protected const string GridStyleName = "pulseoffice-grid";

    protected const string Hairline = "#b3b3b3";
}

internal abstract class OdfChartDocumentBuilder(OdfChartVocabulary vocabulary, OdfUnits units) : OdfChartXml
{
    public XDocument Build(ChartDefinition definition, ChartData data, DocumentSize frame) =>
        Build(definition, data, frame, CultureInfo.InvariantCulture);

    public XDocument Build(ChartDefinition definition, ChartData data, DocumentSize frame, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        var size = frame.IsEmpty ? definition.NaturalSize : frame;
        var chartClass = vocabulary.ClassOf(definition.Type);
        var plot = new XElement(Chart + "plot-area", new XAttribute(Chart + "style-name", PlotStyleName), PlotAttributes(definition));

        var categories = CategoriesAddress(definition, data);
        var horizontal = Axis("x", definition.CategoryAxisTitle, false);
        if (categories is not null)
            horizontal.Add(new XElement(Chart + "categories", new XAttribute(Table + "cell-range-address", categories)));

        if (!vocabulary.IsCircular(definition.Type))
            plot.Add(horizontal, Axis("y", definition.ValueAxisTitle, definition.ShowGridLines));
        else if (categories is not null)
            plot.Add(horizontal);

        for (var index = 0; index < data.Series.Count; index++)
            plot.Add(new XElement(Chart + "series",
                new XAttribute(Chart + "style-name", SeriesStyleName(index)),
                ValuesAddress(definition, data, index) is { Length: > 0 } values ? new XAttribute(Chart + "values-cell-range-address", values) : null,
                definition.ExplicitSeriesName(index) is { } explicitName
                    ? new XAttribute(Loext + "label-string", explicitName)
                    : LabelAddress(definition, index) is { Length: > 0 } label ? new XAttribute(Chart + "label-cell-address", label) : null,
                new XAttribute(Chart + "class", definition.IsLineSeries(index, data.Series.Count) ? "chart:line" : chartClass)));

        if (!vocabulary.IsCircular(definition.Type)) plot.Add(new XElement(Chart + "wall", new XAttribute(Chart + "style-name", WallStyleName)));

        var chart = new XElement(Chart + "chart",
            new XAttribute(Chart + "style-name", ChartStyleName),
            new XAttribute(Chart + "class", chartClass),
            new XAttribute(PulseChart + "type", definition.Type.ToString()),
            new XAttribute(Svg + "width", units.Cm(size.Width)),
            new XAttribute(Svg + "height", units.Cm(size.Height)));

        if (definition.ChartTitle is { Length: > 0 } title)
            chart.Add(new XElement(Chart + "title", new XElement(Text + "p", title)));
        if (definition.Legend != LegendPosition.None)
            chart.Add(new XElement(Chart + "legend",
                new XAttribute(Chart + "legend-position", vocabulary.LegendName(definition.Legend)),
                new XAttribute(Chart + "style-name", LegendStyleName)));

        chart.Add(plot);
        chart.Add(Supplement(data, culture));

        return new XDocument(new XElement(Office + "document-content",
            new XAttribute(XNamespace.Xmlns + "office", Office.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "chart", Chart.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "style", Style.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "table", Table.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "text", Text.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "svg", Svg.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "draw", Draw.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xlink", Xlink.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "loext", Loext.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "pulseoffice", PulseChart.NamespaceName),
            new XAttribute(Office + "version", "1.3"),
            new XElement(Office + "automatic-styles", PlotStyle(definition), AxisStyle(), FrameStyles(), Enumerable.Range(0, data.Series.Count).Select(index => SeriesStyle(index, definition, vocabulary.IsCircular(definition.Type) ? null : definition.SeriesColor(index)))),
            new XElement(Office + "body", new XElement(Office + "chart", chart))));
    }

    protected abstract IEnumerable<XAttribute> PlotAttributes(ChartDefinition definition);

    protected abstract string? ValuesAddress(ChartDefinition definition, ChartData data, int series);

    protected abstract string? LabelAddress(ChartDefinition definition, int series);

    protected virtual IEnumerable<XElement> Supplement(ChartData data, CultureInfo culture) => [];

    protected virtual string? CategoriesAddress(ChartDefinition definition, ChartData data) => null;

    private XElement Axis(string dimension, string? title, bool grid)
    {
        var axis = new XElement(Chart + "axis", new XAttribute(Chart + "dimension", dimension), new XAttribute(Chart + "name", $"primary-{dimension}"),
            new XAttribute(Chart + "style-name", AxisStyleName));
        if (title is { Length: > 0 }) axis.Add(new XElement(Chart + "title", new XElement(Text + "p", title)));
        if (grid) axis.Add(new XElement(Chart + "grid", new XAttribute(Chart + "style-name", GridStyleName), new XAttribute(Chart + "class", "major")));
        return axis;
    }

    private XElement AxisStyle() =>
        new(Style + "style",
            new XAttribute(Style + "name", AxisStyleName),
            new XAttribute(Style + "family", "chart"),
            new XElement(Style + "chart-properties", new XAttribute(Chart + "display-label", "true")),
            new XElement(Style + "graphic-properties", new XAttribute(Svg + "stroke-color", Hairline)),
            TextSize());

    private XElement TextSize() =>
        new(Style + "text-properties", new XAttribute(OdfPackage.Namespaces.Fo + "font-size", "10pt"));

    private IEnumerable<XElement> FrameStyles()
    {
        yield return Graphic(ChartStyleName, new XAttribute(Draw + "stroke", "none"), new XAttribute(Draw + "fill", "solid"), new XAttribute(Draw + "fill-color", "#ffffff"));
        yield return Graphic(WallStyleName, new XAttribute(Draw + "stroke", "solid"), new XAttribute(Svg + "stroke-color", Hairline), new XAttribute(Draw + "fill", "none"));
        yield return Graphic(GridStyleName, new XAttribute(Svg + "stroke-color", Hairline));
        var legend = Graphic(LegendStyleName, new XAttribute(Draw + "stroke", "none"), new XAttribute(Draw + "fill", "none"));
        legend.Add(TextSize());
        yield return legend;
    }

    private XElement Graphic(string name, params XAttribute[] attributes) =>
        new(Style + "style",
            new XAttribute(Style + "name", name),
            new XAttribute(Style + "family", "chart"),
            new XElement(Style + "graphic-properties", attributes));

    private string SeriesStyleName(int index) => $"{SeriesStylePrefix}{index + 1}";

    private XElement SeriesStyle(int index, ChartDefinition definition, string? colour)
    {
        var properties = new XElement(Style + "chart-properties", new XAttribute(Chart + "data-label-number", definition.ShowDataLabels ? "value" : "none"));
        if (vocabulary.PieOffsetOf(definition.Type) is > 0 and var offset)
            properties.Add(new XAttribute(Chart + "pie-offset", offset.ToString(CultureInfo.InvariantCulture)));

        if (vocabulary.ScatterLinesOf(definition.Type) is not null)
            properties.Add(new XAttribute(Chart + "symbol-type", "automatic"));

        var graphic = new XElement(Style + "graphic-properties");
        if (colour is not null)
            graphic.Add(new XAttribute(Draw + "fill", "solid"),
                new XAttribute(Draw + "fill-color", colour.ToLowerInvariant()),
                new XAttribute(Svg + "stroke-color", colour.ToLowerInvariant()));
        if (vocabulary.ScatterLinesOf(definition.Type) is false)
            graphic.Add(new XAttribute(Draw + "stroke", "none"));

        return new XElement(Style + "style",
            new XAttribute(Style + "name", SeriesStyleName(index)),
            new XAttribute(Style + "family", "chart"),
            properties,
            graphic.HasAttributes ? graphic : null);
    }

    private XElement PlotStyle(ChartDefinition definition)
    {
        var flags = vocabulary.FlagsOf(definition.Type);
        var properties = new XElement(Style + "chart-properties");
        if (flags.Horizontal) properties.Add(new XAttribute(Chart + "vertical", "true"));
        if (flags.Stacked) properties.Add(new XAttribute(Chart + "stacked", "true"));
        if (flags.Percent) properties.Add(new XAttribute(Chart + "percentage", "true"));
        if (definition.ThreeDimensional) properties.Add(new XAttribute(Chart + "three-dimensional", "true"));
        if (vocabulary.InterpolationOf(definition.Type) is { } interpolation) properties.Add(new XAttribute(Chart + "interpolation", interpolation));
        if (vocabulary.IsCandlestick(definition.Type)) properties.Add(new XAttribute(Chart + "japanese-candle-stick", "true"));

        return new XElement(Style + "style",
            new XAttribute(Style + "name", PlotStyleName),
            new XAttribute(Style + "family", "chart"),
            properties);
    }
}

internal sealed class SheetChartDocumentBuilder(OdfChartVocabulary vocabulary, OdfUnits units)
    : OdfChartDocumentBuilder(vocabulary, units)
{
    private readonly SeriesSourceAddresses _sources = new();

    protected override IEnumerable<XAttribute> PlotAttributes(ChartDefinition definition) =>
        definition.HasSeriesSources
            ?
            [
                new(Table + "cell-range-address", _sources.Whole(definition)),
                new(Chart + "data-source-has-labels", "none"),
            ]
            : Compact(definition);

    private IEnumerable<XAttribute> Compact(ChartDefinition definition) =>
    [
        new(Table + "cell-range-address", new SourceRange(definition).Whole()),
        new(Chart + "data-source-has-labels", (_fit.Fit(definition).FirstRowIsHeader, _fit.Fit(definition).FirstColumnIsCategory) switch
        {
            (true, true) => "both",
            (true, false) => "row",
            (false, true) => "column",
            _ => "none",
        }),
    ];

    protected override string? ValuesAddress(ChartDefinition definition, ChartData data, int series) =>
        definition.HasSeriesSources ? _sources.Values(definition, series) : new SourceRange(definition).Values(series);

    protected override string? LabelAddress(ChartDefinition definition, int series) =>
        definition.HasSeriesSources ? _sources.Label(definition, series) : new SourceRange(definition).Label(series);

    protected override string? CategoriesAddress(ChartDefinition definition, ChartData data) =>
        definition.HasSeriesSources ? _sources.Categories(definition) : _fit.Fit(definition) is var fitted && (fitted.SeriesInColumns ? fitted.FirstColumnIsCategory : fitted.FirstRowIsHeader) ? new SourceRange(fitted).Categories() : null;

    private readonly ChartLabelFit _fit = new();

    private sealed class SourceRange(ChartDefinition source)
    {
        private readonly ChartDefinition definition = new ChartLabelFit().Fit(source);

        private readonly CellRange _range = CellRange.TryParseA1(source.SourceRange, out var parsed)
            ? parsed
            : CellRange.Single(new CellAddress(0, 0));

        private int DataTop => definition.FirstRowIsHeader ? _range.Start.Row + 1 : _range.Start.Row;

        private int DataLeft => definition.FirstColumnIsCategory ? _range.Start.Column + 1 : _range.Start.Column;

        public string Whole() => Range(_range.Start, _range.End);

        public string Values(int index) =>
            definition.SeriesInColumns
                ? Range(new CellAddress(DataTop, DataLeft + index), new CellAddress(_range.End.Row, DataLeft + index))
                : Range(new CellAddress(DataTop + index, DataLeft), new CellAddress(DataTop + index, _range.End.Column));

        public string Label(int index) =>
            Cell(definition.SeriesInColumns
                ? new CellAddress(_range.Start.Row, DataLeft + index)
                : new CellAddress(DataTop + index, _range.Start.Column));

        private readonly PulseOffice.Sheets.Engine.Parsing.SheetNameQuoting _quoting = new();

        public string? Categories() =>
            definition.SeriesInColumns
                ? (DataTop <= _range.End.Row ? Range(new CellAddress(DataTop, _range.Start.Column), new CellAddress(_range.End.Row, _range.Start.Column)) : null)
                : (DataLeft <= _range.End.Column ? Range(new CellAddress(_range.Start.Row, DataLeft), new CellAddress(_range.Start.Row, _range.End.Column)) : null);

        private string Cell(CellAddress address) =>
            string.IsNullOrEmpty(definition.SourceSheet) ? "." + address.ToA1() : _quoting.Qualified(definition.SourceSheet, address.ToA1());

        private string Range(CellAddress from, CellAddress to) => $"{Cell(from)}:{Cell(to)}";
    }
}

internal sealed class InlineChartDocumentBuilder(OdfChartVocabulary vocabulary, OdfUnits units)
    : OdfChartDocumentBuilder(vocabulary, units)
{
    protected override IEnumerable<XAttribute> PlotAttributes(ChartDefinition definition) => [];

    protected override string ValuesAddress(ChartDefinition definition, ChartData data, int series)
    {
        var column = new CellAddress(0, series + 1).ToA1().TrimEnd('1');
        return $"local-table.{column}2:.{column}{Rows(data) + 1}";
    }

    protected override string LabelAddress(ChartDefinition definition, int series) =>
        $"local-table.{new CellAddress(0, series + 1).ToA1().TrimEnd('1')}1";

    protected override IEnumerable<XElement> Supplement(ChartData data, CultureInfo culture)
    {
        var table = new XElement(Table + "table", new XAttribute(Table + "name", "local-table"),
            new XElement(Table + "table-header-columns", new XElement(Table + "table-column")),
            new XElement(Table + "table-columns", new XElement(Table + "table-column",
                new XAttribute(Table + "number-columns-repeated", Math.Max(1, data.Series.Count)))),
            new XElement(Table + "table-header-rows",
                new XElement(Table + "table-row", TextCell(string.Empty), data.Series.Select(series => TextCell(series.Name)))));

        var rows = new XElement(Table + "table-rows");
        for (var row = 0; row < Rows(data); row++)
            rows.Add(new XElement(Table + "table-row",
                TextCell(row < data.Categories.Count ? data.Categories[row] : string.Empty),
                data.Series.Select(series => NumberCell(row < series.Values.Count ? series.Values[row] : double.NaN, culture))));
        table.Add(rows);

        yield return table;
    }

    private int Rows(ChartData data) => data.Series.Select(series => series.Values.Count).Append(data.Categories.Count).Max();

    private XElement NumberCell(double value, CultureInfo culture) => !double.IsFinite(value)
        ? new(Table + "table-cell")
        : new(Table + "table-cell",
            new XAttribute(Office + "value-type", "float"),
            new XAttribute(Office + "value", value.ToString(CultureInfo.InvariantCulture)),
            new XElement(Text + "p", value.ToString(culture)));

    private XElement TextCell(string text) =>
        new(Table + "table-cell", new XAttribute(Office + "value-type", "string"), new XElement(Text + "p", text));
}

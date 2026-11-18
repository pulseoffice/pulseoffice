using System.Globalization;
using DocumentFormat.OpenXml;
using PulseOffice.Charts;
using PulseOffice.Sheets.Addressing;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;

namespace PulseOffice.Formats.OpenXml;

internal interface ISeriesSource
{
    OpenXmlElement Name(int index, string name);

    OpenXmlElement Categories(ChartData data);

    OpenXmlElement Values(int index, IReadOnlyList<double> values);
}

internal sealed class LiteralSeriesSource : ISeriesSource
{
    public OpenXmlElement Name(int index, string name) => new C.SeriesText(new C.NumericValue { Text = name });

    private readonly OoxmlCategoryLevels _levels = new();

    public OpenXmlElement Categories(ChartData data) => new C.CategoryAxisData(_levels.Literal(data));

    public OpenXmlElement Values(int index, IReadOnlyList<double> values)
    {
        var literal = new C.NumberLiteral(new C.FormatCode("General"), new C.PointCount { Val = (uint)values.Count });
        for (var i = 0; i < values.Count; i++)
            if (double.IsFinite(values[i]))
                literal.Append(new C.NumericPoint { Index = (uint)i, NumericValue = new C.NumericValue(values[i].ToString(CultureInfo.InvariantCulture)) });
        return new C.Values(literal);
    }
}

internal sealed class SheetSeriesSource : ISeriesSource
{
    private readonly CellRange _range;
    private readonly ChartDefinition definition;
    private readonly LiteralSeriesSource _literals = new();
    private readonly PulseOffice.Sheets.Engine.Parsing.SheetNameQuoting _quoting = new();

    public SheetSeriesSource(ChartDefinition source)
    {
        _range = CellRange.TryParseA1(source.SourceRange, out var parsed) ? parsed : CellRange.Single(new CellAddress(0, 0));
        definition = new ChartLabelFit().Fit(source, _range);
    }

    private int DataTop => definition.FirstRowIsHeader ? _range.Start.Row + 1 : _range.Start.Row;

    private int DataLeft => definition.FirstColumnIsCategory ? _range.Start.Column + 1 : _range.Start.Column;

    public OpenXmlElement Name(int index, string name) =>
        definition.ExplicitSeriesName(index) is not null
            ? _literals.Name(index, name)
            : new C.SeriesText(new C.StringReference
        {
            Formula = new C.Formula(definition.SeriesInColumns
                ? Reference(_range.Start.Row, _range.Start.Row, DataLeft + index, DataLeft + index)
                : Reference(DataTop + index, DataTop + index, _range.Start.Column, _range.Start.Column)),
            StringCache = new C.StringCache(new C.PointCount { Val = 1u }, new C.StringPoint(new C.NumericValue(name)) { Index = 0u }),
        });

    private readonly OoxmlCategoryLevels _levels = new();

    public OpenXmlElement Categories(ChartData data)
    {
        if (!(definition.SeriesInColumns ? definition.FirstColumnIsCategory : definition.FirstRowIsHeader))
            return _literals.Categories(data);

        return _levels.Referenced(definition.SeriesInColumns
            ? Reference(DataTop, _range.End.Row, _range.Start.Column, _range.Start.Column)
            : Reference(_range.Start.Row, _range.Start.Row, DataLeft, _range.End.Column), data);
    }

    public OpenXmlElement Values(int index, IReadOnlyList<double> values)
    {
        var cache = new C.NumberingCache(new C.PointCount { Val = (uint)values.Count });
        for (var i = 0; i < values.Count; i++)
            if (double.IsFinite(values[i]))
                cache.AppendChild(new C.NumericPoint(new C.NumericValue(values[i].ToString(CultureInfo.InvariantCulture))) { Index = (uint)i });

        return new C.Values(new C.NumberReference
        {
            Formula = new C.Formula(definition.SeriesInColumns
                ? Reference(DataTop, _range.End.Row, DataLeft + index, DataLeft + index)
                : Reference(DataTop + index, DataTop + index, DataLeft, _range.End.Column)),
            NumberingCache = cache,
        });
    }

    private string Reference(int top, int bottom, int left, int right)
    {
        var sheet = _quoting.Quoted(definition.SourceSheet);
        return $"{sheet}!{new CellAddress(top, left).ToA1(true, true)}:{new CellAddress(bottom, right).ToA1(true, true)}";
    }
}

internal sealed class ChartPalette
{
    private readonly string[] _colours = ["4472C4", "ED7D31", "A5A5A5", "FFC000", "5B9BD5", "70AD47", "264478", "9E480E"];

    public C.ChartShapeProperties Fill(int index) =>
        new(new A.SolidFill(new A.RgbColorModelHex { Val = _colours[index % _colours.Length] }));

    public C.ChartShapeProperties Solid(string colour, bool line, int index)
    {
        var hex = colour.TrimStart('#').ToUpperInvariant();
        if (hex.Length != 6 || !hex.All(char.IsAsciiHexDigit)) hex = _colours[index % _colours.Length];
        return line
            ? new C.ChartShapeProperties(new A.Outline(new A.SolidFill(new A.RgbColorModelHex { Val = hex })) { Width = 28575 })
            : new C.ChartShapeProperties(new A.SolidFill(new A.RgbColorModelHex { Val = hex }));
    }

    public IEnumerable<C.DataPoint> Points(int count) =>
        Enumerable.Range(0, count).Select(i => new C.DataPoint(
            new C.Index { Val = (uint)i }, new C.Bubble3D { Val = false }, Fill(i)));
}

internal sealed class ChartSpaceBuilder(ChartFamilies families)
{
    private readonly ChartPalette _palette = new();

    private const uint CategoryAxis = 111111111u;
    private const uint ValueAxis = 222222222u;
    private const uint SecondaryCategoryAxis = 333333333u;
    private const uint SecondaryValueAxis = 444444444u;

    private readonly OoxmlAxisScales _scales = new();
    private readonly OoxmlDataLabels _labels = new();
    private readonly OoxmlSeriesFormats _formats = new();

    public C.ChartSpace Build(ChartDefinition definition, ChartData data, ISeriesSource source)
    {
        var plot = new C.PlotArea(new C.Layout());
        var axes = false;
        var secondaryAxes = false;
        var valueCategories = false;
        C.View3D? view = null;

        foreach (var (type, members, secondary) in Groups(definition, data.Series.Count))
        {
            var family = families.For(type);
            var solid = definition.ThreeDimensional && definition.Type != ChartType.ColumnLine && family.WritesThreeDimensional(type);
            var chart = family.Chart(type, solid);
            if (solid) view = family.View(type);

            foreach (var bundle in family.Bundles(members))
            {
                var index = family.Lead(bundle);
                var series = family.Series();
                series.AppendChild(new C.Index { Val = (uint)index });
                series.AppendChild(new C.Order { Val = (uint)index });
                series.AppendChild(source.Name(index, definition.ExplicitSeriesName(index) ?? data.Series[index].Name));
                series.AppendChild(family.ColoursPoints ? _palette.Fill(index) : _palette.Solid(definition.SeriesColor(index), family.DrawsLines(type) || definition.IsLineSeries(index, data.Series.Count), index));
                if (family.ColoursPoints) series.Append(_palette.Points(data.Categories.Count));
                if (definition.ShowDataLabels) series.AppendChild(_labels.Write(definition.DataLabels, series));
                series.Append(family.Data(source, data, bundle));
                family.Finish(series, definition with { Type = type });
                _formats.Apply(series, definition.SeriesFormat(index));
                chart.AppendChild(series);
            }

            family.Close(chart, type, solid);
            if (family.HasAxes)
            {
                chart.AppendChild(new C.AxisId { Val = secondary ? SecondaryCategoryAxis : CategoryAxis });
                chart.AppendChild(new C.AxisId { Val = secondary ? SecondaryValueAxis : ValueAxis });
                axes = true;
                secondaryAxes |= secondary;
                valueCategories |= family.ValueCategoryAxis;
            }
            plot.AppendChild(chart);
        }

        if (axes) plot.Append(Axes(definition, valueCategories, secondaryAxes));

        var root = new C.Chart();
        if (definition.ChartTitle is { Length: > 0 } title) root.AppendChild(Title(title));
        root.AppendChild(new C.AutoTitleDeleted { Val = string.IsNullOrEmpty(definition.ChartTitle) });
        if (view is not null) root.AppendChild(view);
        root.AppendChild(plot);
        if (definition.Legend != LegendPosition.None)
            root.AppendChild(new C.Legend(new C.LegendPosition { Val = Legend(definition.Legend) }, new C.Overlay { Val = false }));
        root.AppendChild(new C.PlotVisibleOnly { Val = true });
        root.AppendChild(new C.DisplayBlanksAs { Val = C.DisplayBlanksAsValues.Gap });

        var space = new C.ChartSpace(new C.EditingLanguage { Val = "en-US" }, root);
        if (families.Declares(definition.Type)) space.AppendChild(_declared.Extension(definition.Type));
        return space;
    }

    private readonly DeclaredChartType _declared = new();

    private IEnumerable<(ChartType Type, IReadOnlyList<int> Series, bool Secondary)> Groups(ChartDefinition definition, int count)
    {
        var all = Enumerable.Range(0, count).ToList();
        IReadOnlyList<(ChartType Type, IReadOnlyList<int> Series)> groups = definition.Type != ChartType.ColumnLine
            ? [(definition.Type, all)]
            :
            [
                (ChartType.Column, [.. all.Where(index => !definition.IsLineSeries(index, count))]),
                (ChartType.Line, [.. all.Where(index => definition.IsLineSeries(index, count))]),
            ];

        foreach (var (type, members) in groups)
        {
            var family = families.For(type);
            if (!family.HasAxes || family is BubbleFamily || !definition.HasSecondaryAxis)
            {
                yield return (type, members, false);
                continue;
            }
            List<int> primary = [.. members.Where(index => !definition.SeriesFormat(index).SecondaryAxis)];
            List<int> secondary = [.. members.Where(index => definition.SeriesFormat(index).SecondaryAxis)];
            if (primary.Count > 0 || secondary.Count == 0) yield return (type, primary, false);
            if (secondary.Count > 0) yield return (type, secondary, true);
        }
    }

    private IEnumerable<OpenXmlElement> Axes(ChartDefinition definition, bool valueCategories, bool secondary)
    {
        yield return Across(CategoryAxis, ValueAxis, valueCategories, definition.CategoryAxis, definition.ValueAxis, definition.CategoryAxisTitle, false);
        yield return Upright(ValueAxis, CategoryAxis, definition.ValueAxis, definition.CategoryAxis, definition.ValueAxisTitle, definition.ShowGridLines, false);
        if (!secondary) yield break;
        yield return Across(SecondaryCategoryAxis, SecondaryValueAxis, valueCategories, ChartAxisScale.Automatic, definition.SecondaryValueAxis, null, true);
        yield return Upright(SecondaryValueAxis, SecondaryCategoryAxis, definition.SecondaryValueAxis, ChartAxisScale.Automatic with { Crossing = AxisCrossing.Maximum }, null, false, true);
    }

    private OpenXmlCompositeElement Across(uint id, uint crossing, bool valueCategories, ChartAxisScale own, ChartAxisScale crossed, string? title, bool deleted)
    {
        OpenXmlCompositeElement category = valueCategories ? new C.ValueAxis() : new C.CategoryAxis();
        category.Append(
            new C.AxisId { Val = id },
            _scales.Scaling(own),
            new C.Delete { Val = deleted },
            new C.AxisPosition { Val = C.AxisPositionValues.Bottom });
        if (title is { Length: > 0 }) category.AppendChild(Title(title));
        category.Append(_scales.NumberFormat(own));
        category.AppendChild(new C.CrossingAxis { Val = crossing });
        category.AppendChild(_scales.Crossing(crossed));
        if (valueCategories) category.Append(_scales.Units(own));
        return category;
    }

    private C.ValueAxis Upright(uint id, uint crossing, ChartAxisScale own, ChartAxisScale crossed, string? title, bool grid, bool right)
    {
        var value = new C.ValueAxis(
            new C.AxisId { Val = id },
            _scales.Scaling(own),
            new C.Delete { Val = false },
            new C.AxisPosition { Val = right ? C.AxisPositionValues.Right : C.AxisPositionValues.Left });
        if (grid) value.AppendChild(new C.MajorGridlines());
        if (title is { Length: > 0 }) value.AppendChild(Title(title));
        value.Append(_scales.NumberFormat(own));
        value.AppendChild(new C.CrossingAxis { Val = crossing });
        value.AppendChild(_scales.Crossing(crossed));
        value.Append(_scales.Units(own));
        return value;
    }

    private C.Title Title(string text) =>
        new(new C.ChartText(new C.RichText([new A.BodyProperties(), new A.ListStyle(),
                .. text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(line => new A.Paragraph(new A.Run(new A.Text(line))))])),
            new C.Overlay { Val = false });

    private C.LegendPositionValues Legend(LegendPosition position) => position switch
    {
        LegendPosition.Bottom => C.LegendPositionValues.Bottom,
        LegendPosition.Top => C.LegendPositionValues.Top,
        LegendPosition.Left => C.LegendPositionValues.Left,
        _ => C.LegendPositionValues.Right,
    };
}

internal sealed record ChartSpaceContent(
    ChartType Type, IReadOnlyList<OpenXmlCompositeElement> Series, string? Title, LegendPosition Legend)
{
    public string? CategoryAxisTitle { get; init; }

    public string? ValueAxisTitle { get; init; }

    public bool ShowGridLines { get; init; }

    public bool ShowDataLabels { get; init; }

    public int LineSeriesCount { get; init; } = 1;

    public IReadOnlyList<string>? SeriesNames { get; init; }

    public IReadOnlyList<string?>? SeriesColors { get; init; }

    public bool ThreeDimensional { get; init; }

    public ChartDataLabelOptions DataLabels { get; init; } = ChartDataLabelOptions.ValuesOnly;

    public ChartAxisScale CategoryAxis { get; init; } = ChartAxisScale.Automatic;

    public ChartAxisScale ValueAxis { get; init; } = ChartAxisScale.Automatic;

    public ChartAxisScale SecondaryValueAxis { get; init; } = ChartAxisScale.Automatic;

    public IReadOnlyList<ChartSeriesFormat?>? SeriesFormats { get; init; }

    public ChartDefinition Decorate(ChartDefinition definition) => definition with
    {
        LineSeriesCount = LineSeriesCount,
        ChartTitle = Title,
        Legend = Legend,
        CategoryAxisTitle = CategoryAxisTitle,
        ValueAxisTitle = ValueAxisTitle,
        ShowGridLines = ShowGridLines,
        ShowDataLabels = ShowDataLabels,
        SeriesNames = SeriesNames,
        SeriesColors = SeriesColors,
        ThreeDimensional = ThreeDimensional,
        DataLabels = DataLabels,
        CategoryAxis = CategoryAxis,
        ValueAxis = ValueAxis,
        SecondaryValueAxis = SecondaryValueAxis,
        SeriesFormats = SeriesFormats,
    };
}

internal sealed class ModelSeriesSlots
{
    public IReadOnlyList<OpenXmlCompositeElement?> Of(IReadOnlyList<OpenXmlCompositeElement> series) =>
        [.. series.SelectMany(element => element is C.BubbleChartSeries ? [null, element, null] : new OpenXmlCompositeElement?[] { element })];
}

internal sealed class ExplicitSeriesColours
{
    public IReadOnlyList<string?>? Explicit(IReadOnlyList<OpenXmlCompositeElement?> slots)
    {
        if (slots.Any(element => element?.Elements<C.DataPoint>().Any() == true)) return null;
        var colours = slots.Select(Colour).ToList();
        return colours.Any(colour => colour is not null) ? colours : null;
    }

    private string? Colour(OpenXmlCompositeElement? series)
    {
        var properties = series?.GetFirstChild<C.ChartShapeProperties>();
        if (properties is null) return null;
        var line = series is C.LineChartSeries or C.ScatterChartSeries;
        var fill = line
            ? properties.GetFirstChild<A.Outline>()?.GetFirstChild<A.SolidFill>()
            : properties.GetFirstChild<A.SolidFill>();
        return fill?.GetFirstChild<A.RgbColorModelHex>()?.Val?.Value is { Length: 6 } hex ? "#" + hex.ToUpperInvariant() : null;
    }
}

internal sealed class ExplicitSeriesNames
{
    public IReadOnlyList<string>? Explicit(IReadOnlyList<OpenXmlCompositeElement?> slots)
    {
        var texts = slots.Select(element => element?.GetFirstChild<C.SeriesText>()).ToList();
        return texts.Any(Explicit) ? [.. texts.Select(Cached)] : null;
    }

    private bool Explicit(C.SeriesText? text)
    {
        if (text is null) return false;
        if (text.GetFirstChild<C.StringReference>() is not { } reference) return text.GetFirstChild<C.NumericValue>() is not null;

        var formula = reference.Formula?.Text ?? string.Empty;
        var address = formula[(formula.LastIndexOf('!') + 1)..].Replace("$", string.Empty, StringComparison.Ordinal);
        return !CellRange.TryParseA1(address, out var range) || !range.IsSingleCell;
    }

    private string Cached(C.SeriesText? text) =>
        text is null ? string.Empty : string.Join(" ", text.Descendants<C.NumericValue>().Select(value => value.Text).Where(value => value.Length > 0));
}

internal sealed class DeclaredChartType
{
    private const string Uri = "{6D5B6F0C-3C1E-4C4B-9E6A-50A1C3E5F0C1}";
    private const string Namespace = "urn:pulseoffice:chart";

    public C.ChartSpaceExtensionList Extension(ChartType type)
    {
        var marker = new OpenXmlUnknownElement("pulseoffice", "chartType", Namespace);
        marker.SetAttribute(new OpenXmlAttribute("val", string.Empty, type.ToString()));
        return new C.ChartSpaceExtensionList(new C.ChartSpaceExtension(marker) { Uri = Uri });
    }

    public ChartType? Read(C.ChartSpace space) =>
        space.GetFirstChild<C.ChartSpaceExtensionList>()?.Elements<C.ChartSpaceExtension>()
            .Where(extension => extension.Uri?.Value == Uri)
            .SelectMany(extension => extension.ChildElements)
            .Where(element => element.LocalName == "chartType" && element.NamespaceUri == Namespace)
            .Select(element => element.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == "val").Value)
            .Select(value => Enum.TryParse<ChartType>(value, out var type) && Enum.IsDefined(type) ? type : (ChartType?)null)
            .FirstOrDefault(type => type is not null);
}

internal sealed class ChartSpaceReader(ChartFamilies families, PulseOffice.Formats.OpenXml.DrawingML.IDrawingPlainText plainText)
{
    private readonly DeclaredChartType _declared = new();

    private readonly ExplicitSeriesNames _names = new();
    private readonly ExplicitSeriesColours _colours = new();
    private readonly ModelSeriesSlots _slots = new();
    private readonly OoxmlAxisScales _scales = new();
    private readonly OoxmlDataLabels _labels = new();
    private readonly OoxmlSeriesFormats _formats = new();

    private const uint MaximumPoints = 100_000;
    private const int MaximumTotalPoints = 1_000_000;

    public ChartSpaceContent? Read(C.ChartSpace space)
    {
        if (space.Descendants<C.PlotArea>().FirstOrDefault() is not { } plot) return null;
        if (families.Read(plot) is not { } read) return null;
        var found = _declared.Read(space) is { } declared && families.Refines(declared, read.Type)
            ? read with { Type = declared }
            : read;
        var threeDimensional = found.ThreeDimensional;
        found = found with { Series = Gathered(plot, found.Series) };
        var primaryAxes = PrimaryAxes(plot);

        var legend = space.Descendants<C.Legend>().Any()
            ? Legend(space.Descendants<C.LegendPosition>().FirstOrDefault()?.Val?.Value)
            : LegendPosition.None;
        var chart = space.GetFirstChild<C.Chart>();
        var title = TextOf(chart?.GetFirstChild<C.Title>());
        var valueCategories = found.Type is ChartType.Scatter or ChartType.ScatterWithLines or ChartType.Bubble;
        var valueAxis = Axis<C.ValueAxis>(plot, primaryAxes, valueCategories ? 1 : 0) ?? (valueCategories ? plot.Elements<C.ValueAxis>().Skip(1).FirstOrDefault() : plot.GetFirstChild<C.ValueAxis>());
        OpenXmlCompositeElement? categoryAxis = valueCategories
            ? Axis<C.ValueAxis>(plot, primaryAxes, 0) ?? plot.Elements<C.ValueAxis>().FirstOrDefault()
            : (OpenXmlCompositeElement?)plot.GetFirstChild<C.CategoryAxis>() ?? plot.GetFirstChild<C.DateAxis>();
        var secondaryAxes = SecondaryAxes(plot, primaryAxes);
        var secondaryValue = secondaryAxes.Count == 0 ? null
            : plot.Elements<C.ValueAxis>().Where(axis => secondaryAxes.Contains(axis.GetFirstChild<C.AxisId>()?.Val?.Value ?? 0))
                .OrderBy(axis => axis.GetFirstChild<C.AxisPosition>()?.Val?.Value == C.AxisPositionValues.Left || axis.GetFirstChild<C.AxisPosition>()?.Val?.Value == C.AxisPositionValues.Right ? 0 : 1)
                .FirstOrDefault();
        var labelOwner = found.Series.Select(series => series.GetFirstChild<C.DataLabels>()).FirstOrDefault(labels => labels is not null)
            ?? (found.Series.FirstOrDefault()?.Parent as OpenXmlCompositeElement)?.GetFirstChild<C.DataLabels>();
        var formats = _slots.Of(found.Series).Select(series => series is null ? null : _formats.Read(series, IsSecondary(series, primaryAxes))).ToList();
        return new ChartSpaceContent(found.Type, found.Series, title, legend)
        {
            ThreeDimensional = threeDimensional,
            LineSeriesCount = found.Type == ChartType.ColumnLine ? Math.Max(1, plot.Elements<C.LineChart>().Sum(line => line.Elements<C.LineChartSeries>().Count())) : 1,
            CategoryAxisTitle = TextOf(plot.GetFirstChild<C.CategoryAxis>()?.GetFirstChild<C.Title>() ?? plot.GetFirstChild<C.DateAxis>()?.GetFirstChild<C.Title>()
                ?? (found.Type is ChartType.Scatter or ChartType.ScatterWithLines or ChartType.Bubble ? plot.Elements<C.ValueAxis>().FirstOrDefault()?.GetFirstChild<C.Title>() : null)),
            ValueAxisTitle = TextOf(valueAxis?.GetFirstChild<C.Title>()),
            ShowGridLines = valueAxis?.GetFirstChild<C.MajorGridlines>() is not null,
            SeriesNames = _names.Explicit(_slots.Of(found.Series)),
            SeriesColors = _colours.Explicit(_slots.Of(found.Series)),
            ShowDataLabels = found.Series.Append(found.Series.FirstOrDefault()?.Parent as OpenXmlCompositeElement)
                .Any(owner => _labels.Shown(owner?.GetFirstChild<C.DataLabels>())),
            DataLabels = _labels.Read(labelOwner),
            CategoryAxis = _scales.Read(categoryAxis, plot),
            ValueAxis = _scales.Read(valueAxis, plot),
            SecondaryValueAxis = _scales.Read(secondaryValue, plot),
            SeriesFormats = formats.Any(format => format is { IsPlain: false }) ? [.. formats.Select(format => format is { IsPlain: false } ? format : null)] : null,
        };
    }

    private IReadOnlyList<OpenXmlCompositeElement> Gathered(C.PlotArea plot, IReadOnlyList<OpenXmlCompositeElement> series)
    {
        var groups = series.Select(element => element.Parent).OfType<OpenXmlCompositeElement>().Distinct().ToList();
        var kinds = groups.Select(group => group.GetType()).ToHashSet();
        var extra = plot.ChildElements.OfType<OpenXmlCompositeElement>().Where(group => kinds.Contains(group.GetType()) && !groups.Contains(group)).ToList();
        if (extra.Count == 0) return series;
        var seriesKinds = series.Select(element => element.GetType()).ToHashSet();
        return [.. series.Concat(extra.SelectMany(group => group.ChildElements.OfType<OpenXmlCompositeElement>().Where(child => seriesKinds.Contains(child.GetType()))))
            .OrderBy(element => element.GetFirstChild<C.Index>()?.Val?.Value ?? uint.MaxValue)];
    }

    private IReadOnlyList<uint> PrimaryAxes(C.PlotArea plot) =>
        plot.ChildElements.OfType<OpenXmlCompositeElement>()
            .Select(group => group.Elements<C.AxisId>().Select(id => id.Val?.Value ?? 0).ToList())
            .FirstOrDefault(ids => ids.Count > 0) ?? new List<uint>();

    private HashSet<uint> SecondaryAxes(C.PlotArea plot, IReadOnlyList<uint> primary) =>
        [.. plot.ChildElements.OfType<OpenXmlCompositeElement>()
            .Where(group => group.Elements<C.AxisId>().Any() && !group.Elements<C.AxisId>().Select(id => id.Val?.Value ?? 0).SequenceEqual(primary))
            .SelectMany(group => group.Elements<C.AxisId>().Select(id => id.Val?.Value ?? 0))
            .Where(id => !primary.Contains(id))];

    private bool IsSecondary(OpenXmlCompositeElement series, IReadOnlyList<uint> primary) =>
        series.Parent is OpenXmlCompositeElement group && group.Elements<C.AxisId>().Any()
            && !group.Elements<C.AxisId>().Select(id => id.Val?.Value ?? 0).SequenceEqual(primary);

    private T? Axis<T>(C.PlotArea plot, IReadOnlyList<uint> ids, int position) where T : OpenXmlCompositeElement =>
        position < ids.Count ? plot.Elements<T>().FirstOrDefault(axis => axis.GetFirstChild<C.AxisId>()?.Val?.Value == ids[position]) : null;

    private string? TextOf(C.Title? title)
    {
        if (title is null) return null;
        var text = title.GetFirstChild<C.ChartText>()?.GetFirstChild<C.RichText>() is { } rich ? plainText.Body(rich) : string.Empty;
        if (text.Length == 0 && title.GetFirstChild<C.ChartText>()?.GetFirstChild<C.StringReference>() is { } reference)
            text = string.Concat(reference.Descendants<C.StringPoint>().Select(point => point.NumericValue?.Text));
        return text.Length > 0 ? text : null;
    }

    public ChartData Literals(IReadOnlyList<OpenXmlCompositeElement> series)
    {
        var budget = new PointBudget(MaximumTotalPoints);
        var categories = new List<string>();
        OpenXmlElement? levelled = null;
        var read = new List<ChartSeries>();
        foreach (var element in series)
        {
            OpenXmlElement? axis = (OpenXmlElement?)element.GetFirstChild<C.CategoryAxisData>() ?? element.GetFirstChild<C.XValues>();
            var names = Indexed(axis, axis is null ? null : (axis.Descendants<C.MultiLevelStringCache>().Any() ? _categoryLevels.Leaves(axis) : axis.Descendants<OpenXmlCompositeElement>())
                .Where(point => point is C.StringPoint or C.NumericPoint)
                .Select(point => (point.GetAttributes().Where(attribute => attribute.LocalName == "idx")
                        .Select(attribute => uint.TryParse(attribute.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var i) ? i : (uint?)null).FirstOrDefault(),
                    point.GetFirstChild<C.NumericValue>()?.Text ?? string.Empty)), string.Empty, budget);
            if (names.Count > categories.Count)
            {
                categories = names;
                levelled = axis;
            }

            OpenXmlElement? numbers = (OpenXmlElement?)element.GetFirstChild<C.Values>() ?? element.GetFirstChild<C.YValues>();
            var values = Indexed(numbers, numbers?.Descendants<C.NumericPoint>()
                .Select(point => (point.Index?.Value,
                    double.TryParse(point.NumericValue?.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN)), double.NaN, budget);
            var name = element.GetFirstChild<C.SeriesText>()?.Descendants<C.NumericValue>().FirstOrDefault()?.Text ?? $"Series {read.Count + 1}";
            if (element is C.BubbleChartSeries)
            {
                read.Add(new ChartSeries("X", Numbers(element.GetFirstChild<C.XValues>(), budget)));
                read.Add(new ChartSeries(name, values));
                read.Add(new ChartSeries("Size", Numbers(element.GetFirstChild<C.BubbleSize>(), budget)));
            }
            else read.Add(new ChartSeries(name, values));
        }
        return new ChartData(categories, read).WithCategoryLevels(_categoryLevels.Read(levelled, categories.Count));
    }

    private readonly OoxmlCategoryLevels _categoryLevels = new();

    private List<double> Numbers(OpenXmlElement? numbers, PointBudget budget) =>
        Indexed(numbers, numbers?.Descendants<C.NumericPoint>()
            .Select(point => (point.Index?.Value,
                double.TryParse(point.NumericValue?.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN)), double.NaN, budget);

    private List<T> Indexed<T>(OpenXmlElement? container, IEnumerable<(uint? Index, T Value)>? points, T missing, PointBudget budget)
    {
        var list = new List<T>();
        if (points is null) return list;

        var limit = Math.Min(MaximumPoints, (uint)budget.Remaining);
        var declared = Math.Min(container?.Descendants<C.PointCount>().FirstOrDefault()?.Val?.Value ?? 0, limit);
        foreach (var (index, value) in points)
        {
            var wanted = index ?? (uint)list.Count;
            if (wanted >= limit) continue;
            var position = (int)wanted;
            while (list.Count <= position) list.Add(missing);
            list[position] = value;
        }
        while (list.Count < declared) list.Add(missing);
        budget.Spend(list.Count);
        return list;
    }

    private LegendPosition Legend(C.LegendPositionValues? value)
    {
        if (value == C.LegendPositionValues.Bottom) return LegendPosition.Bottom;
        if (value == C.LegendPositionValues.Top) return LegendPosition.Top;
        if (value == C.LegendPositionValues.Left) return LegendPosition.Left;
        return LegendPosition.Right;
    }
}

internal sealed class PointBudget(int total)
{
    public int Remaining { get; private set; } = total;

    public void Spend(int points) => Remaining = Math.Max(0, Remaining - points);
}

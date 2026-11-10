using System.Xml.Linq;
using PulseOffice.Document.Embedding;
using PulseOffice.Formats.Odf.TextCodecs;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf;

internal sealed class CellAlignTable() : AutomaticStyleTable<CellVerticalAlign?>("table-cell")
{
    protected override CellVerticalAlign? Describe(XElement style, XElement automatic) =>
        (string?)style.Element(Style + "table-cell-properties")?.Attribute(Style + "vertical-align") switch
        {
            "middle" => CellVerticalAlign.Center,
            "bottom" => CellVerticalAlign.Bottom,
            _ => null,
        };
}

internal sealed class ColumnWidthTable(OdfValues values) : AutomaticStyleTable<double?>("table-column")
{
    protected override double? Describe(XElement style, XElement automatic) =>
        (string?)style.Element(Style + "table-column-properties")?.Attribute(Style + "column-width") is { } width
            && values.Length(width) is > 0 and var points ? points : null;
}

internal sealed class RowHeightTable(OdfValues values) : AutomaticStyleTable<double?>("table-row")
{
    protected override double? Describe(XElement style, XElement automatic)
    {
        var properties = style.Element(Style + "table-row-properties");
        var height = (string?)properties?.Attribute(Style + "row-height")
            ?? (string?)properties?.Attribute(Style + "min-row-height");
        return height is not null && values.Length(height) is > 0 and var points ? points : null;
    }
}

internal sealed class PageBackgroundTable(OdfValues values) : AutomaticStyleTable<uint?>("drawing-page")
{
    protected override uint? Describe(XElement style, XElement automatic)
    {
        var properties = style.Element(Style + "drawing-page-properties");
        if ((string?)properties?.Attribute(Draw + "fill") is { } fill && fill != "solid") return null;
        if (values.Color((string?)properties?.Attribute(Draw + "fill-color")) is not { } colour) return null;
        if ((string?)properties!.Attribute(Draw + "opacity") is not { Length: > 1 } opacity || !opacity.EndsWith('%')
            || !double.TryParse(opacity[..^1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var percent)) return colour;
        var alpha = (uint)Math.Round(Math.Clamp(percent, 0, 100) * 2.55);
        return (alpha << 24) | (colour & 0x00FFFFFFu);
    }
}

internal sealed class OdfPageFill(OdfShapeStyleReader reader)
{
    private static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;

    public PulseOffice.Drawing.Shapes.ShapeFill? Read(XElement? properties, IReadOnlyDictionary<string, XElement> definitions) =>
        properties is not null && (string?)properties.Attribute(Draw + "fill") is "gradient" or "hatch"
            ? reader.ReadFill(properties, definitions)
            : null;
}

internal sealed class PageFillTable(OdfPageFill fills, IReadOnlyDictionary<string, XElement> definitions)
    : AutomaticStyleTable<PulseOffice.Drawing.Shapes.ShapeFill>("drawing-page")
{
    protected override PulseOffice.Drawing.Shapes.ShapeFill? Describe(XElement style, XElement automatic) =>
        fills.Read(style.Element(Style + "drawing-page-properties"), definitions);
}

internal sealed class PageImageLayoutTable(OdfValues values) : AutomaticStyleTable<BackgroundImageLayout>("drawing-page")
{
    protected override BackgroundImageLayout? Describe(XElement style, XElement automatic)
    {
        var properties = style.Element(Style + "drawing-page-properties");
        if ((string?)properties?.Attribute(Draw + "fill") != "bitmap") return null;
        var mode = (string?)properties.Attribute(Style + "repeat") switch
        {
            "stretch" => BackgroundImageMode.Stretch,
            "no-repeat" => BackgroundImageMode.Natural,
            _ => BackgroundImageMode.Tile,
        };
        return new BackgroundImageLayout(mode,
            values.Measure((string?)properties.Attribute(Draw + "fill-image-width")) ?? PulseOffice.Foundation.Units.Twip.Zero,
            values.Measure((string?)properties.Attribute(Draw + "fill-image-height")) ?? PulseOffice.Foundation.Units.Twip.Zero)
        {
            Opacity = (string?)properties.Attribute(Draw + "opacity") is { Length: > 1 } opacity && opacity.EndsWith('%')
                && double.TryParse(opacity[..^1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var percent)
                ? Math.Clamp(percent / 100, 0, 1) : 1,
        };
    }
}

internal sealed class PageImageTable(XElement? styles) : AutomaticStyleTable<string>("drawing-page")
{
    protected override string? Describe(XElement style, XElement automatic)
    {
        var properties = style.Element(Style + "drawing-page-properties");
        if ((string?)properties?.Attribute(Draw + "fill") != "bitmap") return null;
        var fillName = (string?)properties.Attribute(Draw + "fill-image-name");

        return automatic.Elements(Draw + "fill-image")
            .Concat(styles?.Element(Office + "styles")?.Elements(Draw + "fill-image") ?? [])
            .FirstOrDefault(image => (string?)image.Attribute(Draw + "name") == fillName)
            ?.Attribute(OdfPackage.Namespaces.Xlink + "href")?.Value;
    }
}

internal sealed class PageVisibilityTable() : AutomaticStyleTable<string>("drawing-page")
{
    protected override string? Describe(XElement style, XElement automatic)
    {
        if (style.Element(Style + "drawing-page-properties") is not { } properties) return null;
        var flags = new List<string>();
        if ((string?)properties.Attribute(OdfPackage.Namespaces.Presentation + "visibility") == "hidden") flags.Add("hidden");
        if ((string?)properties.Attribute(OdfPackage.Namespaces.Presentation + "background-visible") == "false") flags.Add("noBackground");
        if ((string?)properties.Attribute(OdfPackage.Namespaces.Presentation + "background-objects-visible") == "false") flags.Add("noObjects");
        return flags.Count == 0 ? null : string.Join(' ', flags);
    }
}

internal sealed class PageTransitionTable(OdfValues values, SmilTransitionMapper transitions)
    : AutomaticStyleTable<SlideTransition>("drawing-page")
{
    private static readonly XNamespace Smil = OdfPackage.Namespaces.Smil;
    private static readonly XNamespace Presentation = OdfPackage.Namespaces.Presentation;

    protected override SlideTransition? Describe(XElement style, XElement automatic)
    {
        var properties = style.Element(Style + "drawing-page-properties");
        if (properties is null) return null;
        var sound = properties.Element(Presentation + "sound");
        var advancing = (string?)properties.Attribute(Presentation + "transition-type") == "automatic";
        var type = (string?)properties.Attribute(Smil + "type");
        if (type is null && !advancing && sound is null) return null;

        var native = type is null
            ? SlideTransition.None
            : transitions.WithExtensionKind(
                transitions.FromSmil(type, (string?)properties.Attribute(Smil + "subtype") ?? string.Empty, (string?)properties.Attribute(Smil + "direction") == "reverse"),
                (string?)properties.Attribute(transitions.ExtensionNamespace + "kind"));

        return native with
        {
            DurationMs = values.Seconds((string?)properties.Attribute(Smil + "dur"),
                (string?)properties.Attribute(Presentation + "transition-speed") switch
                {
                    "slow" => 2000,
                    "fast" => 500,
                    _ => 1000,
                }),
            Advance = !advancing ? AdvanceMode.OnClick
                : (string?)properties.Attribute(transitions.ExtensionNamespace + "advance-on-click") == "true" ? AdvanceMode.OnClickOrAfterTime : AdvanceMode.AfterTime,
            AdvanceAfterMs = values.XmlDuration((string?)properties.Attribute(Presentation + "duration")),
            SoundPath = (string?)sound?.Attribute(OdfPackage.Namespaces.Xlink + "href"),
            LoopSoundUntilNext = (string?)sound?.Attribute(Presentation + "play-full") == "false",
        };
    }
}

internal sealed class DrawingDefinitionTable
{
    private static readonly XNamespace Office = OdfPackage.Namespaces.Office;
    private static readonly XNamespace Style = OdfPackage.Namespaces.Style;
    private static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;

    private readonly HashSet<string> _definitionKinds = ["gradient", "hatch", "fill-image", "opacity", "stroke-dash", "marker"];

    private readonly OdfGraphicStyleInheritance _inheritance = new();

    private bool IsDefinition(XElement element) => element.Name.Namespace == Draw && _definitionKinds.Contains(element.Name.LocalName);

    public IReadOnlyDictionary<string, XElement> Read(XElement root, XElement? styles)
    {
        var result = new Dictionary<string, XElement>(StringComparer.Ordinal);

        foreach (var definition in styles?.Element(Office + "styles")?.Elements() ?? [])
        {
            if (IsDefinition(definition) && (string?)definition.Attribute(Draw + "name") is { } shared)
                result[shared] = definition;
            else if (definition.Name == Style + "style" && (string?)definition.Attribute(Style + "family") == "graphic"
                && (string?)definition.Attribute(Style + "name") is { } named)
                result[_inheritance.Key(named)] = definition;
        }

        if (root.Element(Office + "automatic-styles") is not { } automatic) return result;

        foreach (var style in automatic.Elements(Style + "style")
            .Where(s => (string?)s.Attribute(Style + "family") == "graphic"))
            if ((string?)style.Attribute(Style + "name") is { } name) result[name] = style;

        foreach (var style in automatic.Elements(Style + "style")
            .Where(s => (string?)s.Attribute(Style + "family") == "text"))
            if ((string?)style.Attribute(Style + "name") is { } name) result.TryAdd(name, style);

        foreach (var definition in automatic.Elements().Where(IsDefinition))
            if ((string?)definition.Attribute(Draw + "name") is { } name) result[name] = definition;

        foreach (var list in automatic.Elements(OdfPackage.Namespaces.Text + "list-style"))
            if ((string?)list.Attribute(Style + "name") is { } name) result.TryAdd(OdfShapeTextLists.KeyPrefix + name, list);

        return result;
    }
}

internal sealed record OdpStyleSheet(
    IReadOnlyDictionary<string, ParagraphProperties> Paragraphs,
    IReadOnlyDictionary<string, SpanProperties> Spans,
    IReadOnlyDictionary<string, XElement> Drawings,
    OdfTableStyles Tables,
    IReadOnlyDictionary<string, uint> PageBackgrounds,
    IReadOnlyDictionary<string, string> PageImages,
    IReadOnlyDictionary<string, BackgroundImageLayout> PageImageLayouts,
    IReadOnlyDictionary<string, PulseOffice.Drawing.Shapes.ShapeFill> PageFills,
    IReadOnlyDictionary<string, string> HiddenPages,
    IReadOnlyDictionary<string, SlideTransition> PageTransitions,
    IReadOnlyDictionary<string, XElement> Lists,
    OdfTextFrameStyles Frames,
    OdfPresentationTextStyles Presentation,
    IReadOnlyDictionary<string, string> DataStyles)
{
    public OdfTextStyles Text { get; } = new(Paragraphs, Spans, Lists, DataStyles);
}

internal sealed class OdfPresentationTextStyles(
    IReadOnlyDictionary<string, XElement> automatic,
    IReadOnlyDictionary<string, XElement> common,
    OdfParagraphPropertiesReader reader) : OdfXml
{
    private const int DeepestParent = 12;
    private const int OutlineLevels = 9;
    private const string FirstOutline = "outline1";

    public OdfParagraphLevels Levels(XElement frame)
    {
        if ((string?)frame.Attribute(Presentation + "style-name") is not { } name) return OdfParagraphLevels.None;

        var chain = Chain(name).ToList();
        if (chain.Count == 0) return OdfParagraphLevels.None;

        var first = Merge(chain);
        var list = chain.Select(style => style.Element(Style + "graphic-properties")?.Element(Text + "list-style")).FirstOrDefault(found => found is not null);
        var outline = chain.Select(style => (string?)style.Attribute(Style + "name"))
            .FirstOrDefault(styleName => styleName is not null && styleName.EndsWith(FirstOutline, StringComparison.Ordinal));
        if (outline is null) return new OdfParagraphLevels([first], list);

        var prefix = outline[..^1];
        var levels = new List<ParagraphProperties> { first };
        for (var level = 2; level <= OutlineLevels; level++)
        {
            var deeper = Chain(prefix + level.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();
            levels.Add(deeper.Count == 0 ? levels[^1] : Merge(deeper));
        }
        return new OdfParagraphLevels(levels, list);
    }

    private ParagraphProperties Merge(IEnumerable<XElement> chain) =>
        chain.Aggregate(ParagraphProperties.Empty, (merged, style) => merged.Over(reader.Read(style)));

    private IEnumerable<XElement> Chain(string? name)
    {
        var scope = automatic;
        for (var depth = 0; name is not null && depth < DeepestParent; depth++)
        {
            if (!scope.TryGetValue(name, out var style) && !common.TryGetValue(name, out style)) yield break;
            yield return style;
            name = (string?)style.Attribute(Style + "parent-style-name");
            scope = common;
        }
    }
}

internal sealed class OdpStyleSheetBuilder(OdfValues values, SmilTransitionMapper transitions, OdfPageFill fills, IOdfDateStyleCodec dates)
{
    private readonly OdfTextFrameReader _frames = new(values, new OdfTextAnchors());

    public OdpStyleSheet Build(XElement content, XElement? styles) => Build(content, styles, new DrawingDefinitionTable().Read(content, styles));

    private OdpStyleSheet Build(XElement content, XElement? styles, IReadOnlyDictionary<string, XElement> drawings) => new(
        new ParagraphStyleTable(new OdfParagraphPropertiesReader(values, _frames)).Read(content),
        new TextStyleTable(values, new OdfLanguageCodec(), new OdfFontFaceNames()).Read(content),
        drawings,
        new OdfTableStyles(
            Present(new CellAlignTable().Read(content)),
            Present(new ColumnWidthTable(values).Read(content)),
            Present(new RowHeightTable(values).Read(content)),
            content.Descendants(OdfPackage.Namespaces.Style + "style")
                .Where(style => (string?)style.Attribute(OdfPackage.Namespaces.Style + "family") is "graphic" or "presentation")
                .GroupBy(style => (string?)style.Attribute(OdfPackage.Namespaces.Style + "name") ?? string.Empty)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal),
            content.Descendants(OdfPackage.Namespaces.Style + "style")
                .Where(style => (string?)style.Attribute(OdfPackage.Namespaces.Style + "family") == "table-cell")
                .GroupBy(style => (string?)style.Attribute(OdfPackage.Namespaces.Style + "name") ?? string.Empty)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal)),
        Present(new PageBackgroundTable(values).Read(content)),
        new PageImageTable(styles).Read(content),
        new PageImageLayoutTable(values).Read(content),
        new PageFillTable(fills, drawings).Read(content),
        new PageVisibilityTable().Read(content),
        new PageTransitionTable(values, transitions).Read(content),
        new ListStyleTable().Read(content, styles),
        new OdfTextFrameStylesBuilder(_frames).Build(content, styles),
        new OdfPresentationTextStyles(
            Presentational(content.Element(OdfPackage.Namespaces.Office + "automatic-styles")),
            Presentational(styles?.Element(OdfPackage.Namespaces.Office + "styles")),
            new OdfParagraphPropertiesReader(values, _frames)),
        DataStyles(content, styles));

    private Dictionary<string, string> DataStyles(XElement content, XElement? styles)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var roots = new[]
        {
            styles?.Element(OdfPackage.Namespaces.Office + "styles"),
            styles?.Element(OdfPackage.Namespaces.Office + "automatic-styles"),
            content.Element(OdfPackage.Namespaces.Office + "automatic-styles"),
        };
        foreach (var style in roots.SelectMany(root => root?.Elements() ?? []).Where(dates.IsDataStyle))
            if ((string?)style.Attribute(OdfPackage.Namespaces.Style + "name") is { } name && dates.Read(style) is { } pattern)
                result[name] = pattern;
        return result;
    }

    private Dictionary<string, XElement> Presentational(XElement? root) =>
        (root?.Elements(OdfPackage.Namespaces.Style + "style") ?? [])
            .Where(style => (string?)style.Attribute(OdfPackage.Namespaces.Style + "family") == "presentation")
            .GroupBy(style => (string?)style.Attribute(OdfPackage.Namespaces.Style + "name") ?? string.Empty)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    private Dictionary<string, TValue> Present<TValue>(IReadOnlyDictionary<string, TValue?> table) where TValue : struct =>
        table.Where(entry => entry.Value.HasValue).ToDictionary(entry => entry.Key, entry => entry.Value!.Value, StringComparer.Ordinal);
}

using System.Xml;
using System.Xml.Linq;
using PulseOffice.Documents;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;

namespace PulseOffice.Formats.Odf.Odt;

internal sealed record AutomaticParagraph(string? Parent, ParagraphProperties Paragraph, CharacterProperties Character, string? Master, bool Implied, int? LineNumber = null, bool LineNumberImplied = false, bool ListSuppressed = false);

internal sealed record AutomaticText(string? Parent, CharacterProperties Character);

internal sealed class OdtAutomaticStyles(IReadOnlyDictionary<(string Family, string Name), XElement> styles, IReadOnlyDictionary<string, XElement>? graphics = null, XElement? graphicDefault = null) : OdtXml
{
    public const string DataFamily = "data";

    public static readonly OdtAutomaticStyles Empty = new(new Dictionary<(string, string), XElement>());

    private static readonly XName[] PlacementAttributes =
    [
        OdfPackage.Namespaces.Style + "wrap", OdfPackage.Namespaces.Style + "horizontal-pos", OdfPackage.Namespaces.Style + "horizontal-rel",
        OdfPackage.Namespaces.Style + "vertical-pos", OdfPackage.Namespaces.Style + "vertical-rel", OdfPackage.Namespaces.Style + "run-through",
        OdfPackage.Namespaces.Style + "wrap-contour", OdfPackage.Namespaces.Style + "wrap-contour-mode", OdfPackage.Namespaces.Style + "number-wrapped-paragraphs",
        OdfPackage.Namespaces.Fo + "margin-left", OdfPackage.Namespaces.Fo + "margin-right", OdfPackage.Namespaces.Fo + "margin-top", OdfPackage.Namespaces.Fo + "margin-bottom",
    ];

    public XElement? GraphicPlacement(string? styleName)
    {
        var own = Find("graphic", styleName);
        var chain = new List<XElement>();
        var current = own ?? (styleName is not null && graphics?.TryGetValue(styleName, out var named) == true ? named : null);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (current is not null && chain.Count < 64)
        {
            chain.Add(current);
            var parent = (string?)current.Attribute(OdfPackage.Namespaces.Style + "parent-style-name");
            current = parent is not null && seen.Add(parent) && graphics?.TryGetValue(parent, out var next) == true ? next : null;
        }
        if (graphicDefault is not null) chain.Add(graphicDefault);
        var first = own?.Element(OdfPackage.Namespaces.Style + "graphic-properties");
        if (chain.Count == (own is null ? 0 : 1)) return first;
        var merged = first is null ? new XElement(OdfPackage.Namespaces.Style + "graphic-properties") : new XElement(first);
        foreach (var name in PlacementAttributes)
        {
            if (merged.Attribute(name) is not null) continue;
            var inherited = chain.Select(style => style.Element(OdfPackage.Namespaces.Style + "graphic-properties")?.Attribute(name)).FirstOrDefault(attribute => attribute is not null);
            if (inherited is not null) merged.SetAttributeValue(name, inherited.Value);
        }
        return merged;
    }

    public XElement? Find(string family, string? name) =>
        name is not null && styles.TryGetValue((family, name), out var style) ? style : null;

    public IReadOnlySet<string> ChainTargets { get; } = styles.Values
        .Select(style => (string?)style.Element(OdfPackage.Namespaces.Style + "graphic-properties")?.Attribute(OdfPackage.Namespaces.Draw + "chain-next-name"))
        .OfType<string>()
        .ToHashSet(StringComparer.Ordinal);
}

internal sealed class OdtAutomaticStyleIndex
{
    private static readonly HashSet<string> CommonFallbackFamilies = new(["table-column", "table-row"], StringComparer.Ordinal);

    public OdtAutomaticStyles Read(XElement? automatic, XElement? common = null)
    {
        var map = new Dictionary<(string, string), XElement>();
        foreach (var data in new[] { automatic, common }.OfType<XElement>().SelectMany(container => container.Elements())
                     .Where(element => element.Name.Namespace == OdfPackage.Namespaces.Number))
            if ((string?)data.Attribute(OdfPackage.Namespaces.Style + "name") is { } dataName) map.TryAdd((OdtAutomaticStyles.DataFamily, dataName), data);
        foreach (var style in automatic?.Elements(OdfPackage.Namespaces.Style + "style") ?? [])
        {
            var family = (string?)style.Attribute(OdfPackage.Namespaces.Style + "family");
            var name = (string?)style.Attribute(OdfPackage.Namespaces.Style + "name");
            if (family is not null && name is not null) map.TryAdd((family, name), style);
        }
        foreach (var style in common?.Elements(OdfPackage.Namespaces.Style + "style") ?? [])
            if ((string?)style.Attribute(OdfPackage.Namespaces.Style + "family") is { } commonFamily && CommonFallbackFamilies.Contains(commonFamily)
                && (string?)style.Attribute(OdfPackage.Namespaces.Style + "name") is { } commonName)
                map.TryAdd((commonFamily, commonName), style);
        var graphics = new Dictionary<string, XElement>(StringComparer.Ordinal);
        foreach (var style in common?.Elements(OdfPackage.Namespaces.Style + "style") ?? [])
            if ((string?)style.Attribute(OdfPackage.Namespaces.Style + "family") == "graphic" && (string?)style.Attribute(OdfPackage.Namespaces.Style + "name") is { } graphicName)
                graphics.TryAdd(graphicName, style);
        var graphicDefault = common?.Elements(OdfPackage.Namespaces.Style + "default-style")
            .FirstOrDefault(style => (string?)style.Attribute(OdfPackage.Namespaces.Style + "family") == "graphic");
        return new OdtAutomaticStyles(map, graphics, graphicDefault);
    }
}

internal sealed class OdtNameMap(OdtStyleNames names)
{
    private readonly Dictionary<string, string> _map = new(StringComparer.Ordinal);

    public void Add(string encoded, string model) => _map.TryAdd(encoded, model);

    public string Resolve(string encoded) => _map.TryGetValue(encoded, out var model) ? model : names.Decode(encoded);
}

internal sealed class OdtReadContext(
    TextDocument document,
    IReadOnlyDictionary<string, string> fontFaces,
    Func<string, byte[]?> readBytes,
    OdtNameMap styleNames,
    OdtNameMap pageNames)
{
    public const int MaximumDepth = 64;

    private const int RepetitionBudget = 100_000;

    private readonly HashSet<string> _ids = new(StringComparer.Ordinal);

    private int _repetitions = RepetitionBudget;

    private const int SpaceBudget = 10_000_000;

    private int _spaces = SpaceBudget;

    public TextDocument Document { get; } = document;

    public IReadOnlyDictionary<string, string> FontFaces { get; } = fontFaces;

    public Func<string, byte[]?> ReadBytes { get; } = readBytes;

    public OdtNameMap StyleNames { get; } = styleNames;

    public OdtNameMap PageNames { get; } = pageNames;

    public OdtAutomaticStyles Automatic { get; set; } = OdtAutomaticStyles.Empty;

    public HashSet<string> EndedComments { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> CommentNames { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> CommentParents { get; } = new(StringComparer.Ordinal);

    public OdtListContinuity Lists { get; } = new();

    public Dictionary<string, (string ParagraphId, int Offset, string Index)> OpenIndexMarks { get; } = new(StringComparer.Ordinal);

    public int Depth { get; set; }

    public bool Ours { get; set; }

    public Func<string, XDocument?> ReadXml { get; set; } = _ => null;

    public IReadOnlyDictionary<string, XElement> Drawings { get; set; } = new Dictionary<string, XElement>();

    public Dictionary<string, XElement> FormDeclarations { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Losses { get; } = new(StringComparer.Ordinal);

    public int TakeSpaces(int requested)
    {
        var granted = Math.Clamp(requested, 1, Math.Max(1, _spaces));
        _spaces = Math.Max(0, _spaces - granted);
        return granted;
    }

    public bool TakeRepetition(int copies)
    {
        if (copies > _repetitions) return false;
        _repetitions -= copies;
        return true;
    }

    public string Id(XElement element, string prefix)
    {
        var declared = (string?)element.Attribute(OdfPackage.Namespaces.Xml + "id");
        return Id(declared, prefix);
    }

    public string Id(string? declared, string prefix)
    {
        if (declared is { Length: > 0 } && IsName(declared) && _ids.Add(declared))
        {
            Document.Ids.Reserve(declared);
            return declared;
        }

        string id;
        do id = Document.NextId(prefix + "_");
        while (!_ids.Add(id));
        return id;
    }

    private bool IsName(string value)
    {
        try
        {
            XmlConvert.VerifyNCName(value);
            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }
}

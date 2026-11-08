using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace PulseOffice.Tools.CorpusAudit;

public sealed class RuleContext(NamespaceKeys keys, IReadOnlySet<string> numberedStyles, IReadOnlySet<string> invisibleGraphicStyles)
{
    public HashSet<string> ListedParagraphStyles { get; } = new(StringComparer.Ordinal);

    public HashSet<string> NumberedOutlineLevels { get; } = new(StringComparer.Ordinal);

    public HashSet<string> SilentNumberings { get; } = new(StringComparer.Ordinal);

    public HashSet<string> SilentListStyles { get; } = new(StringComparer.Ordinal);

    public IReadOnlySet<string> NumberedStyles { get; } = numberedStyles;

    public IReadOnlySet<string> InvisibleGraphicStyles { get; } = invisibleGraphicStyles;

    public string Key(XName name) => keys.Key(name);

    public string? Attribute(XElement element, string key) => keys.Attribute(element, key);

    public bool HasChild(XElement element, params string[] names) => keys.HasChild(element, names);

    public bool HasAncestor(XElement element, string key) => keys.HasAncestor(element, key);

    public bool HasDescendant(XElement element, params string[] names) => keys.HasDescendant(element, names);

    public bool HasText(XElement element, string textElement) => keys.HasText(element, textElement);

    public XElement? Child(XElement? element, string key) => element?.Elements().FirstOrDefault(child => keys.Key(child.Name) == key);
}

public interface IRuleContextFactory
{
    RuleContext Create(ZipArchive archive, string extension);
}

public sealed class RuleContextFactory(NamespaceKeys keys) : IRuleContextFactory
{
    private readonly XmlReaderSettings _settings = new() { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };

    public RuleContext Create(ZipArchive archive, string extension)
    {
        var odf = extension.StartsWith(".od", StringComparison.OrdinalIgnoreCase);
        var word = extension.Equals(".docx", StringComparison.OrdinalIgnoreCase);
        var silent = word ? SilentNumberings(archive) : [];
        var context = new RuleContext(keys, word ? NumberedParagraphStyles(archive, silent) : new HashSet<string>(),
            odf ? InvisibleGraphicStyles(archive) : new HashSet<string>());
        context.SilentNumberings.UnionWith(silent);
        if (odf) context.ListedParagraphStyles.UnionWith(ListedParagraphStyles(archive));
        if (odf) context.NumberedOutlineLevels.UnionWith(NumberedOutlineLevels(archive));
        if (odf) context.SilentListStyles.UnionWith(SilentListStyles(archive));
        return context;
    }

    private HashSet<string> ListedParagraphStyles(ZipArchive archive)
    {
        var byName = new[] { "styles.xml", "content.xml" }.SelectMany(part => Load(archive, part)?.Descendants() ?? [])
            .Where(style => keys.Key(style.Name) == "style:style" && keys.Attribute(style, "style:family") == "paragraph" && keys.Attribute(style, "style:name") is not null)
            .GroupBy(style => keys.Attribute(style, "style:name")!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
        return byName.Keys.Where(name => Listed(name, byName, [])).ToHashSet(StringComparer.Ordinal);
    }

    private IEnumerable<string> NumberedOutlineLevels(ZipArchive archive) =>
        (Load(archive, "styles.xml")?.Descendants() ?? []).Where(level => keys.Key(level.Name) == "text:outline-level-style"
                && keys.Attribute(level, "style:num-format") is { Length: > 0 })
            .Select(level => keys.Attribute(level, "text:level")).OfType<string>();

    private bool Listed(string name, Dictionary<string, XElement> byName, HashSet<string> visited)
    {
        if (!visited.Add(name) || !byName.TryGetValue(name, out var style)) return false;
        if (keys.Attribute(style, "style:list-style-name") is { } list) return list.Length > 0 && list != "Outline";
        return keys.Attribute(style, "style:parent-style-name") is { } parent && Listed(parent, byName, visited);
    }

    private HashSet<string> InvisibleGraphicStyles(ZipArchive archive) =>
        (Load(archive, "content.xml")?.Descendants() ?? []).Where(style => keys.Key(style.Name) == "style:style" && keys.Attribute(style, "style:family") == "graphic")
            .Where(style => style.Elements().FirstOrDefault(child => keys.Key(child.Name) == "style:graphic-properties") is { } properties
                && keys.Attribute(properties, "draw:fill") == "none" && keys.Attribute(properties, "draw:stroke") == "none")
            .Select(style => keys.Attribute(style, "style:name")).OfType<string>().ToHashSet(StringComparer.Ordinal);

    private HashSet<string> SilentNumberings(ZipArchive archive)
    {
        var numbering = Load(archive, "word/numbering.xml")?.Root;
        if (numbering is null) return [];
        var silentLevels = numbering.Elements().Where(item => keys.Key(item.Name) == "w:abstractNum")
            .SelectMany(item => item.Elements().Where(level => keys.Key(level.Name) == "w:lvl" && SilentLevel(level))
                .Select(level => (Abstract: keys.Attribute(item, "w:abstractNumId"), Level: keys.Attribute(level, "w:ilvl") ?? "0")))
            .ToLookup(entry => entry.Abstract ?? string.Empty, entry => entry.Level, StringComparer.Ordinal);
        return numbering.Elements().Where(item => keys.Key(item.Name) == "w:num")
            .SelectMany(item => item.Elements().FirstOrDefault(child => keys.Key(child.Name) == "w:abstractNumId") is { } link
                && keys.Attribute(link, "w:val") is { } id && keys.Attribute(item, "w:numId") is { } numId
                    ? silentLevels[id].Select(level => SilentKey(numId, level))
                    : [])
            .ToHashSet(StringComparer.Ordinal);
    }

    public static string SilentKey(string numId, string level) => numId + "/" + level;

    private IEnumerable<string> SilentListStyles(ZipArchive archive) =>
        new[] { "styles.xml", "content.xml" }.SelectMany(part => Load(archive, part)?.Descendants() ?? [])
            .Where(list => keys.Key(list.Name) == "text:list-style")
            .Where(list => list.Elements().FirstOrDefault(level => keys.Attribute(level, "text:level") == "1") is { } first
                && keys.Key(first.Name) == "text:list-level-style-number" && keys.Attribute(first, "style:num-format") is null or ""
                && (keys.Attribute(first, "style:num-prefix") ?? string.Empty).Trim().Length == 0
                && (keys.Attribute(first, "style:num-suffix") ?? string.Empty).Trim().Length == 0)
            .Select(list => keys.Attribute(list, "style:name")).OfType<string>();

    private bool SilentLevel(XElement level)
    {
        var none = level.Elements().FirstOrDefault(child => keys.Key(child.Name) == "w:numFmt") is { } format && keys.Attribute(format, "w:val") == "none";
        var text = level.Elements().FirstOrDefault(child => keys.Key(child.Name) == "w:lvlText") is { } label ? keys.Attribute(label, "w:val") ?? string.Empty : string.Empty;
        var visible = none ? System.Text.RegularExpressions.Regex.Replace(text, @"%\d", string.Empty) : text;
        return visible.Trim().Length == 0 && !level.Elements().Any(child => keys.Key(child.Name) == "w:lvlPicBulletId");
    }

    private HashSet<string> NumberedParagraphStyles(ZipArchive archive, HashSet<string> silent)
    {
        var styles = Load(archive, "word/styles.xml")?.Root?.Elements().Where(style => keys.Key(style.Name) == "w:style"
            && keys.Attribute(style, "w:type") is null or "paragraph").ToList() ?? [];
        var byId = styles.Where(style => keys.Attribute(style, "w:styleId") is not null)
            .GroupBy(style => keys.Attribute(style, "w:styleId")!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        return byId.Keys.Where(id => Numbered(id, byId, silent, [])).ToHashSet(StringComparer.Ordinal);
    }

    private bool Numbered(string id, Dictionary<string, XElement> byId, HashSet<string> silent, HashSet<string> visited)
    {
        if (!visited.Add(id) || !byId.TryGetValue(id, out var style)) return false;
        var properties = style.Elements().FirstOrDefault(child => keys.Key(child.Name) == "w:pPr");
        var numbering = properties?.Elements().FirstOrDefault(child => keys.Key(child.Name) == "w:numPr");
        var numId = numbering?.Elements().FirstOrDefault(child => keys.Key(child.Name) == "w:numId");
        var level = numbering?.Elements().FirstOrDefault(child => keys.Key(child.Name) == "w:ilvl") is { } ilvl ? keys.Attribute(ilvl, "w:val") ?? "0" : "0";
        if (numId is not null) return keys.Attribute(numId, "w:val") is { } value && value != "0" && !silent.Contains(SilentKey(value, level));
        var parent = style.Elements().FirstOrDefault(child => keys.Key(child.Name) == "w:basedOn");
        return parent is not null && keys.Attribute(parent, "w:val") is { } basedOn && Numbered(basedOn, byId, silent, visited);
    }

    private XDocument? Load(ZipArchive archive, string part)
    {
        var entry = archive.GetEntry(part);
        if (entry is null) return null;
        try
        {
            using var stream = entry.Open();
            using var reader = XmlReader.Create(stream, _settings);
            return XDocument.Load(reader);
        }
        catch (XmlException)
        {
            return null;
        }
    }
}

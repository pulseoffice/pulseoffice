using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace PulseOffice.Tools.CorpusAudit;

public interface IFeatureCounter
{
    IReadOnlyDictionary<string, int>? Count(byte[] package, string extension);
}

public sealed class FeatureCounter(IFeatureCatalog catalog, NamespaceKeys keys, IRuleContextFactory contexts) : IFeatureCounter
{
    private readonly XmlReaderSettings _settings = new() { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };

    public IReadOnlyDictionary<string, int>? Count(byte[] package, string extension)
    {
        var rules = catalog.RulesFor(extension).ToLookup(rule => rule.Element, StringComparer.Ordinal);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        try
        {
            using var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
            var orphans = Orphans(archive, extension);
            var context = contexts.Create(archive, extension);
            foreach (var entry in archive.Entries.Where(entry => catalog.PartIncluded(extension, entry.FullName) && !orphans.Contains(entry.FullName)))
            {
                XDocument document;
                try
                {
                    using var stream = entry.Open();
                    using var reader = XmlReader.Create(stream, _settings);
                    document = XDocument.Load(reader);
                }
                catch (XmlException)
                {
                    continue;
                }

                foreach (var element in document.Descendants())
                {
                    var key = keys.Key(element.Name);
                    if (!rules.Contains(key) || keys.HasAncestor(element, "mc:Fallback")) continue;
                    var deleted = keys.HasAncestor(element, "text:deletion");
                    foreach (var rule in rules[key].Where(rule => catalog.FeatureIncluded(extension, entry.FullName, rule.Feature) && (!deleted || rule.Feature is not ("indexes" or "tables" or "lists"))))
                    {
                        var weight = rule.Weight(element, context);
                        if (weight > 0) counts[rule.Feature] = counts.GetValueOrDefault(rule.Feature) + weight;
                    }
                }
            }
        }
        catch (InvalidDataException)
        {
            return null;
        }

        return counts;
    }

    private HashSet<string> Orphans(ZipArchive archive, string extension)
    {
        var orphans = new HashSet<string>(StringComparer.Ordinal);
        if (extension.Equals(".pptx", StringComparison.OrdinalIgnoreCase)) return UnreferencedCharts(archive);
        if (extension.StartsWith(".od", StringComparison.OrdinalIgnoreCase)) return UnreferencedObjects(archive);
        if (!extension.Equals(".docx", StringComparison.OrdinalIgnoreCase)) return orphans;
        var main = Load(archive, "word/document.xml");
        var relations = Load(archive, "word/_rels/document.xml.rels");
        if (main is null || relations is null) return orphans;
        var used = main.Descendants().Where(element => keys.Key(element.Name) is "w:headerReference" or "w:footerReference")
            .Select(element => element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "id")?.Value)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var stored in archive.Entries.Where(entry => entry.FullName.StartsWith("customXml/item", StringComparison.Ordinal) && !entry.FullName.Contains("Props", StringComparison.Ordinal)))
        {
            var root = Load(archive, stored.FullName)?.Root;
            if (root?.Name.NamespaceName != "urn:pulseoffice:page-styles") continue;
            used.UnionWith(root.Descendants().Where(element => element.Name.LocalName is "headerReference" or "footerReference")
                .Select(element => element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "id")?.Value));
        }
        var referenced = new HashSet<string>(StringComparer.Ordinal);
        foreach (var relation in relations.Root?.Elements() ?? [])
        {
            var type = relation.Attribute("Type")?.Value ?? string.Empty;
            if (!type.EndsWith("/header", StringComparison.Ordinal) && !type.EndsWith("/footer", StringComparison.Ordinal)) continue;
            var part = "word/" + (relation.Attribute("Target")?.Value ?? string.Empty).TrimStart('/').Replace("word/", string.Empty, StringComparison.Ordinal);
            if (used.Contains(relation.Attribute("Id")?.Value)) referenced.Add(part);
            else orphans.Add(part);
        }

        foreach (var entry in archive.Entries.Select(entry => entry.FullName).Where(IsHeaderOrFooter).Where(name => !referenced.Contains(name))) orphans.Add(entry);

        return orphans;
    }

    private HashSet<string> UnreferencedObjects(ZipArchive archive)
    {
        var referenced = new[] { "content.xml", "styles.xml" }.Select(part => Load(archive, part))
            .SelectMany(document => document?.Descendants() ?? [])
            .Where(element => keys.Key(element.Name) is "draw:object" or "draw:object-ole")
            .Select(element => element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "href")?.Value ?? string.Empty)
            .Select(href => href.StartsWith("./", StringComparison.Ordinal) ? href[2..] : href)
            .Select(href => href.TrimEnd('/') + "/")
            .ToHashSet(StringComparer.Ordinal);
        return archive.Entries.Select(entry => entry.FullName)
            .Where(name => name.EndsWith("/content.xml", StringComparison.Ordinal) && !referenced.Contains(name[..^"content.xml".Length]))
            .ToHashSet(StringComparer.Ordinal);
    }

    private HashSet<string> UnreferencedCharts(ZipArchive archive)
    {
        var referenced = archive.Entries.Where(entry => entry.FullName.StartsWith("ppt/slides/_rels/", StringComparison.Ordinal))
            .Select(entry => Load(archive, entry.FullName))
            .SelectMany(relations => relations?.Root?.Elements() ?? [])
            .Select(relation => relation.Attribute("Target")?.Value ?? string.Empty)
            .Select(target => "ppt/" + target.Replace("../", string.Empty, StringComparison.Ordinal).TrimStart('/').Replace("ppt/", string.Empty, StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        return archive.Entries.Select(entry => entry.FullName)
            .Where(name => name.StartsWith("ppt/charts/chart", StringComparison.Ordinal) && name.EndsWith(".xml", StringComparison.Ordinal) && !referenced.Contains(name))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool IsHeaderOrFooter(string part) =>
        part.StartsWith("word/header", StringComparison.Ordinal) || part.StartsWith("word/footer", StringComparison.Ordinal);

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

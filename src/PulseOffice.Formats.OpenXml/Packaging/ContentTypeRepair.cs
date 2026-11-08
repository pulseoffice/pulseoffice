using System.Xml.Linq;

namespace PulseOffice.Formats.OpenXml;

internal sealed class ContentTypeRepair : IPackageRepairStep
{
    private static readonly XNamespace Types = "http://schemas.openxmlformats.org/package/2006/content-types";
    private const string Officedocument = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/";
    private const string Spreadsheet = "application/vnd.openxmlformats-officedocument.spreadsheetml.";
    private const string RelationshipsType = "application/vnd.openxmlformats-package.relationships+xml";

    private readonly PackageRelationships _relationships = new();
    private readonly SafeXmlLoader _xml = new();

    private readonly IReadOnlyDictionary<string, string> _byRelationship = new Dictionary<string, string>
    {
        [Officedocument + "officeDocument"] = Spreadsheet + "sheet.main+xml",
        [Officedocument + "worksheet"] = Spreadsheet + "worksheet+xml",
        [Officedocument + "chartsheet"] = Spreadsheet + "chartsheet+xml",
        [Officedocument + "styles"] = Spreadsheet + "styles+xml",
        [Officedocument + "sharedStrings"] = Spreadsheet + "sharedStrings+xml",
        [Officedocument + "externalLink"] = Spreadsheet + "externalLink+xml",
        [Officedocument + "calcChain"] = Spreadsheet + "calcChain+xml",
        [Officedocument + "comments"] = Spreadsheet + "comments+xml",
        [Officedocument + "table"] = Spreadsheet + "table+xml",
        [Officedocument + "pivotTable"] = Spreadsheet + "pivotTable+xml",
        [Officedocument + "pivotCacheDefinition"] = Spreadsheet + "pivotCacheDefinition+xml",
        [Officedocument + "pivotCacheRecords"] = Spreadsheet + "pivotCacheRecords+xml",
        [Officedocument + "theme"] = "application/vnd.openxmlformats-officedocument.theme+xml",
        [Officedocument + "drawing"] = "application/vnd.openxmlformats-officedocument.drawing+xml",
        [Officedocument + "chart"] = "application/vnd.openxmlformats-officedocument.drawingml.chart+xml",
        [Officedocument + "extended-properties"] = "application/vnd.openxmlformats-officedocument.extended-properties+xml",
        ["http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties"] = "application/vnd.openxmlformats-package.core-properties+xml",
    };

    private const string Wordprocessing = "application/vnd.openxmlformats-officedocument.wordprocessingml.";

    private readonly IReadOnlyDictionary<string, string> _wordByRelationship = new Dictionary<string, string>
    {
        [Officedocument + "officeDocument"] = Wordprocessing + "document.main+xml",
        [Officedocument + "header"] = Wordprocessing + "header+xml",
        [Officedocument + "footer"] = Wordprocessing + "footer+xml",
        [Officedocument + "footnotes"] = Wordprocessing + "footnotes+xml",
        [Officedocument + "endnotes"] = Wordprocessing + "endnotes+xml",
        [Officedocument + "comments"] = Wordprocessing + "comments+xml",
        [Officedocument + "styles"] = Wordprocessing + "styles+xml",
        [Officedocument + "numbering"] = Wordprocessing + "numbering+xml",
        [Officedocument + "settings"] = Wordprocessing + "settings+xml",
        [Officedocument + "fontTable"] = Wordprocessing + "fontTable+xml",
        [Officedocument + "webSettings"] = Wordprocessing + "webSettings+xml",
        [Officedocument + "glossaryDocument"] = Wordprocessing + "document.glossary+xml",
        [Officedocument + "customXmlProps"] = "application/vnd.openxmlformats-officedocument.customXmlProperties+xml",
        ["http://schemas.microsoft.com/office/2007/relationships/stylesWithEffects"] = "application/vnd.ms-word.stylesWithEffects+xml",
        [Officedocument + "theme"] = "application/vnd.openxmlformats-officedocument.theme+xml",
        [Officedocument + "chart"] = "application/vnd.openxmlformats-officedocument.drawingml.chart+xml",
        [Officedocument + "extended-properties"] = "application/vnd.openxmlformats-officedocument.extended-properties+xml",
        ["http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties"] = "application/vnd.openxmlformats-package.core-properties+xml",
    };

    public bool Apply(PackageModel package)
    {
        if (package[PackageModel.ContentTypesPart] is not { } bytes) return false;

        XDocument document;
        try
        {
            using var input = new MemoryStream(bytes);
            document = _xml.Document(input);
        }
        catch (System.Xml.XmlException)
        {
            return false;
        }

        if (document.Root is not { } root) return false;
        var map = package.Names.Any(name => name.StartsWith("word/", StringComparison.OrdinalIgnoreCase)) ? _wordByRelationship : _byRelationship;
        var changed = EnsureDefault(root, "rels", RelationshipsType);
        changed |= EnsureDefault(root, "xml", "application/xml");
        var overrides = root.Elements(Types + "Override")
            .GroupBy(element => PartOf(element), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        foreach (var (relsPart, relationships) in _relationships.All(package))
        {
            foreach (var (relationship, target) in _relationships.Internal(relsPart, relationships))
            {
                if (package.ActualName(target) is not { } part) continue;
                if (!map.TryGetValue((string?)relationship.Attribute("Type") ?? string.Empty, out var expected)) continue;
                var current = ContentTypeOf(root, overrides, part);
                var caseOnly = overrides.TryGetValue(part, out var declared) && declared.All(element => PartOf(element) != part);
                if (!caseOnly && (current == expected || !IsGeneric(current))) continue;
                Override(root, overrides, part, expected);
                changed = true;
            }
        }

        if (!changed) return false;
        using var output = new MemoryStream();
        document.Save(output);
        package[PackageModel.ContentTypesPart] = output.ToArray();
        return true;
    }

    private bool EnsureDefault(XElement root, string extension, string contentType)
    {
        if (root.Elements(Types + "Default").Any(element => string.Equals((string?)element.Attribute("Extension"), extension, StringComparison.OrdinalIgnoreCase)))
            return false;
        root.AddFirst(new XElement(Types + "Default", new XAttribute("Extension", extension), new XAttribute("ContentType", contentType)));
        return true;
    }

    private bool IsGeneric(string? contentType) =>
        string.IsNullOrEmpty(contentType) || contentType is "application/xml" or "text/xml" or "application/octet-stream";

    private string PartOf(XElement element) => Uri.UnescapeDataString(((string?)element.Attribute("PartName") ?? string.Empty).TrimStart('/'));

    private string? ContentTypeOf(XElement root, Dictionary<string, List<XElement>> overrides, string part)
    {
        if (overrides.TryGetValue(part, out var explicitTypes)
            && explicitTypes.FirstOrDefault(element => PartOf(element) == part) is { } exact)
            return (string?)exact.Attribute("ContentType");
        var extension = Path.GetExtension(part).TrimStart('.');
        return (string?)root.Elements(Types + "Default")
            .FirstOrDefault(element => string.Equals((string?)element.Attribute("Extension"), extension, StringComparison.OrdinalIgnoreCase))
            ?.Attribute("ContentType");
    }

    private void Override(XElement root, Dictionary<string, List<XElement>> overrides, string part, string contentType)
    {
        if (overrides.TryGetValue(part, out var existing))
            foreach (var element in existing) element.Remove();
        var replacement = new XElement(Types + "Override",
            new XAttribute("PartName", "/" + string.Join('/', part.Split('/').Select(Uri.EscapeDataString))),
            new XAttribute("ContentType", contentType));
        root.Add(replacement);
        overrides[part] = [replacement];
    }
}

using System.Xml.Linq;

namespace PulseOffice.Formats.OpenXml;

internal sealed class PackageRelationships
{
    private readonly SafeXmlLoader _xml = new();

    public static readonly XNamespace Namespace = "http://schemas.openxmlformats.org/package/2006/relationships";

    public IEnumerable<(string RelsPart, XDocument Document)> All(PackageModel package)
    {
        foreach (var name in package.Names.Where(name => name.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            XDocument document;
            try
            {
                using var input = new MemoryStream(package[name]!);
                document = _xml.Document(input);
            }
            catch (System.Xml.XmlException)
            {
                continue;
            }

            yield return (name, document);
        }
    }

    public IEnumerable<(XElement Relationship, string Target)> Internal(string relsPart, XDocument document) =>
        document.Root?.Elements(Namespace + "Relationship")
            .Where(relationship => (string?)relationship.Attribute("TargetMode") != "External")
            .Select(relationship => (relationship, Target: Resolve(SourceFolder(relsPart), (string?)relationship.Attribute("Target"))))
            .Where(pair => pair.Target is not null)
            .Select(pair => (pair.relationship, pair.Target!)) ?? [];

    public void Store(PackageModel package, string relsPart, XDocument document)
    {
        using var output = new MemoryStream();
        document.Save(output);
        package[relsPart] = output.ToArray();
    }

    private string SourceFolder(string relsPath) =>
        Path.GetDirectoryName(Path.GetDirectoryName(relsPath) ?? string.Empty)?.Replace('\\', '/') ?? string.Empty;

    private string? Resolve(string folder, string? target)
    {
        if (string.IsNullOrWhiteSpace(target)) return null;
        target = Uri.UnescapeDataString(target.Replace('\\', '/').Split('#')[0]);
        if (target.Length == 0) return null;
        var combined = target.StartsWith('/') ? target : $"{folder}/{target}";
        var stack = new List<string>();
        foreach (var segment in combined.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".") continue;
            if (segment == "..") { if (stack.Count > 0) stack.RemoveAt(stack.Count - 1); continue; }
            stack.Add(segment);
        }

        return string.Join('/', stack);
    }
}

internal sealed class DanglingRelationshipRepair : IPackageRepairStep
{
    private readonly PackageRelationships _relationships = new();

    public bool Apply(PackageModel package)
    {
        var changed = false;
        foreach (var (relsPart, document) in _relationships.All(package).ToList())
        {
            var dangling = _relationships.Internal(relsPart, document)
                .Where(pair => !package.Contains(pair.Target))
                .Select(pair => pair.Relationship)
                .ToList();
            if (dangling.Count == 0) continue;
            foreach (var relationship in dangling) relationship.Remove();
            _relationships.Store(package, relsPart, document);
            changed = true;
        }

        return changed;
    }
}

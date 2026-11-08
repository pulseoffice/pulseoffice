using System.Xml.Linq;

namespace PulseOffice.Formats.OpenXml;

internal sealed class DuplicateRelationshipRepair : IPackageRepairStep
{
    private readonly PackageRelationships _relationships = new();

    public bool Apply(PackageModel package)
    {
        var changed = false;
        foreach (var (relsPart, document) in _relationships.All(package).ToList())
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var duplicates = document.Root?.Elements(PackageRelationships.Namespace + "Relationship")
                .Where(relationship => !seen.Add((string?)relationship.Attribute("Id") ?? string.Empty))
                .ToList() ?? [];
            if (duplicates.Count == 0) continue;
            foreach (var duplicate in duplicates) duplicate.Remove();
            _relationships.Store(package, relsPart, document);
            changed = true;
        }

        return changed;
    }
}

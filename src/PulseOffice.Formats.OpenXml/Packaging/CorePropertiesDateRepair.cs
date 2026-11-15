using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PulseOffice.Formats.OpenXml;

internal sealed class CorePropertiesDateRepair : IPackageRepairStep
{
    private const string CoreRelationship = "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties";
    private const int TicksDigits = 7;

    private readonly PackageRelationships _relationships = new();
    private readonly SafeXmlLoader _xml = new();
    private readonly XNamespace _terms = "http://purl.org/dc/terms/";
    private readonly XNamespace _core = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
    private readonly Regex _fraction = new(@"\.(\d+)", RegexOptions.CultureInvariant);

    public bool Apply(PackageModel package)
    {
        var changed = false;
        foreach (var name in CoreParts(package))
        {
            if (package[name] is not { } bytes) continue;
            XDocument document;
            try
            {
                using var input = new MemoryStream(bytes);
                document = _xml.Document(input);
            }
            catch (System.Xml.XmlException)
            {
                continue;
            }

            var dates = document.Root?.Elements().Where(element => element.Name == _terms + "created" || element.Name == _terms + "modified" || element.Name == _core + "lastPrinted").ToList() ?? [];
            var touched = false;
            foreach (var date in dates)
            {
                var trimmed = _fraction.Replace(date.Value, match => match.Groups[1].Length > TicksDigits ? "." + match.Groups[1].Value[..TicksDigits] : match.Value, 1);
                if (trimmed == date.Value) continue;
                date.Value = trimmed;
                touched = true;
            }

            if (!touched) continue;
            using var output = new MemoryStream();
            document.Save(output);
            package[name] = output.ToArray();
            changed = true;
        }

        return changed;
    }

    private IEnumerable<string> CoreParts(PackageModel package) =>
        _relationships.All(package)
            .Where(pair => string.Equals(pair.RelsPart, "_rels/.rels", StringComparison.OrdinalIgnoreCase))
            .SelectMany(pair => _relationships.Internal(pair.RelsPart, pair.Document))
            .Where(pair => (string?)pair.Relationship.Attribute("Type") == CoreRelationship)
            .Select(pair => package.ActualName(pair.Target))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}

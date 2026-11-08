using System.Xml.Linq;

namespace PulseOffice.Formats.OpenXml;

internal sealed class UndeclaredPartRepair : IPackageRepairStep
{
    private const string Fallback = "application/octet-stream";
    private static readonly XNamespace Types = "http://schemas.openxmlformats.org/package/2006/content-types";

    private readonly MediaTypes _media = new();
    private readonly SafeXmlLoader _xml = new();

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
        var defaults = new HashSet<string>(root.Elements(Types + "Default").Select(element => (string?)element.Attribute("Extension") ?? string.Empty), StringComparer.OrdinalIgnoreCase);
        var overrides = new HashSet<string>(root.Elements(Types + "Override").Select(element => Uri.UnescapeDataString(((string?)element.Attribute("PartName") ?? string.Empty).TrimStart('/'))), StringComparer.OrdinalIgnoreCase);
        var changed = false;

        foreach (var part in package.Names.Where(name => name != PackageModel.ContentTypesPart && !overrides.Contains(name)).ToList())
        {
            var extension = Path.GetExtension(part).TrimStart('.');
            if (extension.Length > 0 && defaults.Contains(extension)) continue;

            var type = _media.FromExtension(part) ?? Fallback;
            if (extension.Length > 0)
            {
                root.Add(new XElement(Types + "Default", new XAttribute("Extension", extension), new XAttribute("ContentType", type)));
                defaults.Add(extension);
            }
            else
            {
                root.Add(new XElement(Types + "Override", new XAttribute("PartName", "/" + string.Join('/', part.Split('/').Select(Uri.EscapeDataString))), new XAttribute("ContentType", type)));
                overrides.Add(part);
            }

            changed = true;
        }

        if (!changed) return false;
        using var output = new MemoryStream();
        document.Save(output);
        package[PackageModel.ContentTypesPart] = output.ToArray();
        return true;
    }
}

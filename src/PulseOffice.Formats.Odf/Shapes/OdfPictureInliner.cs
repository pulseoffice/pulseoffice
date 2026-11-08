using System.Xml.Linq;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfPictureInliner(IReadOnlyList<XName> carriers)
{
    public static readonly XName FillImage = Namespaces.Draw + "fill-image";

    public static readonly XName ListImage = Namespaces.Text + "list-level-style-image";

    public OdfPictureInliner() : this([FillImage])
    {
    }

    public void Inline(XDocument? document, Func<string, byte[]?> read)
    {
        if (document is null) return;
        foreach (var image in document.Descendants().Where(element => carriers.Contains(element.Name)).ToList())
        {
            if (image.Element(Namespaces.Office + "binary-data") is not null) continue;
            if ((string?)image.Attribute(Namespaces.Xlink + "href") is not { Length: > 0 } href || href.Contains("://", StringComparison.Ordinal)) continue;
            if (read(href.TrimStart('.', '/')) is { Length: > 0 } data)
                image.Add(new XElement(Namespaces.Office + "binary-data", Convert.ToBase64String(data)));
        }
    }
}

using System.Xml.Linq;
using PulseOffice.Foundation.Diagnostics;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal sealed class OdsLinkedPictures
{
    private readonly LinkedFiles _links = new();

    public Diagnostic Loss { get; } = Diagnostic.Warning("ODF192",
        "Pictures whose data is missing from the file are not imported: the file names a picture it does not contain.");

    public bool Present(XDocument content, Func<string, byte[]?> read) =>
        content.Descendants(Namespaces.Table + "table").Descendants(Namespaces.Draw + "frame").Any(frame => Unstored(frame, read));

    private bool Unstored(XElement frame, Func<string, byte[]?> read)
    {
        var images = frame.Elements(Namespaces.Draw + "image").ToList();
        return images.Count > 0 && frame.Element(Namespaces.Draw + "object") is null && frame.Element(Namespaces.Draw + "object-ole") is null
            && images.All(image => (string?)image.Attribute(Namespaces.Xlink + "href") is not { Length: > 0 } href || !_links.IsExternal(href) && read(href.TrimStart('.', '/')) is null);
    }
}

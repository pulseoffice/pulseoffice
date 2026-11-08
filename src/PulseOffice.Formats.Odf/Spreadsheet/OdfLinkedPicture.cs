using System.Xml.Linq;
using PulseOffice.Drawing.Assets;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfLinkedPicture
{
    private readonly LinkedFiles _links = new();
    private readonly OdfImageTypes _types = new();

    public RasterImageObject? Read(XElement frame)
    {
        if (frame.Element(Namespaces.Draw + "object") is not null || frame.Element(Namespaces.Draw + "object-ole") is not null) return null;
        foreach (var image in frame.Elements(Namespaces.Draw + "image"))
        {
            if ((string?)image.Attribute(Namespaces.Xlink + "href") is not { Length: > 0 } href || !_links.IsExternal(href)) continue;
            if (_links.ToPath(_links.FromPackageHref(href)) is not { Length: > 0 } path) continue;
            var declared = (string?)image.Attribute(Namespaces.Draw + "mime-type");
            return new RasterImageObject
            {
                Id = string.Empty,
                Data = ReadOnlyMemory<byte>.Empty,
                MediaType = declared is { Length: > 0 } ? declared : _types.MediaTypeOf(path),
                LinkPath = path,
            };
        }
        return null;
    }
}

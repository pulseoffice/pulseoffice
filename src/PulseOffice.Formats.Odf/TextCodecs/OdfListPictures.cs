using System.Security.Cryptography;
using System.Xml.Linq;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf.TextCodecs;

internal sealed class OdfListPictures(OdtMeasure measure, OdfImageTypes images) : OdfXml
{
    private const double DefaultSize = 9;

    private const double LargestSize = 4000;

    public OdfListPictures() : this(new OdtMeasure(), new OdfImageTypes())
    {
    }

    public XElement Element(ListBulletPicture picture, int number, Action<string, byte[], string> addMedia)
    {
        var href = $"Pictures/bullet-{Convert.ToHexStringLower(SHA256.HashData(picture.Data.Span))}.{images.Extension(picture.MediaType)}";
        addMedia(href, picture.Data.ToArray(), picture.MediaType);
        return new XElement(Text + "list-level-style-image",
            new XAttribute(Text + "level", number),
            new XAttribute(Xlink + "type", "simple"),
            new XAttribute(Xlink + "href", href),
            new XAttribute(Xlink + "show", "embed"),
            new XAttribute(Xlink + "actuate", "onLoad"));
    }

    public void Size(XElement properties, ListBulletPicture picture)
    {
        properties.SetAttributeValue(Fo + "width", measure.Points(picture.Width));
        properties.SetAttributeValue(Fo + "height", measure.Points(picture.Height));
    }

    public ListBulletPicture? Read(XElement level, Func<string, byte[]?> readBytes)
    {
        var properties = level.Element(Style + "list-level-properties");
        var width = Side(measure.Points((string?)properties?.Attribute(Fo + "width")));
        var height = Side(measure.Points((string?)properties?.Attribute(Fo + "height")));
        var href = (string?)level.Attribute(Xlink + "href");
        var mediaType = href is { Length: > 0 } ? images.MediaTypeOf(href) : "image/png";
        var encoded = ((string?)level.Element(Office + "binary-data"))?.Trim() ?? string.Empty;
        var buffer = new byte[encoded.Length];
        if (encoded.Length > 0 && Convert.TryFromBase64String(encoded, buffer, out var written))
            return new ListBulletPicture(buffer.AsMemory(0, written).ToArray(), mediaType, width, height);
        return href is { Length: > 0 } && !href.Contains("://", StringComparison.Ordinal) && readBytes(href.TrimStart('.', '/')) is { Length: > 0 } data
            ? new ListBulletPicture(data, mediaType, width, height)
            : null;
    }

    private double Side(double? points) => points is { } value && double.IsFinite(value) && value > 0 ? Math.Min(value, LargestSize) : DefaultSize;
}

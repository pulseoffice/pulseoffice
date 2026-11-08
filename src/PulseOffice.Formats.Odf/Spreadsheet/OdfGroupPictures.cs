using System.Xml.Linq;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal interface IOdfPictureStore
{
    string Store(RasterImageObject raster);
}

internal sealed class SheetGroupImageReader : SheetShapeReader
{
    private readonly OdfLinkedPicture _linked = new();
    private readonly OdfImageTypes _types = new();
    private readonly ImageHeader _header = new();
    private readonly OdfVectorPictures _vectors = new();

    public override bool Reads(XElement element) =>
        element.Name == Namespaces.Draw + "frame" && element.Element(Namespaces.Draw + "image") is not null
        && element.Element(Namespaces.Draw + "object") is null && element.Element(Namespaces.Draw + "object-ole") is null;

    public override PlacedObject? Read(XElement element, CellAddress anchor, SheetShapeReadContext context)
    {
        var name = (string?)element.Attribute(Namespaces.Draw + "name") ?? $"image{context.Next()}";
        if (Content(element, context) is not { } raster) return null;
        var size = new DocumentSize(
            context.Units.Parse((string?)element.Attribute(Namespaces.Svg + "width"), 3),
            context.Units.Parse((string?)element.Attribute(Namespaces.Svg + "height"), 3));
        var position = context.Rotation.Read(element, size.Width, size.Height);
        return new PlacedObject
        {
            Id = name,
            Content = raster with { Id = name },
            Anchor = anchor,
            OffsetX = position.X,
            OffsetY = position.Y,
            Size = size,
        };
    }

    private RasterImageObject? Content(XElement frame, SheetShapeReadContext context)
    {
        foreach (var image in frame.Elements(Namespaces.Draw + "image"))
        {
            if ((string?)image.Attribute(Namespaces.Xlink + "href") is not { Length: > 0 } href || context.ReadBytes(href) is not { } bytes) continue;
            var detected = _header.MediaType(bytes);
            if (detected is null && _vectors.Declares(image)) continue;
            var pixels = _header.Size(bytes);
            return new RasterImageObject
            {
                Id = string.Empty,
                Data = bytes,
                MediaType = detected ?? _types.MediaTypeOf(href),
                PixelWidth = pixels?.Width ?? 0,
                PixelHeight = pixels?.Height ?? 0,
            };
        }
        return _linked.Read(frame);
    }
}

internal sealed class OdfVectorPictures
{
    private const string SvgMediaType = "image/svg+xml";

    public bool Declares(XElement image) =>
        ((string?)image.Attribute(Namespaces.Xlink + "href") ?? string.Empty).EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
        || string.Equals((string?)image.Attribute(Namespaces.Draw + "mime-type"), SvgMediaType, StringComparison.OrdinalIgnoreCase);
}

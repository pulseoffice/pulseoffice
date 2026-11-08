using System.Globalization;
using System.Text;
using System.Xml.Linq;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing;
using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

public sealed class OdfImageMapper(SignatureLineImage signatureImages, VectorFallback vectors) : IOdfPictureStore
{
    public OdfImageMapper()
        : this(new SignatureLineImage())
    {
    }

    public OdfImageMapper(SignatureLineImage signatureImages)
        : this(signatureImages, new VectorFallback())
    {
    }

    private readonly List<(string Path, string MediaType, byte[] Content)> _pictures = [];
    private readonly OdfImageTypes _types = new();
    private readonly OdfDrawUnits _units = new();
    private readonly OdfDrawRotation _rotation = new();
    private readonly AnchoredFrames _frames = new();
    private readonly OdfImageMirrors _mirrors = new();
    private readonly ImageHeader _header = new();
    private readonly OdfLinkedPicture _linked = new();
    private readonly OdfSignatureLineCodec _signatures = new(signatureImages);

    public IReadOnlyList<XElement> Declarations => _mirrors.Declarations;

    public IReadOnlyList<(string Path, string MediaType, byte[] Content)> Pictures => _pictures;

    string IOdfPictureStore.Store(RasterImageObject raster)
    {
        if (raster.LinkPath is { Length: > 0 } linked) return new PulseOffice.Formats.LinkedFiles().ToPackageHref(new PulseOffice.Formats.LinkedFiles().ToHref(linked));
        var path = $"Pictures/{_pictures.Count + 1:0000}.{_types.Extension(raster.MediaType)}";
        _pictures.Add((path, raster.MediaType, raster.Data.ToArray()));
        return path;
    }

    public IReadOnlyDictionary<CellAddress, List<XElement>> BuildFrames(Worksheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var frames = new Dictionary<CellAddress, List<XElement>>();
        foreach (var placed in sheet.Objects)
        {
            var frame = placed.Content switch
            {
                PulseOffice.Document.Forms.SignatureLineObject line => SignatureFrame(placed, line),
                IDrawingObject drawing => drawing.Accept(new SheetImageVisitor(placed, _pictures, _types, vectors, _units, _rotation, _mirrors)),
                _ => null,
            };
            if (frame is null) continue;

            if (!frames.TryGetValue(placed.Anchor, out var list)) frames[placed.Anchor] = list = [];
            list.Add(frame);
        }
        return frames;
    }

    public IReadOnlyList<PlacedObject> ReadImages(XElement table, OdfPackage package, XDocument? content = null)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(package);

        var placed = new List<PlacedObject>();
        var ordinal = 0;

        foreach (var (frame, anchor) in _frames.In(table))
        {
            if (frame.Element(Namespaces.Draw + "object") is not null || frame.Element(Namespaces.Draw + "object-ole") is not null) continue;
            if (Preferred(frame, package) is not var (href, bytes))
            {
                if (_linked.Read(frame) is { } linked) placed.Add(Linked(frame, anchor, linked, ++ordinal, placed.Count));
                continue;
            }

            var name = (string?)frame.Attribute(Namespaces.Draw + "name") ?? $"image{++ordinal}";
            var size = new DocumentSize(
                _units.Parse((string?)frame.Attribute(Namespaces.Svg + "width"), 3),
                _units.Parse((string?)frame.Attribute(Namespaces.Svg + "height"), 3));
            var position = _rotation.Read(frame, size.Width, size.Height);
            if (_signatures.Read(frame, name) is { } signature)
            {
                placed.Add(new PlacedObject
                {
                    Id = name,
                    Content = signature,
                    Anchor = anchor,
                    OffsetX = position.X,
                    OffsetY = position.Y,
                    Size = size,
                    ZOrder = _units.Order(frame) is var signatureOrder and >= 0 ? signatureOrder : placed.Count,
                });
                continue;
            }
            var (flipHorizontal, flipVertical) = _mirrors.Read(frame, content);
            var vector = href.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) || IsSvg(bytes);
            var pixels = vector ? null : _header.Size(bytes);

            EmbeddedObject embedded = vector
                ? new VectorGraphicObject
                {
                    Id = name,
                    Asset = new VectorAsset { Id = name, Name = name, SourceKind = VectorSourceKind.InlineSvg, SvgContent = Encoding.UTF8.GetString(bytes) },
                    RotationDegrees = position.Degrees,
                    FlipHorizontal = flipHorizontal,
                    FlipVertical = flipVertical,
                }
                : new RasterImageObject
                {
                    Id = name,
                    Data = bytes,
                    MediaType = _types.MediaTypeOf(href),
                    PixelWidth = pixels?.Width ?? 0,
                    PixelHeight = pixels?.Height ?? 0,
                    Map = new OdfImageMapCodec().Read(frame, size),
                };

            placed.Add(new PlacedObject
            {
                Id = name,
                Content = embedded,
                Anchor = anchor,
                OffsetX = position.X,
                OffsetY = position.Y,
                Size = size,
                ZOrder = _units.Order(frame) is var order and >= 0 ? order : placed.Count,
            });
        }
        return placed;
    }

    private PlacedObject Linked(XElement frame, CellAddress anchor, RasterImageObject linked, int ordinal, int order)
    {
        var name = (string?)frame.Attribute(Namespaces.Draw + "name") ?? $"image{ordinal}";
        var size = new DocumentSize(
            _units.Parse((string?)frame.Attribute(Namespaces.Svg + "width"), 3),
            _units.Parse((string?)frame.Attribute(Namespaces.Svg + "height"), 3));
        var position = _rotation.Read(frame, size.Width, size.Height);
        return new PlacedObject
        {
            Id = name,
            Content = linked with { Id = name, Map = new OdfImageMapCodec().Read(frame, size) },
            Anchor = anchor,
            OffsetX = position.X,
            OffsetY = position.Y,
            Size = size,
            ZOrder = _units.Order(frame) is var linkedOrder and >= 0 ? linkedOrder : order,
        };
    }

    private (string Href, byte[] Bytes)? Preferred(XElement frame, OdfPackage package)
    {
        (string Href, byte[] Bytes)? first = null;
        foreach (var image in frame.Elements(Namespaces.Draw + "image"))
        {
            if ((string?)image.Attribute(Namespaces.Xlink + "href") is not { Length: > 0 } href) continue;
            if (package.ReadBytes(href.TrimStart('.', '/')) is not { } bytes) continue;
            if (vectors.IsSvg(bytes, (string?)image.Attribute(Namespaces.Draw + "mime-type"), href)) return (href, bytes);
            first ??= (href, bytes);
        }
        return first;
    }

    private XElement SignatureFrame(PlacedObject placed, PulseOffice.Document.Forms.SignatureLineObject line)
    {
        var path = $"Pictures/{_pictures.Count + 1:0000}.svg";
        _pictures.Add((path, OdfSignatureLineCodec.MediaType, _signatures.UnsignedImage(line, placed.Size)));
        var fallback = _signatures.UnsignedRaster(line, placed.Size) is { } raster ? $"Pictures/{_pictures.Count + 1:0000}.png" : null;
        if (fallback is not null) _pictures.Add((fallback, _signatures.RasterMediaType, _signatures.UnsignedRaster(line, placed.Size)!));
        var frame = new XElement(Namespaces.Draw + "frame",
            new XAttribute(Namespaces.Draw + "name", placed.Id),
            new XAttribute(Namespaces.Draw + "z-index", placed.ZOrder.ToString(CultureInfo.InvariantCulture)),
            new XAttribute(Namespaces.Svg + "width", _units.Length(placed.Size.Width)),
            new XAttribute(Namespaces.Svg + "height", _units.Length(placed.Size.Height)),
            new XElement(Namespaces.Draw + "image",
                new XAttribute(Namespaces.Xlink + "href", path),
                new XAttribute(Namespaces.Xlink + "type", "simple"),
                new XAttribute(Namespaces.Xlink + "show", "embed"),
                new XAttribute(Namespaces.Xlink + "actuate", "onLoad"),
                new XAttribute(Namespaces.Draw + "mime-type", OdfSignatureLineCodec.MediaType)),
            fallback is null ? null : _signatures.Fallback(fallback),
            line.Title is { } title ? new XElement(Namespaces.Svg + "title", title) : null,
            line.Description is { } description ? new XElement(Namespaces.Svg + "desc", description) : null,
            _signatures.Element(line));
        _rotation.Place(frame, placed.OffsetX, placed.OffsetY, placed.Size.Width, placed.Size.Height, 0);
        return frame;
    }

    private bool IsSvg(byte[] bytes)
    {
        var head = Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 512));
        return head.Contains("<svg", StringComparison.Ordinal);
    }
}

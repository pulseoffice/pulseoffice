using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using A = DocumentFormat.OpenXml.Drawing;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;
using Worksheet = PulseOffice.Sheets.Worksheet;

namespace PulseOffice.Formats.OpenXml;

public sealed class XlsxImageMapper(VectorFallback vectors)
{
    public XlsxImageMapper() : this(new VectorFallback())
    {
    }

    private readonly OoxmlImageTypes _types = new();
    private readonly DrawingML.DrawingDuotone _duotones = new();

    public IEnumerable<OpenXmlCompositeElement> BuildAnchors(DrawingsPart drawings, Worksheet sheet)
    {
        ArgumentNullException.ThrowIfNull(drawings);
        ArgumentNullException.ThrowIfNull(sheet);

        var id = 3000u;

        foreach (var placed in sheet.Objects)
        {
            if (placed.Content is not RasterImageObject raster) continue;

            yield return BuildAnchor(sheet.Layout, placed, RasterBlip(drawings, raster), ++id);
        }

        foreach (var placed in sheet.Objects)
        {
            if (placed.Content is not VectorGraphicObject { Asset.SvgContent.Length: > 0 } vector) continue;

            var svg = vectors.Svg(vector);
            var anchor = BuildAnchor(sheet.Layout, placed, _svgBlips.Blip(drawings, svg, vectors.Png(svg, placed.Size)), ++id);
            var transform = anchor.GetFirstChild<Xdr.Picture>()!.ShapeProperties!.Transform2D!;
            if (vector.RotationDegrees != 0) transform.Rotation = (int)Math.Round(vector.RotationDegrees * 60000) % 21600000;
            if (vector.FlipHorizontal) transform.HorizontalFlip = true;
            if (vector.FlipVertical) transform.VerticalFlip = true;
            yield return anchor;
        }
    }

    public IReadOnlyList<PlacedObject> ReadImages(DrawingsPart drawings, SheetLayout layout)
    {
        ArgumentNullException.ThrowIfNull(drawings);
        ArgumentNullException.ThrowIfNull(layout);

        if (drawings.WorksheetDrawing is not { } drawing) return [];

        var anchors = new XlsxAnchorLayout(layout);
        var placed = new List<PlacedObject>();
        var ordinal = 0;

        foreach (var anchor in drawing.ChildElements.Where(child => child is Xdr.OneCellAnchor or Xdr.TwoCellAnchor or Xdr.AbsoluteAnchor))
        {
            if (anchor.GetFirstChild<Xdr.Picture>() is not { } picture) continue;
            if (picture.BlipFill?.Blip is not { } blip) continue;

            var name = picture.NonVisualPictureProperties?.NonVisualDrawingProperties?.Name?.Value is { Length: > 0 } given
                ? given
                : $"image{++ordinal}";

            if (Content(drawings, blip, picture, name) is not { } content) continue;

            placed.Add(anchors.Place(anchor, new PlacedObject { Id = name, Content = content, Anchor = new CellAddress(0, 0) },
                new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3))));
        }

        return placed;
    }

    public EmbeddedObject? PictureContent(DrawingsPart drawings, Xdr.Picture picture, string name) =>
        picture.BlipFill?.Blip is { } blip ? Content(drawings, blip, picture, name) : null;

    private EmbeddedObject? Content(DrawingsPart drawings, A.Blip blip, Xdr.Picture picture, string name)
    {
        var transform = picture.ShapeProperties?.Transform2D;

        if (SvgOf(drawings, blip) is { } svg)
            return new VectorGraphicObject
            {
                Id = name,
                Asset = new VectorAsset { Id = name, Name = name, SourceKind = VectorSourceKind.InlineSvg, SvgContent = svg },
                RotationDegrees = (transform?.Rotation?.Value ?? 0) / 60000.0,
                FlipHorizontal = transform?.HorizontalFlip?.Value ?? false,
                FlipVertical = transform?.VerticalFlip?.Value ?? false,
            };

        if (blip.Embed?.Value is not { } embed || Part(drawings, embed) is not ImagePart image)
            return blip.Link?.Value is { } link && _externals.Of(drawings, link) is { } external
                   && _links.ToPath(external.Uri.OriginalString) is { Length: > 0 } path
                ? new RasterImageObject { Id = name, Data = ReadOnlyMemory<byte>.Empty, MediaType = _types.MediaTypeOf(path), LinkPath = path, Duotone = _duotones.Read(blip) }
                : null;

        var bytes = Bytes(image);
        var pixels = _header.Size(bytes);
        return new RasterImageObject
        {
            Id = name,
            Data = bytes,
            MediaType = image.ContentType,
            PixelWidth = pixels?.Width ?? 0,
            PixelHeight = pixels?.Height ?? 0,
            Duotone = _duotones.Read(blip),
        };
    }

    private string? SvgOf(DrawingsPart drawings, A.Blip blip) =>
        _svgBlips.Svg(blip, drawings) is { } svg ? System.Text.Encoding.UTF8.GetString(svg) : null;

    private readonly DrawingML.OoxmlSvgBlip _svgBlips = new();

    private OpenXmlPart? Part(DrawingsPart drawings, string id)
    {
        try { return drawings.GetPartById(id); }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private byte[] Bytes(OpenXmlPart part)
    {
        using var stream = part.GetStream();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private readonly ImageHeader _header = new();
    private readonly XlsxCellAnchors _anchors = new();

    public A.Blip RasterBlip(DrawingsPart drawings, RasterImageObject raster)
    {
        A.Blip blip;
        if (raster.LinkPath is { Length: > 0 } linked)
        {
            var href = _links.ToHref(linked);
            blip = new A.Blip { Link = drawings.AddExternalRelationship(ImageRelationship, _targets.Of(href)).Id };
        }
        else
        {
            var part = drawings.AddImagePart(_types.PartOf(raster.MediaType));
            using (var stream = part.GetStream())
                stream.Write(raster.Data.Span);
            blip = new A.Blip { Embed = drawings.GetIdOfPart(part) };
        }
        _duotones.Write(blip, raster.Duotone);
        return blip;
    }

    private const string ImageRelationship = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image";
    private readonly PulseOffice.Formats.LinkedFiles _links = new();
    private readonly ExternalRelationshipIndex _externals = new();
    private readonly ExternalTargetUris _targets = new();

    private Xdr.TwoCellAnchor BuildAnchor(SheetLayout layout, PlacedObject placed, A.Blip blip, uint id)
    {
        var width = Emu.FromTwips(placed.Size.Width).Value;
        var height = Emu.FromTwips(placed.Size.Height).Value;

        var anchor = _anchors.Create(layout, placed);

        anchor.AppendChild(new Xdr.Picture(
            new Xdr.NonVisualPictureProperties(
                new Xdr.NonVisualDrawingProperties { Id = id, Name = placed.Id },
                new Xdr.NonVisualPictureDrawingProperties(new A.PictureLocks { NoChangeAspect = true })),
            new Xdr.BlipFill(
                blip,
                new A.Stretch(new A.FillRectangle())),
            new Xdr.ShapeProperties(
                new A.Transform2D(
                    new A.Offset { X = 0, Y = 0 },
                    new A.Extents { Cx = width, Cy = height }),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })));

        anchor.AppendChild(new Xdr.ClientData());

        return anchor;
    }

}

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using D = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

public sealed class OoxmlImageTypes
{
    private readonly IReadOnlyList<(string MediaType, PartTypeInfo Part)> _types =
        [
            ("image/png", ImagePartType.Png), ("image/jpeg", ImagePartType.Jpeg), ("image/gif", ImagePartType.Gif), ("image/bmp", ImagePartType.Bmp), ("image/tiff", ImagePartType.Tiff),
            ("image/x-emf", ImagePartType.Emf), ("image/emf", ImagePartType.Emf), ("image/x-wmf", ImagePartType.Wmf), ("image/wmf", ImagePartType.Wmf),
        ];

    public PartTypeInfo PartOf(string mediaType) =>
        _types.Where(type => type.MediaType == mediaType).Select(type => type.Part).DefaultIfEmpty(ImagePartType.Png).First();

    public string MediaTypeOf(string path)
    {
        var extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return extension switch
        {
            "jpg" or "jpeg" => "image/jpeg",
            "gif" => "image/gif",
            "bmp" => "image/bmp",
            "tif" or "tiff" => "image/tiff",
            "emf" => "image/x-emf",
            "wmf" => "image/x-wmf",
            _ => "image/png",
        };
    }
}

internal abstract class DeckDrawingCodec
{

    protected D.Transform2D Transform(ObjectElement element) =>
        new(Offset(element.Bounds), Extents(element.Bounds));

    protected D.Offset Offset(DocumentRect bounds) => new() { X = bounds.Left.Value * PulseOffice.Foundation.Units.Emu.PerTwip, Y = bounds.Top.Value * PulseOffice.Foundation.Units.Emu.PerTwip };

    protected D.Extents Extents(DocumentRect bounds) => new() { Cx = bounds.Width.Value * PulseOffice.Foundation.Units.Emu.PerTwip, Cy = bounds.Height.Value * PulseOffice.Foundation.Units.Emu.PerTwip };

    protected DocumentRect Bounds(D.Offset? offset, D.Extents? extents) => new(
        Twips(offset?.X?.Value ?? 0), Twips(offset?.Y?.Value ?? 0), Twips(extents?.Cx?.Value ?? 0), Twips(extents?.Cy?.Value ?? 0));

    private Twip Twips(long emu) => new((int)Math.Clamp(emu / PulseOffice.Foundation.Units.Emu.PerTwip, int.MinValue, int.MaxValue));

    protected int Rotation(double degrees) =>
        double.IsFinite(degrees) ? (int)(((long)Math.Round(degrees % 360 * 60000) % 21600000 + 21600000) % 21600000) : 0;

    protected DocumentRect Bounds(D.Transform2D? transform) => Bounds(transform?.Offset, transform?.Extents);

    protected P.NonVisualShapeProperties ShapeNames(uint id, string name) =>
        new(new P.NonVisualDrawingProperties { Id = id, Name = name }, new P.NonVisualShapeDrawingProperties(), new P.ApplicationNonVisualDrawingProperties());

    protected byte[] ReadAll(OpenXmlPart part)
    {
        using var stream = part.GetStream();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    protected PackagedObjectKind KindOf(string mediaType) =>
        mediaType.StartsWith("video/", StringComparison.Ordinal) || mediaType.StartsWith("audio/", StringComparison.Ordinal)
            ? PackagedObjectKind.Media
            : PackagedObjectKind.File;
}

internal abstract class DeckElementBuilder : DeckDrawingCodec
{
    public abstract bool Builds(EmbeddedObject content);

    public abstract OpenXmlElement Build(ObjectElement element, uint id, SlidePart part);
}

internal sealed class DeckPresetShapeCodec(XlsxShapeStyleMapper styles, IShapePresets presets) : DeckElementBuilder
{
    private readonly DrawingPresetGeometry _geometry = new(presets);
    private readonly DrawingDimensionLines _dimensions = new();

    public override bool Builds(EmbeddedObject content) => content is ShapeObject;

    public override OpenXmlElement Build(ObjectElement element, uint id, SlidePart part) => Build(element, id);

    public OpenXmlElement Build(ObjectElement element, uint id)
    {
        var original = (ShapeObject)element.Content;
        var shape = _dimensions.Fallback(original, new DocumentSize(element.Bounds.Width, element.Bounds.Height));
        var transform = Transform(element);
        if (shape.RotationDegrees != 0) transform.Rotation = Rotation(shape.RotationDegrees);
        if (shape.FlipHorizontal) transform.HorizontalFlip = true;
        if (shape.FlipVertical) transform.VerticalFlip = true;

        OpenXmlElement preset = original.Dimension is { } dimension ? _dimensions.Geometry(shape, dimension, original.Style.Line) : _geometry.Build(shape);
        var properties = new P.ShapeProperties(transform, preset);
        properties.Append(styles.Fill(shape.Style.Fill));
        properties.Append(styles.Line(shape.Style.Line));
        if (styles.Effects(shape.Style) is { } effects) properties.Append(effects);
        if (styles.Scene(shape.Style) is { } scene) properties.Append(scene);
        if (styles.Bevel(shape.Style) is { } bevel) properties.Append(bevel);
        _dimensions.Write(properties, original);

        var paragraph = new D.Paragraph(styles.Paragraph(shape.Style.Text));
        if (!string.IsNullOrEmpty(shape.Text)) paragraph.Append(new D.Run(styles.Run(shape.Style.Text), new D.Text(shape.Text)));

        return new P.Shape(ShapeNames(id, "DeckShape"), properties, new P.TextBody(styles.Body(shape.Style.Text), new D.ListStyle(), paragraph));
    }

    public ObjectElement? Read(P.Shape shape, string id)
    {
        var properties = shape.ShapeProperties;
        var geometry = properties?.GetFirstChild<D.PresetGeometry>();
        if (geometry is null && !_dimensions.Carries(properties)) return null;

        var text = string.Concat(shape.TextBody?.Descendants<D.Text>().Select(t => t.Text) ?? []);
        var read = new ShapeObject
        {
            Id = id,
            Shape = ShapeKind.Rectangle,
            Text = string.IsNullOrEmpty(text) ? null : text,
            Style = styles.Read(properties!, shape.TextBody),
            RotationDegrees = properties!.Transform2D?.Rotation is { } rotation ? rotation / 60000.0 : 0,
            FlipHorizontal = properties.Transform2D?.HorizontalFlip?.Value ?? false,
            FlipVertical = properties.Transform2D?.VerticalFlip?.Value ?? false,
        };
        return new ObjectElement
        {
            Id = id,
            Bounds = Bounds(properties!.Transform2D),
            Content = _dimensions.Carries(properties) ? _dimensions.Restore(read, properties) : _geometry.Shaped(read, geometry!.Preset?.InnerText, geometry),
        };
    }
}

internal sealed class DeckGeometryCodec<TContent>(XlsxShapeStyleMapper styles, string name, Func<TContent, ShapeOutline> outline, Func<TContent, ShapeStyle> style)
    : DeckElementBuilder where TContent : EmbeddedObject
{
    private readonly OoxmlGeometry _geometry = new();

    public string Name => name;

    public override bool Builds(EmbeddedObject content) => content is TContent;

    public override OpenXmlElement Build(ObjectElement element, uint id, SlidePart part)
    {
        var content = (TContent)element.Content;
        var shapeStyle = style(content);
        var properties = new P.ShapeProperties(Transform(element), _geometry.CustomGeometry(outline(content)));
        properties.Append(styles.Fill(shapeStyle.Fill));
        properties.Append(styles.Line(shapeStyle.Line));
        if (styles.Effects(shapeStyle) is { } effects) properties.Append(effects);

        return new P.Shape(ShapeNames(id, name), properties, new P.TextBody(new D.BodyProperties(), new D.ListStyle(), new D.Paragraph()));
    }
}

internal sealed class DeckConnectionShapeReader(XlsxShapeStyleMapper styles, ConnectorPresetPaths paths) : DeckDrawingCodec
{
    public ObjectElement? Read(P.ConnectionShape connection, string id)
    {
        if (connection.ShapeProperties is not { } properties) return null;
        var transform = properties.Transform2D;
        var geometry = properties.GetFirstChild<D.PresetGeometry>();
        var kind = styles.Connector(geometry?.Preset?.InnerText);
        var style = styles.Read(properties, null);

        return new ObjectElement
        {
            Id = id,
            Bounds = Bounds(transform),
            RotationDegrees = transform?.Rotation is { } rotation ? rotation / 60000.0 : 0,
            Content = new ConnectorObject
            {
                Id = id,
                Shape = kind,
                Style = style with { Fill = ShapeFill.None },
                Path = paths.Of(kind, geometry?.Preset?.InnerText, Adjustment(geometry),
                    transform?.HorizontalFlip?.Value ?? false, transform?.VerticalFlip?.Value ?? false),
            },
        };
    }

    private double? Adjustment(D.PresetGeometry? geometry) =>
        geometry?.AdjustValueList?.Elements<D.ShapeGuide>().FirstOrDefault()?.Formula?.Value is { } formula
        && formula.StartsWith("val ", StringComparison.Ordinal)
        && long.TryParse(formula[4..], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value / 100000.0
            : null;
}

internal sealed class ConnectorPresetPaths
{
    public ShapeOutline Of(ConnectorKind kind, string? preset, double? adjustment, bool flipH, bool flipV)
    {
        ShapePoint Map(double x, double y) => new(flipH ? 1 - x : x, flipV ? 1 - y : y);

        var start = Map(0, 0);
        var end = Map(1, 1);
        var middle = adjustment ?? 0.5;
        IReadOnlyList<ShapeSegment> segments = (kind, preset) switch
        {
            (ConnectorKind.Elbow, "bentConnector2") => [new MoveSegment(start), new LineSegment(Map(1, 0)), new LineSegment(end)],
            (ConnectorKind.Elbow, _) => [new MoveSegment(start), new LineSegment(Map(middle, 0)), new LineSegment(Map(middle, 1)), new LineSegment(end)],
            (ConnectorKind.Curved, _) => [new MoveSegment(start), new CubicSegment(Map(middle, 0), Map(middle, 1), end)],
            _ => [new MoveSegment(start), new LineSegment(end)],
        };
        return new ShapeOutline([new ShapeFigure(segments)]);
    }
}

internal sealed class DeckFreeformReader(XlsxShapeStyleMapper styles) : DeckDrawingCodec
{
    private readonly OoxmlGeometry _geometry = new();

    public ObjectElement? Read(P.Shape shape, string id)
    {
        if (shape.ShapeProperties?.GetFirstChild<D.CustomGeometry>() is not { } geometry) return null;

        return new ObjectElement
        {
            Id = id,
            Bounds = Bounds(shape.ShapeProperties.Transform2D),
            Content = new FreeformObject
            {
                Id = id,
                Path = FreeformPath.FromOutline(_geometry.OutlineOf(geometry)),
                Style = styles.Read(shape.ShapeProperties, null),
            },
        };
    }
}

internal sealed class DeckFormulaShapeCodec : DeckElementBuilder
{
    public override bool Builds(EmbeddedObject content) => content is FormulaObject;

    public override OpenXmlElement Build(ObjectElement element, uint id, SlidePart part)
    {
        var formula = (FormulaObject)element.Content;
        return new P.Shape(
            ShapeNames(id, "DeckFormula"),
            new P.ShapeProperties(Transform(element), new D.PresetGeometry { Preset = D.ShapeTypeValues.Rectangle }),
            new P.TextBody(new D.BodyProperties(), new D.ListStyle(),
                new D.Paragraph(new D.Run(
                    new D.RunProperties { FontSize = (int)Math.Round(Math.Clamp(double.IsFinite(formula.FontSize) ? formula.FontSize : 18, 1, 4000) * 100) },
                    new D.Text(formula.Expression)))));
    }

    public ObjectElement? Read(P.Shape shape, string id)
    {
        var expression = string.Concat(shape.TextBody?.Descendants<D.Text>().Select(t => t.Text) ?? []);
        return string.IsNullOrEmpty(expression)
            ? null
            : new ObjectElement { Id = id, Bounds = Bounds(shape.ShapeProperties?.Transform2D), Content = new FormulaObject { Id = id, Expression = expression } };
    }
}

internal sealed class DeckVectorPictureCodec(PulseOffice.Formats.VectorFallback fallback, DrawingML.OoxmlSvgBlip svgBlips) : DeckElementBuilder
{
    public override bool Builds(EmbeddedObject content) => content is VectorGraphicObject;

    public override OpenXmlElement Build(ObjectElement element, uint id, SlidePart part)
    {
        var vector = (VectorGraphicObject)element.Content;
        var svg = fallback.Svg(vector);
        var blip = svgBlips.Blip(part, svg, fallback.Png(svg, new DocumentSize(element.Bounds.Width, element.Bounds.Height)));
        var transform = Transform(element);
        if (vector.FlipHorizontal) transform.HorizontalFlip = true;
        if (vector.FlipVertical) transform.VerticalFlip = true;
        return new P.Picture(
            new P.NonVisualPictureProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = "Picture" },
                new P.NonVisualPictureDrawingProperties(new D.PictureLocks { NoChangeAspect = true }),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.BlipFill(blip, new D.Stretch(new D.FillRectangle())),
            new P.ShapeProperties(transform, new D.PresetGeometry { Preset = D.ShapeTypeValues.Rectangle }));
    }

    public ObjectElement? Read(P.Picture picture, SlidePart part, string id)
    {
        if (picture.BlipFill?.GetFirstChild<D.Blip>() is not { } blip || svgBlips.Svg(blip, part) is not { } svg) return null;
        var transform = picture.ShapeProperties?.Transform2D;
        return new ObjectElement
        {
            Id = id,
            Bounds = Bounds(transform),
            Content = fallback.Read(id, svg, transform?.HorizontalFlip?.Value ?? false, transform?.VerticalFlip?.Value ?? false),
        };
    }
}

internal sealed class DeckPictureCodec(OoxmlImageTypes types, DrawingML.DrawingDuotone duotones) : DeckElementBuilder
{
    private const string ImageRelationship = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image";

    private readonly ExternalRelationshipIndex _externals = new();

    public override bool Builds(EmbeddedObject content) => content is RasterImageObject;

    public override OpenXmlElement Build(ObjectElement element, uint id, SlidePart part)
    {
        var image = (RasterImageObject)element.Content;
        D.Blip blip;
        if (image.LinkPath is { Length: > 0 } linked)
        {
            var href = new PulseOffice.Formats.LinkedFiles().ToHref(linked);
            var target = Uri.TryCreate(href, UriKind.RelativeOrAbsolute, out var parsed) ? parsed : new Uri(Uri.EscapeDataString(href), UriKind.Relative);
            var relationship = part.AddExternalRelationship(ImageRelationship, target);
            blip = new D.Blip { Link = relationship.Id };
        }
        else
        {
            var imagePart = part.AddImagePart(types.PartOf(image.MediaType));
            using (var stream = imagePart.GetStream(FileMode.Create)) stream.Write(image.Data.Span);
            blip = new D.Blip { Embed = part.GetIdOfPart(imagePart) };
        }
        if (image.Filter == ImageFilter.Grayscale) blip.Append(new D.Grayscale());
        duotones.Write(blip, image.Duotone);

        var fill = new P.BlipFill(blip);
        if (image.Crop != default)
            fill.Append(new D.SourceRectangle { Left = Percent(image.Crop.Left), Top = Percent(image.Crop.Top), Right = Percent(image.Crop.Right), Bottom = Percent(image.Crop.Bottom) });
        fill.Append(new D.Stretch(new D.FillRectangle()));

        return new P.Picture(
            new P.NonVisualPictureProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = "Picture" },
                new P.NonVisualPictureDrawingProperties(new D.PictureLocks { NoChangeAspect = true }),
                new P.ApplicationNonVisualDrawingProperties()),
            fill,
            new P.ShapeProperties(Transform(element), new D.PresetGeometry { Preset = D.ShapeTypeValues.Rectangle }));
    }

    public ObjectElement? Read(P.Picture picture, SlidePart part, string id)
    {
        var blip = picture.BlipFill?.GetFirstChild<D.Blip>();
        if (blip is null) return null;

        var links = new PulseOffice.Formats.LinkedFiles();
        string? linkPath = null;
        byte[] data;
        string mediaType;
        if (blip.Embed?.Value is { } embed && part.TryGetPartById(embed, out var embedded) && embedded is ImagePart imagePart)
        {
            data = ReadAll(imagePart);
            mediaType = imagePart.ContentType;
        }
        else if (blip.Link?.Value is { } link && _externals.Of(part, link) is { } external)
        {
            linkPath = links.ToPath(external.Uri.OriginalString);
            data = [];
            mediaType = types.MediaTypeOf(linkPath ?? string.Empty);
        }
        else return null;

        var bounds = Bounds(picture.ShapeProperties?.Transform2D);
        var crop = picture.BlipFill?.GetFirstChild<D.SourceRectangle>() is { } source
            ? (Fraction(source.Left), Fraction(source.Top), Fraction(source.Right), Fraction(source.Bottom))
            : default;

        return new ObjectElement
        {
            Id = id,
            Bounds = bounds,
            Content = new RasterImageObject
            {
                Id = id,
                Data = data,
                MediaType = mediaType,
                LinkPath = linkPath,
                NaturalSize = new DocumentSize(bounds.Width, bounds.Height),
                Filter = blip.GetFirstChild<D.Grayscale>() is not null ? ImageFilter.Grayscale : ImageFilter.None,
                Duotone = duotones.Read(blip),
                Crop = crop,
            },
        };
    }

    private int Percent(double fraction) => (int)Math.Round(Math.Clamp(fraction, 0, 1) * 100000);

    private double Fraction(Int32Value? value) => (value?.Value ?? 0) / 100000.0;
}

internal sealed class OoxmlEmbeddedPackageTypes
{
    private readonly IReadOnlyDictionary<string, string> _byProgId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Excel.Sheet.12"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ["Excel.SheetMacroEnabled.12"] = "application/vnd.ms-excel.sheet.macroEnabled.12",
        ["Word.Document.12"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ["Word.DocumentMacroEnabled.12"] = "application/vnd.ms-word.document.macroEnabled.12",
        ["PowerPoint.Show.12"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ["PowerPoint.Slide.12"] = "application/vnd.openxmlformats-officedocument.presentationml.slide",
    };

    public string? ContentType(PackagedObject package) =>
        package.Data.Length > 1 && package.Data.Span[0] == (byte)'P' && package.Data.Span[1] == (byte)'K'
            ? _byProgId.GetValueOrDefault(package.MediaType) ?? "application/vnd.openxmlformats-officedocument.package"
            : null;
}

internal sealed class DeckOleObjectCodec : DeckDrawingCodec
{
    private const string OleUri = "http://schemas.openxmlformats.org/presentationml/2006/ole";
    private readonly PulseOffice.Formats.OpenXml.Embedding.OoxmlOleReplacement _replacement = new();
    private readonly OoxmlEmbeddedPackageTypes _packages = new();

    public P.GraphicFrame Build(ObjectElement element, PackagedObject package, uint id, SlidePart part)
    {
        OpenXmlPart oleData = _packages.ContentType(package) is { } packageType
            ? part.AddEmbeddedPackagePart(packageType)
            : part.AddNewPart<EmbeddedObjectPart>("application/vnd.openxmlformats-officedocument.oleObject", null);
        using (var stream = oleData.GetStream(FileMode.Create)) stream.Write(package.Data.Span);

        var fallback = part.AddImagePart(ImagePartType.Png);
        var png = _replacement.Picture(package);
        using (var stream = fallback.GetStream(FileMode.Create)) stream.Write(png, 0, png.Length);

        var picture = new P.Picture(
            new P.NonVisualPictureProperties(
                new P.NonVisualDrawingProperties { Id = id + 1000, Name = package.FileName },
                new P.NonVisualPictureDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.BlipFill(new D.Blip { Embed = part.GetIdOfPart(fallback) }, new D.Stretch(new D.FillRectangle())),
            new P.ShapeProperties(Transform(element), new D.PresetGeometry { Preset = D.ShapeTypeValues.Rectangle }));

        var oleObject = new P.OleObject(new P.OleObjectEmbed(), picture)
        {
            Name = package.FileName,
            ProgId = package.MediaType,
            Id = part.GetIdOfPart(oleData),
            ImageWidth = (int)(element.Bounds.Width.Value / 1.44),
            ImageHeight = (int)(element.Bounds.Height.Value / 1.44),
        };

        return new P.GraphicFrame(
            new P.NonVisualGraphicFrameProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = "DeckOle" },
                new P.NonVisualGraphicFrameDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.Transform(Offset(element.Bounds), Extents(element.Bounds)),
            new D.Graphic(new D.GraphicData(oleObject) { Uri = OleUri }));
    }

    public ObjectElement? Read(P.GraphicFrame frame, SlidePart part, string id)
    {
        if (frame.Graphic?.GraphicData?.Descendants<P.OleObject>().FirstOrDefault(candidate => candidate.Id?.Value is { Length: > 0 }) is not { } ole) return null;
        if (ole.Id?.Value is not { } embed || !part.TryGetPartById(embed, out var oleData) || oleData is not (EmbeddedObjectPart or EmbeddedPackagePart)) return null;

        var mediaType = ole.ProgId?.Value ?? "application/octet-stream";
        var bounds = Bounds(frame.Transform?.Offset, frame.Transform?.Extents);
        var replacement = ole.Descendants<D.Blip>().FirstOrDefault()?.Embed?.Value is { } blip && part.TryGetPartById(blip, out var picture) && picture is ImagePart image
            ? _replacement.Replacement(ReadAll(image))
            : null;

        return new ObjectElement
        {
            Id = id,
            Bounds = bounds,
            Content = new PackagedObject
            {
                Id = id,
                Packaged = KindOf(mediaType),
                Replacement = replacement,
                FileName = ole.Name?.Value ?? id,
                MediaType = mediaType,
                Data = ReadAll(oleData),
                NaturalSize = new DocumentSize(bounds.Width, bounds.Height),
            },
        };
    }
}

internal sealed class DeckFontworkCodec(XlsxShapeStyleMapper styles) : DeckElementBuilder
{
    private readonly OoxmlFontworkText _text = new(styles);

    public override bool Builds(EmbeddedObject content) => content is PulseOffice.Drawing.Fontwork.FontworkObject;

    public override OpenXmlElement Build(ObjectElement element, uint id, SlidePart part)
    {
        var fontwork = (PulseOffice.Drawing.Fontwork.FontworkObject)element.Content;
        var properties = new P.ShapeProperties(Transform(element),
            new D.PresetGeometry(new D.AdjustValueList()) { Preset = D.ShapeTypeValues.Rectangle },
            new D.NoFill(), new D.Outline(new D.NoFill()));

        return new P.Shape(ShapeNames(id, "DeckFontwork"), properties,
            new P.TextBody(_text.Body(fontwork), new D.ListStyle(), _text.Paragraph(fontwork)));
    }

    public bool Reads(P.Shape shape) => _text.Reads(shape.TextBody);

    public ObjectElement Read(P.Shape shape, string id) => new()
    {
        Id = id,
        Bounds = Bounds(shape.ShapeProperties?.Transform2D),
        Content = _text.Read(shape.TextBody!, id),
    };
}

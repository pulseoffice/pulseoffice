using System.Globalization;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Charts;
using PulseOffice.Document.Embedding;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Drawing.Assets;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Fontwork;
using PulseOffice.Formats;
using PulseOffice.Foundation.Units;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using W = DocumentFormat.OpenXml.Wordprocessing;
using WPS = DocumentFormat.OpenXml.Office2010.Word.DrawingShape;
using WPG = DocumentFormat.OpenXml.Office2010.Word.DrawingGroup;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public delegate IEnumerable<OpenXmlElement> DocxBlockSink(BlockContainer blocks);

public delegate List<Block> DocxBlockSource(IEnumerable<OpenXmlElement> elements);

public sealed record DocxGraphic(AnchoredContent Content, string? Name, string? Description, double Rotation)
{
    public bool FlipHorizontal { get; init; }

    public bool FlipVertical { get; init; }
}

public abstract class DocxGraphicCodec
{

    public abstract string Uri { get; }

    public abstract bool Writes(AnchoredContent content);

    public abstract OpenXmlElement Write(AnchoredObject item, DocxWriteContext context, DocxBlockSink blocks);

    public abstract bool Reads(OpenXmlElement root);

    public abstract DocxGraphic? Read(OpenXmlElement root, DocxReadContext context, DocxBlockSource blocks, string id);

    public virtual DocxGraphic? ReadMember(OpenXmlElement root, DocxReadContext context, DocxBlockSource blocks, string id) => Read(root, context, blocks, id);

    protected A.Transform2D Transform(AnchoredObject item)
    {
        var transform = new A.Transform2D(
            new A.Offset { X = 0, Y = 0 },
            new A.Extents { Cx = item.Size.Width.Value * PulseOffice.Foundation.Units.Emu.PerTwip, Cy = item.Size.Height.Value * PulseOffice.Foundation.Units.Emu.PerTwip });
        if (item.RotationDegrees != 0) transform.Rotation = (int)Math.Round(item.RotationDegrees * 60000);
        if (item.FlipHorizontal) transform.HorizontalFlip = true;
        if (item.FlipVertical) transform.VerticalFlip = true;
        return transform;
    }

    protected double Rotation(A.Transform2D? transform) => (transform?.Rotation?.Value ?? 0) / 60000.0;

    protected WPS.WordprocessingShape Shape(string? name, OpenXmlElement properties)
    {
        var shape = new WPS.WordprocessingShape();
        if (name is not null) shape.Append(new WPS.NonVisualDrawingProperties { Id = 0U, Name = name });
        shape.Append(new WPS.NonVisualDrawingShapeProperties());
        shape.Append(properties);
        return shape;
    }

    protected string? NameOf(OpenXmlElement root) => root.GetFirstChild<WPS.NonVisualDrawingProperties>()?.Name?.Value;
}

public sealed class DocxPictureCodec(OoxmlImageTypes types, ImageHeader header, VectorFallback vectors) : DocxGraphicCodec
{
    private readonly DrawingML.DrawingDuotone _duotones = new();
    private readonly DrawingML.OoxmlSvgBlip _svgBlips = new();

    public DocxPictureCodec(OoxmlImageTypes types, ImageHeader header) : this(types, header, new VectorFallback())
    {
    }

    public override string Uri => "http://schemas.openxmlformats.org/drawingml/2006/picture";

    public override bool Writes(AnchoredContent content) => content is EmbeddedContent { Value: RasterImageObject or VectorGraphicObject };

    public override OpenXmlElement Write(AnchoredObject item, DocxWriteContext context, DocxBlockSink blocks)
    {
        if (((EmbeddedContent)item.Content).Value is VectorGraphicObject vector) return Vector(item, vector, context);
        var image = (RasterImageObject)((EmbeddedContent)item.Content).Value;
        var part = context.AddImage(types.PartOf(image.MediaType));
        using (var stream = part.GetStream(FileMode.Create)) stream.Write(image.Data.Span);
        var blip = new A.Blip { Embed = context.Part.GetIdOfPart(part) };
        if (image.Filter == ImageFilter.Grayscale) blip.Append(new A.Grayscale());
        _duotones.Write(blip, image.Duotone);
        var fill = new PIC.BlipFill(blip);
        if (image.Crop != default)
            fill.Append(new A.SourceRectangle { Left = Percent(image.Crop.Left), Top = Percent(image.Crop.Top), Right = Percent(image.Crop.Right), Bottom = Percent(image.Crop.Bottom) });
        fill.Append(new A.Stretch(new A.FillRectangle()));
        return new PIC.Picture(
            new PIC.NonVisualPictureProperties(
                new PIC.NonVisualDrawingProperties { Id = 0U, Name = item.Name ?? string.Empty },
                new PIC.NonVisualPictureDrawingProperties(new A.PictureLocks { NoChangeAspect = true })),
            fill,
            new PIC.ShapeProperties(Transform(item), new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }));
    }

    private PIC.Picture Vector(AnchoredObject item, VectorGraphicObject vector, DocxWriteContext context)
    {
        var svg = vectors.Svg(vector);
        var transform = Transform(item);
        if (vector.FlipHorizontal) transform.HorizontalFlip = true;
        if (vector.FlipVertical) transform.VerticalFlip = true;
        return new PIC.Picture(
            new PIC.NonVisualPictureProperties(
                new PIC.NonVisualDrawingProperties { Id = 0U, Name = item.Name ?? string.Empty },
                new PIC.NonVisualPictureDrawingProperties(new A.PictureLocks { NoChangeAspect = true })),
            new PIC.BlipFill(_svgBlips.Blip(context.Part, svg, vectors.Png(svg, item.Size)), new A.Stretch(new A.FillRectangle())),
            new PIC.ShapeProperties(transform, new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }));
    }

    public override bool Reads(OpenXmlElement root) => root is PIC.Picture;

    public override DocxGraphic? Read(OpenXmlElement root, DocxReadContext context, DocxBlockSource blocks, string id)
    {
        if (root is not PIC.Picture picture) return null;
        var blip = picture.BlipFill?.Blip;
        if (blip is not null && _svgBlips.Svg(blip, context.Part) is { } svg)
        {
            var transform = picture.ShapeProperties?.Transform2D;
            return new DocxGraphic(new EmbeddedContent(vectors.Read(id, svg, false, false)),
                picture.NonVisualPictureProperties?.NonVisualDrawingProperties?.Name?.Value is { Length: > 0 } vectorName ? vectorName : null, null, Rotation(transform))
            {
                FlipHorizontal = transform?.HorizontalFlip?.Value ?? false,
                FlipVertical = transform?.VerticalFlip?.Value ?? false,
            };
        }
        if (blip?.Embed?.Value is not { } embed || !context.Part.TryGetPartById(embed, out var found) || found is not ImagePart part) return null;
        using var stream = part.GetStream();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var crop = picture.BlipFill?.SourceRectangle is { } source
            ? (Fraction(source.Left), Fraction(source.Top), Fraction(source.Right), Fraction(source.Bottom))
            : default;
        var bytes = buffer.ToArray();
        var pixels = header.Size(bytes);
        var image = new RasterImageObject
        {
            Id = id,
            Data = bytes,
            PixelWidth = pixels?.Width ?? 0,
            PixelHeight = pixels?.Height ?? 0,
            MediaType = part.ContentType,
            Filter = blip.GetFirstChild<A.Grayscale>() is not null ? ImageFilter.Grayscale : ImageFilter.None,
            Duotone = _duotones.Read(blip),
            Crop = crop,
        };
        return new DocxGraphic(new EmbeddedContent(image), picture.NonVisualPictureProperties?.NonVisualDrawingProperties?.Name?.Value is { Length: > 0 } pictureName ? pictureName : null, null,
            Rotation(picture.ShapeProperties?.Transform2D))
        {
            FlipHorizontal = picture.ShapeProperties?.Transform2D?.HorizontalFlip?.Value ?? false,
            FlipVertical = picture.ShapeProperties?.Transform2D?.VerticalFlip?.Value ?? false,
        };
    }

    private int Percent(double fraction) => (int)Math.Round(Math.Clamp(fraction, 0, 1) * 100000);

    private double Fraction(Int32Value? value) => (value?.Value ?? 0) / 100000.0;
}

public sealed class DocxShapeCodec(XlsxShapeStyleMapper styles, IShapePresets presets, IFrameChains chains, DocxTextBoxLook looks) : DocxGraphicCodec
{
    private readonly DrawingPresetGeometry _presetGeometry = new(presets);
    private readonly DrawingDimensionLines _dimensions = new();

    public override string Uri => "http://schemas.microsoft.com/office/word/2010/wordprocessingShape";

    public override bool Writes(AnchoredContent content) => content is TextBoxContent or EmbeddedContent { Value: ShapeObject };

    public override OpenXmlElement Write(AnchoredObject item, DocxWriteContext context, DocxBlockSink blocks)
    {
        var transform = Transform(item);
        switch (item.Content)
        {
            case TextBoxContent box:
            {
                var shape = new WPS.WordprocessingShape(new WPS.NonVisualDrawingShapeProperties { TextBox = true });
                var boxProperties = new WPS.ShapeProperties(transform,
                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle });
                boxProperties.Append(looks.Write(box.Look));
                shape.Append(boxProperties);
                var chain = chains.Chained(context.Document, item) ? chains.Chain(context.Document, item) : [];
                var position = chain.Select((member, index) => (member, index)).FirstOrDefault(pair => ReferenceEquals(pair.member, item)).index;
                var link = chain.Count == 0 ? (ushort)0 : (ushort)context.Drawings[chain[0].Id];
                if (chain.Count > 0 && position > 0) shape.Append(new WPS.LinkedTextBox { Id = link, Sequence = (ushort)position });
                else shape.Append(new WPS.TextBoxInfo2(new W.TextBoxContent(blocks(box.Content))) { Id = chain.Count > 0 ? link : null });
                var body = new WPS.TextBodyProperties { LeftInset = 0, TopInset = 0, RightInset = 0, BottomInset = 0 };
                if (box.AutoGrowHeight) body.Append(new A.ShapeAutoFit());
                else body.Append(new A.NoAutoFit());
                shape.Append(body);
                return shape;
            }

            default:
            {
                var original = (ShapeObject)((EmbeddedContent)item.Content).Value;
                var value = _dimensions.Fallback(original, new DocumentSize(new Emu(transform.Extents?.Cx?.Value ?? 0).ToTwips(), new Emu(transform.Extents?.Cy?.Value ?? 0).ToTwips()));
                if (value.FlipHorizontal) transform.HorizontalFlip = true;
                if (value.FlipVertical) transform.VerticalFlip = true;
                if (value.RotationDegrees != 0 && item.RotationDegrees == 0) transform.Rotation = (int)Math.Round(value.RotationDegrees * 60000);
                var properties = new WPS.ShapeProperties(transform,
                    original.Dimension is { } dimension ? _dimensions.Geometry(value, dimension, original.Style.Line) : _presetGeometry.Build(value));
                properties.Append(value.Shape is ShapeKind.Line && value.Dimension is null ? new A.NoFill() : styles.Fill(value.Style.Fill));
                properties.Append(styles.Line(value.Style.Line));
                if (styles.Effects(value.Style) is { } effects) properties.Append(effects);
                _dimensions.Write(properties, original);
                var shape = new WPS.WordprocessingShape(new WPS.NonVisualDrawingShapeProperties(), properties);
                if (item.Content.Story is { } story && !Blank(story, context.Document.Objects))
                    shape.Append(new WPS.TextBoxInfo2(new W.TextBoxContent(blocks(story))));
                else if (!string.IsNullOrEmpty(value.Text))
                    shape.Append(new WPS.TextBoxInfo2(new W.TextBoxContent(new W.Paragraph(new W.Run(new W.Text(value.Text) { Space = SpaceProcessingModeValues.Preserve })))));
                var text = styles.Body(value.Style.Text);
                shape.Append(new WPS.TextBodyProperties(text.ChildElements.Select(child => child.CloneNode(true)))
                {
                    Anchor = text.Anchor,
                    LeftInset = text.LeftInset,
                    TopInset = text.TopInset,
                    RightInset = text.RightInset,
                    BottomInset = text.BottomInset,
                    Wrap = text.Wrap,
                });
                return shape;
            }
        }
    }

    public override bool Reads(OpenXmlElement root) => root is WPS.WordprocessingShape;

    private static bool Blank(BlockContainer story, AnchoredObjectCollection objects) =>
        story.Blocks.All(block => block is ParagraphBlock { Length: 0, Marks.Count: 0 } && !objects.AnchoredTo(block.Id).Any());

    private bool Visible(WPS.ShapeProperties? properties) =>
        properties?.GetFirstChild<A.SolidFill>() is { } fill && fill.RgbColorModelHex?.Val?.Value?.Equals("FFFFFF", StringComparison.OrdinalIgnoreCase) != true
        || properties?.GetFirstChild<A.GradientFill>() is not null || properties?.GetFirstChild<A.BlipFill>() is not null
        || properties?.GetFirstChild<A.Outline>() is { } line && line.GetFirstChild<A.NoFill>() is null && line.HasChildren;

    public override DocxGraphic? Read(OpenXmlElement root, DocxReadContext context, DocxBlockSource blocks, string id) => Read(root, context, blocks, id, member: false);

    public override DocxGraphic? ReadMember(OpenXmlElement root, DocxReadContext context, DocxBlockSource blocks, string id) => Read(root, context, blocks, id, member: true);

    private DocxGraphic? Read(OpenXmlElement root, DocxReadContext context, DocxBlockSource blocks, string id, bool member)
    {
        if (root is not WPS.WordprocessingShape shape) return null;
        var properties = shape.GetFirstChild<WPS.ShapeProperties>();
        var content = shape.GetFirstChild<WPS.TextBoxInfo2>()?.TextBoxContent;
        var geometry = properties?.GetFirstChild<A.PresetGeometry>()?.Preset?.InnerText;
        var linked = shape.GetFirstChild<WPS.LinkedTextBox>();
        if (_dimensions.Carries(properties))
            return new DocxGraphic(new EmbeddedContent(_dimensions.Restore(new ShapeObject
            {
                Id = id,
                Shape = ShapeKind.Line,
                Style = Bodied(styles.Read(properties!, null), shape.GetFirstChild<WPS.TextBodyProperties>()),
                FlipHorizontal = properties!.Transform2D?.HorizontalFlip?.Value ?? false,
                FlipVertical = properties.Transform2D?.VerticalFlip?.Value ?? false,
            }, properties), null), null, null, Rotation(properties.Transform2D));
        var isTextBox = !member && (shape.GetFirstChild<WPS.NonVisualDrawingShapeProperties>()?.TextBox?.Value == true
                        || (geometry is null or "rect" && content is not null && properties?.GetFirstChild<A.NoFill>() is not null));
        var holdsTables = content?.Descendants<W.Table>().Any() == true;
        if (holdsTables && !isTextBox && linked is null && geometry is not (null or "rect")) context.Losses.Add("DOCX124");
        isTextBox |= holdsTables;
        var transform = properties?.Transform2D;
        if (isTextBox || linked is not null)
        {
            var box = new TextBoxContent(new BlockContainer(content is null ? [] : blocks(content.ChildElements)))
            {
                AutoGrowHeight = shape.GetFirstChild<WPS.TextBodyProperties>()?.GetFirstChild<A.ShapeAutoFit>() is not null,
                Look = Visible(properties) ? looks.Read(properties) : null,
            };
            if (looks.Approximated(properties)) context.Losses.Add("DOCX184");
            if (linked is { Id.Value: var chainId }) context.TextBoxLinks.Add(new DocxTextBoxLink(chainId, linked.Sequence?.Value ?? 1, id));
            else if (shape.GetFirstChild<WPS.TextBoxInfo2>()?.Id?.Value is { } headId) context.TextBoxLinks.Add(new DocxTextBoxLink(headId, 0, id));
            return new DocxGraphic(box, null, null, Rotation(transform));
        }

        var story = content is null ? null : new BlockContainer(blocks(content.ChildElements));
        var value = _presetGeometry.Shaped(new ShapeObject
        {
            Id = id,
            Shape = ShapeKind.Rectangle,
            Style = Bodied(styles.Read(properties, null), shape.GetFirstChild<WPS.TextBodyProperties>()),
            FlipHorizontal = transform?.HorizontalFlip?.Value ?? false,
            FlipVertical = transform?.VerticalFlip?.Value ?? false,
        }, geometry, properties?.GetFirstChild<A.PresetGeometry>());
        return new DocxGraphic(new EmbeddedContent(value, story is { Blocks.Count: > 0 } ? story : null), null, null, Rotation(transform));
    }

    private ShapeStyle Bodied(ShapeStyle style, WPS.TextBodyProperties? body) => style with
    {
        Text = style.Text with
        {
            Vertical = body?.Anchor?.InnerText is { Length: > 0 } anchor ? styles.Anchor(anchor) : TextVerticalPlacement.Top,
            Inset = body?.LeftInset?.Value is { } inset ? new Twip((int)Math.Round(inset / (double)PulseOffice.Foundation.Units.Emu.PerTwip)) : style.Text.Inset,
        },
    };
}

public sealed class DocxFreeformCodec(XlsxShapeStyleMapper styles, OoxmlVectorPaths paths) : DocxGraphicCodec
{
    public override string Uri => "http://schemas.microsoft.com/office/word/2010/wordprocessingShape";

    public override bool Writes(AnchoredContent content) => content is EmbeddedContent { Value: FreeformObject };

    public override OpenXmlElement Write(AnchoredObject item, DocxWriteContext context, DocxBlockSink blocks)
    {
        var freeform = (FreeformObject)((EmbeddedContent)item.Content).Value;
        var transform = Transform(item);
        if (freeform.RotationDegrees != 0 && item.RotationDegrees == 0) transform.Rotation = (int)Math.Round(freeform.RotationDegrees * 60000);
        var properties = new WPS.ShapeProperties(transform, paths.Geometry(freeform.Path.ToOutline()));
        properties.Append(styles.Fill(freeform.Style.Fill));
        properties.Append(styles.Line(freeform.Style.Line));
        if (styles.Effects(freeform.Style) is { } effects) properties.Append(effects);
        var shape = Shape(null, properties);
        if (item.Content.Story is { Blocks.Count: > 0 } story && story.Blocks.Any(block => block is not ParagraphBlock { Length: 0 }))
            shape.Append(new WPS.TextBoxInfo2(new W.TextBoxContent(blocks(story))));
        shape.Append(new WPS.TextBodyProperties());
        return shape;
    }

    private readonly DrawingDimensionLines _dimensions = new();

    public override bool Reads(OpenXmlElement root) =>
        root is WPS.WordprocessingShape shape && shape.GetFirstChild<WPS.ShapeProperties>() is { } properties
        && properties.GetFirstChild<A.CustomGeometry>() is not null && !_dimensions.Carries(properties);

    public override DocxGraphic? Read(OpenXmlElement root, DocxReadContext context, DocxBlockSource blocks, string id)
    {
        var properties = root.GetFirstChild<WPS.ShapeProperties>();
        if (properties?.GetFirstChild<A.CustomGeometry>() is not { } geometry) return null;
        var content = root.GetFirstChild<WPS.TextBoxInfo2>()?.TextBoxContent;
        var story = content?.Descendants<W.Text>().Any(text => !string.IsNullOrWhiteSpace(text.Text)) == true ? new BlockContainer(blocks(content.ChildElements)) : null;
        var value = new FreeformObject
        {
            Id = id,
            Path = FreeformPath.FromOutline(paths.Outline(geometry)),
            Style = styles.Read(properties, null),
            RotationDegrees = Rotation(properties.Transform2D),
        };
        return new DocxGraphic(new EmbeddedContent(value, story), NameOf(root), null, 0);
    }
}

public sealed class DocxFontworkCodec(FontworkWarps warps) : DocxGraphicCodec
{
    public override string Uri => "http://schemas.microsoft.com/office/word/2010/wordprocessingShape";

    public override bool Writes(AnchoredContent content) => content is EmbeddedContent { Value: FontworkObject };

    public override OpenXmlElement Write(AnchoredObject item, DocxWriteContext context, DocxBlockSink blocks)
    {
        var fontwork = (FontworkObject)((EmbeddedContent)item.Content).Value;
        var shape = new WPS.WordprocessingShape(new WPS.NonVisualDrawingShapeProperties(),
            new WPS.ShapeProperties(Transform(item), new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }, new A.NoFill(), new A.Outline(new A.NoFill())));
        var run = new W.RunProperties(new W.RunFonts { Ascii = fontwork.FontFamily, HighAnsi = fontwork.FontFamily }, new W.Bold { Val = fontwork.Bold }, new W.Italic { Val = fontwork.Italic });
        if (fontwork.Style.Fill.IsVisible) run.Append(new W.Color { Val = fontwork.Style.Fill.ColorHex.TrimStart('#') });
        run.Append(new W.FontSize { Val = "72" });
        shape.Append(new WPS.TextBoxInfo2(new W.TextBoxContent(new W.Paragraph(
            new W.ParagraphProperties(new W.Justification { Val = W.JustificationValues.Center }),
            new W.Run(run, new W.Text(fontwork.Text) { Space = SpaceProcessingModeValues.Preserve })))));
        shape.Append(new WPS.TextBodyProperties(
            new A.PresetTextWarp(new A.AdjustValueList()) { Preset = new EnumValue<A.TextShapeValues>(new A.TextShapeValues(warps.Ooxml(fontwork.Warp))) },
            new A.NormalAutoFit()) { Wrap = A.TextWrappingValues.None });
        return shape;
    }

    public override bool Reads(OpenXmlElement root) =>
        root.GetFirstChild<WPS.TextBodyProperties>()?.GetFirstChild<A.PresetTextWarp>()?.Preset?.InnerText is { } preset && preset != "textNoShape";

    public override DocxGraphic? Read(OpenXmlElement root, DocxReadContext context, DocxBlockSource blocks, string id)
    {
        var warp = root.GetFirstChild<WPS.TextBodyProperties>()?.GetFirstChild<A.PresetTextWarp>()?.Preset?.InnerText;
        var content = root.GetFirstChild<WPS.TextBoxInfo2>()?.TextBoxContent;
        var run = content?.Descendants<W.RunProperties>().FirstOrDefault();
        var colour = run?.GetFirstChild<W.Color>()?.Val?.Value;
        var value = new FontworkObject
        {
            Id = id,
            Text = string.Join(" ", content?.Descendants<W.Paragraph>().Select(paragraph => string.Concat(paragraph.Descendants<W.Text>().Select(text => text.Text))) ?? []),
            Warp = warps.FromOoxml(warp),
            Bold = run?.GetFirstChild<W.Bold>() is { } bold ? bold.Val?.Value ?? true : true,
            Italic = run?.GetFirstChild<W.Italic>() is { } italic && (italic.Val?.Value ?? true),
            FontFamily = run?.GetFirstChild<W.RunFonts>()?.Ascii?.Value ?? "Inter",
            Style = colour is { Length: 6 } ? ShapeStyle.Default with { Fill = ShapeStyle.Default.Fill with { ColorHex = "#" + colour } } : ShapeStyle.Default,
        };
        return new DocxGraphic(new EmbeddedContent(value), null, null, 0);
    }
}

public sealed class DocxChartCodec(InlineChartParts charts) : DocxGraphicCodec
{
    public override string Uri => "http://schemas.openxmlformats.org/drawingml/2006/chart";

    public override bool Writes(AnchoredContent content) => content is EmbeddedContent { Value: ChartDefinition };

    public override OpenXmlElement Write(AnchoredObject item, DocxWriteContext context, DocxBlockSink blocks) =>
        new C.ChartReference { Id = charts.Write(context.Part, (ChartDefinition)((EmbeddedContent)item.Content).Value) };

    public override bool Reads(OpenXmlElement root) => root is C.ChartReference;

    public override DocxGraphic? Read(OpenXmlElement root, DocxReadContext context, DocxBlockSource blocks, string id) =>
        charts.Read(context.Part, (root as C.ChartReference)?.Id?.Value, id) is { } chart
            ? new DocxGraphic(new EmbeddedContent(chart), null, null, 0)
            : null;
}

public sealed class DocxGroupCodec(IReadOnlyList<DocxGraphicCodec> leaves, OoxmlVectorPaths paths, IPortableObjects portable, IGroupStories groupStories, DocxGroupMemberLinks memberLinks) : DocxGraphicCodec
{
    private const string VectorPrefix = "pulsevector#";

    public override string Uri => "http://schemas.microsoft.com/office/word/2010/wordprocessingGroup";

    public override bool Writes(AnchoredContent content) =>
        content is EmbeddedContent { Value: VectorGraphicObject } || content is EmbeddedContent { Value: ShapeGroupObject group } && Writable(group);

    public bool Complete(AnchoredContent content) =>
        content is not EmbeddedContent { Value: ShapeGroupObject group } || Whole(group);

    private bool Writable(ShapeGroupObject group) =>
        group.Children.Any(child => child.Content is ShapeGroupObject nested ? Writable(nested) : Leaf(child.Content) is not null);

    private bool Whole(ShapeGroupObject group) =>
        group.Children.All(child => child.Content is ShapeGroupObject nested ? Whole(nested) : Leaf(child.Content) is not null);

    private void Members(OpenXmlCompositeElement group, ShapeGroupObject shapes, long width, long height, IReadOnlyDictionary<string, BlockContainer> stories, DocxWriteContext context, DocxBlockSink blocks)
    {
        foreach (var child in shapes.Children)
        {
            var (x, y, w, h) = child.Scaled(width, height);
            if (child.Content is ShapeGroupObject nested)
            {
                if (!Writable(nested)) continue;
                var inner = new A.TransformGroup(
                    new A.Offset { X = (long)x, Y = (long)y }, new A.Extents { Cx = (long)w, Cy = (long)h },
                    new A.ChildOffset { X = 0, Y = 0 }, new A.ChildExtents { Cx = (long)w, Cy = (long)h });
                var rotation = nested.RotationDegrees + child.RotationDegrees;
                if (rotation != 0) inner.Rotation = (int)Math.Round(rotation * 60000);
                var shape = new WPG.GroupShape(new WPG.NonVisualGroupDrawingShapeProperties(), new WPG.GroupShapeProperties(inner));
                Members(shape, nested, (long)w, (long)h, stories, context, blocks);
                group.Append(shape);
                continue;
            }

            if (Leaf(child.Content) is not { } codec) continue;
            var synthetic = new AnchoredObject(child.Content.Id, new AsCharacterAnchor(string.Empty), groupStories.Child(child.Content, stories),
                new DocumentSize(new Twip((int)(w / PulseOffice.Foundation.Units.Emu.PerTwip)), new Twip((int)(h / PulseOffice.Foundation.Units.Emu.PerTwip)))) { RotationDegrees = child.RotationDegrees };
            var element = codec.Write(synthetic, context, blocks);
            if (codec is DocxChartCodec)
                element = new WPG.GraphicFrame(
                    new WPG.NonVisualDrawingProperties { Id = 0U, Name = child.Content.Id },
                    new WPG.NonVisualGraphicFrameProperties(),
                    new WPG.Transform2D(new A.Offset { X = (long)x, Y = (long)y }, new A.Extents { Cx = (long)w, Cy = (long)h }),
                    new A.Graphic(new A.GraphicData(element) { Uri = codec.Uri }));
            else if (element.Descendants<A.Transform2D>().FirstOrDefault() is { } childTransform)
            {
                childTransform.Offset = new A.Offset { X = (long)x, Y = (long)y };
                childTransform.Extents = new A.Extents { Cx = (long)w, Cy = (long)h };
            }

            if (child.Hyperlink is { Length: > 0 } link) memberLinks.Write(element, context.Link(link));
            group.Append(element);
        }
    }

    public override OpenXmlElement Write(AnchoredObject item, DocxWriteContext context, DocxBlockSink blocks)
    {
        var width = item.Size.Width.Value * PulseOffice.Foundation.Units.Emu.PerTwip;
        var height = item.Size.Height.Value * PulseOffice.Foundation.Units.Emu.PerTwip;
        var transform = new A.TransformGroup(
            new A.Offset { X = 0, Y = 0 }, new A.Extents { Cx = width, Cy = height },
            new A.ChildOffset { X = 0, Y = 0 }, new A.ChildExtents { Cx = width, Cy = height });
        var group = new WPG.WordprocessingGroup(new WPG.NonVisualGroupDrawingShapeProperties(), new WPG.GroupShapeProperties(transform));
        switch (((EmbeddedContent)item.Content).Value)
        {
            case VectorGraphicObject vector:
                if (vector.RotationDegrees != 0) transform.Rotation = (int)Math.Round(vector.RotationDegrees * 60000);
                if (vector.FlipHorizontal) transform.HorizontalFlip = true;
                if (vector.FlipVertical) transform.VerticalFlip = true;
                var index = 0;
                foreach (var path in paths.Paths(vector))
                {
                    var properties = new WPS.ShapeProperties(
                        new A.Transform2D(new A.Offset { X = 0, Y = 0 }, new A.Extents { Cx = width, Cy = height }),
                        paths.Geometry(path.Outline));
                    properties.Append(paths.Paint(path.Paint, width, height));
                    var shape = Shape(VectorPrefix + index++.ToString(CultureInfo.InvariantCulture), properties);
                    shape.Append(new WPS.TextBodyProperties());
                    group.Append(shape);
                }

                break;
            case ShapeGroupObject shapes:
                if (shapes.RotationDegrees != 0) transform.Rotation = (int)Math.Round(shapes.RotationDegrees * 60000);
                Members(group, shapes, width, height, ((EmbeddedContent)item.Content).MemberStories, context, blocks);
                break;
        }

        return group;
    }

    public override bool Reads(OpenXmlElement root) => root is WPG.WordprocessingGroup or WPG.GroupShape;

    public override DocxGraphic? Read(OpenXmlElement root, DocxReadContext context, DocxBlockSource blocks, string id)
    {
        if (root is not (WPG.WordprocessingGroup or WPG.GroupShape)) return null;
        var group = root;
        var transform = group.GetFirstChild<WPG.GroupShapeProperties>()?.TransformGroup;
        var children = group.ChildElements.Where(child => child is WPS.WordprocessingShape or PIC.Picture or WPG.WordprocessingGroup or WPG.GroupShape or WPG.GraphicFrame).ToList();
        if (children.Count > 0 && children.All(child => child is WPS.WordprocessingShape && NameOf(child)?.StartsWith(VectorPrefix, StringComparison.Ordinal) == true))
        {
            var vectorPaths = children.Select(child => child.GetFirstChild<WPS.ShapeProperties>()).OfType<WPS.ShapeProperties>()
                .Where(properties => properties.GetFirstChild<A.CustomGeometry>() is not null)
                .Select(properties => new VectorPath(paths.Outline(properties.GetFirstChild<A.CustomGeometry>()!), paths.PaintOf(properties)))
                .ToList();
            var vector = new VectorGraphicObject
            {
                Id = id,
                Asset = new VectorAsset { Id = id, Name = id, SourceKind = VectorSourceKind.InlineSvg, SvgContent = paths.Svg(vectorPaths) },
                RotationDegrees = Rotation(transform),
                FlipHorizontal = transform?.HorizontalFlip?.Value ?? false,
                FlipVertical = transform?.VerticalFlip?.Value ?? false,
            };
            return new DocxGraphic(new EmbeddedContent(vector), null, null, 0);
        }

        var offsetX = transform?.ChildOffset?.X?.Value ?? 0;
        var offsetY = transform?.ChildOffset?.Y?.Value ?? 0;
        var extentX = Math.Max(1, transform?.ChildExtents?.Cx?.Value ?? transform?.Extents?.Cx?.Value ?? 1);
        var extentY = Math.Max(1, transform?.ChildExtents?.Cy?.Value ?? transform?.Extents?.Cy?.Value ?? 1);
        var members = new List<GroupChild>();
        var stories = new Dictionary<string, BlockContainer>(StringComparer.Ordinal);
        foreach (var child in children)
        {
            var childId = context.Document.NextId("obj");
            var source = child is WPG.GraphicFrame frame ? frame.Graphic?.GraphicData?.FirstChild : child;
            var codec = child is WPG.WordprocessingGroup or WPG.GroupShape ? this : source is null ? null : leaves.FirstOrDefault(candidate => candidate.Reads(source));
            var found = source is null ? null : codec?.ReadMember(source, context, blocks, childId);
            if (found is { Content: TextBoxContent textBox }) found = found with { Content = groupStories.Member(childId, textBox) };
            if (found is not { Content: EmbeddedContent embedded } read)
            {
                context.Losses.Add("DOCX102");
                continue;
            }
            groupStories.Collect(embedded, stories);

            var nested = child is WPG.WordprocessingGroup or WPG.GroupShape;
            var box = nested ? null : child.Descendants<A.Transform2D>().FirstOrDefault();
            var frameBox = (child as WPG.GraphicFrame)?.Transform2D;
            var innerTransform = nested ? child.GetFirstChild<WPG.GroupShapeProperties>()?.TransformGroup : null;
            var x = box?.Offset?.X?.Value ?? frameBox?.Offset?.X?.Value ?? innerTransform?.Offset?.X?.Value ?? 0;
            var y = box?.Offset?.Y?.Value ?? frameBox?.Offset?.Y?.Value ?? innerTransform?.Offset?.Y?.Value ?? 0;
            var w = box?.Extents?.Cx?.Value ?? frameBox?.Extents?.Cx?.Value ?? innerTransform?.Extents?.Cx?.Value ?? extentX;
            var h = box?.Extents?.Cy?.Value ?? frameBox?.Extents?.Cy?.Value ?? innerTransform?.Extents?.Cy?.Value ?? extentY;
            members.Add(new GroupChild(portable.Portable(embedded) ?? embedded.Value, (x - offsetX) / (double)extentX, (y - offsetY) / (double)extentY, w / (double)extentX, h / (double)extentY)
            {
                RotationDegrees = read.Rotation,
                Hyperlink = memberLinks.Read(child) is { } relationship ? context.Link(relationship) : null,
            });
        }

        return members.Count == 0
            ? null
            : new DocxGraphic(new EmbeddedContent(new ShapeGroupObject { Id = id, Children = members, RotationDegrees = Rotation(transform) }, null, stories), null, null, 0);
    }

    private double Rotation(A.TransformGroup? transform) => (transform?.Rotation?.Value ?? 0) / 60000.0;

    private DocxGraphicCodec? Leaf(EmbeddedObject content) =>
        leaves.FirstOrDefault(codec => codec.Writes(new EmbeddedContent(content)));
}

public sealed class DocxDrawingCodec(IEnumerable<DocxGraphicCodec> codecs, IEnumerable<DocxInlineObjectCodec> inline, DocxValues values)
{
    private readonly IReadOnlyList<DocxInlineObjectCodec> _inline = [.. inline];

    private const string AnchorExtension = "urn:pulseoffice:docx:anchor";
    private const long WrapPolygonSpan = 21600;
    private static readonly XNamespace Pulse = "urn:pulseoffice:docx";

    private readonly IReadOnlyList<DocxGraphicCodec> _codecs = [.. codecs];

    private readonly IReadOnlyList<(HorizontalRelation Relation, DW.HorizontalRelativePositionValues Word)> _horizontal =
    [
        (HorizontalRelation.Paragraph, DW.HorizontalRelativePositionValues.Column),
        (HorizontalRelation.PageArea, DW.HorizontalRelativePositionValues.Margin),
        (HorizontalRelation.Page, DW.HorizontalRelativePositionValues.Page),
        (HorizontalRelation.Margin, DW.HorizontalRelativePositionValues.LeftMargin),
        (HorizontalRelation.Character, DW.HorizontalRelativePositionValues.Character),
        (HorizontalRelation.Column, DW.HorizontalRelativePositionValues.Column),
    ];

    private readonly IReadOnlyList<(VerticalRelation Relation, DW.VerticalRelativePositionValues Word)> _vertical =
    [
        (VerticalRelation.Paragraph, DW.VerticalRelativePositionValues.Paragraph),
        (VerticalRelation.PageArea, DW.VerticalRelativePositionValues.Margin),
        (VerticalRelation.Page, DW.VerticalRelativePositionValues.Page),
        (VerticalRelation.Margin, DW.VerticalRelativePositionValues.TopMargin),
        (VerticalRelation.Line, DW.VerticalRelativePositionValues.Line),
        (VerticalRelation.Character, DW.VerticalRelativePositionValues.Line),
    ];

    public bool Writes(AnchoredObject item) =>
        _inline.Any(codec => codec.Writes(item.Content))
        || _codecs.Any(codec => codec.Writes(item.Content) && (codec is not DocxGroupCodec group || group.Complete(item.Content)));

    public bool Inline(AnchoredObject item) => _inline.Any(codec => codec.Writes(item.Content) && !codec.Positions);

    public OpenXmlElement? Write(AnchoredObject item, DocxWriteContext context, DocxBlockSink blocks)
    {
        if (_inline.FirstOrDefault(codec => codec.Writes(item.Content)) is { } inline) return inline.Write(item, context);
        if (_codecs.FirstOrDefault(codec => codec.Writes(item.Content)) is not { } codec) return null;
        var graphic = new A.Graphic(new A.GraphicData(codec.Write(item, context, blocks)) { Uri = codec.Uri });
        var id = (uint)context.Drawings[item.Id];
        var properties = new DW.DocProperties { Id = id, Name = item.Name ?? "Object " + values.Text((int)id) };
        if (item.Content is EmbeddedContent { Value.Description: { } description }) properties.Description = description;
        if (item.Content is EmbeddedContent { Value.Title: { } title }) properties.Title = title;
        if (item.Hyperlink is { Length: > 0 } link) properties.Append(new A.HyperlinkOnClick { Id = context.Link(link) });
        if (item.Anchor is PageAnchor or ParagraphAnchor || item.WrapFirstParagraph && item.Anchor is not AsCharacterAnchor) properties.Append(Marker(item.Anchor, item.WrapFirstParagraph));
        var extent = new DW.Extent { Cx = item.Size.Width.Value * PulseOffice.Foundation.Units.Emu.PerTwip, Cy = item.Size.Height.Value * PulseOffice.Foundation.Units.Emu.PerTwip };
        var spacing = item.WrapSpacing;
        if (item.Anchor is AsCharacterAnchor)
            return new W.Run(new W.Drawing(new DW.Inline(extent, new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 }, properties,
                new DW.NonVisualGraphicFrameDrawingProperties(), graphic)
            {
                DistanceFromTop = (uint)Emu(spacing.Top), DistanceFromBottom = (uint)Emu(spacing.Bottom),
                DistanceFromLeft = (uint)Emu(spacing.Left), DistanceFromRight = (uint)Emu(spacing.Right),
            }));

        var horizontal = _horizontal.First(pair => pair.Relation == item.Placement.Horizontal).Word;
        var vertical = _vertical.First(pair => pair.Relation == item.Placement.Vertical).Word;
        var anchor = new DW.Anchor(
            new DW.SimplePosition { X = 0, Y = 0 },
            new DW.HorizontalPosition(new DW.PositionOffset(values.Text((int)Emu(item.Placement.X)))) { RelativeFrom = horizontal },
            new DW.VerticalPosition(new DW.PositionOffset(values.Text((int)Emu(item.Placement.Y)))) { RelativeFrom = vertical },
            extent,
            new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
            Wrap(item),
            properties,
            new DW.NonVisualGraphicFrameDrawingProperties(),
            graphic)
        {
            DistanceFromTop = (uint)Emu(spacing.Top), DistanceFromBottom = (uint)Emu(spacing.Bottom),
            DistanceFromLeft = (uint)Emu(spacing.Left), DistanceFromRight = (uint)Emu(spacing.Right),
            SimplePos = false,
            RelativeHeight = (uint)Math.Max(0, item.ZOrder + 1) * 1024,
            BehindDoc = item.InBackground,
            Locked = false,
            LayoutInCell = true,
            AllowOverlap = true,
        };
        return new W.Run(new W.Drawing(anchor));
    }

    private string? Named(DW.DocProperties? properties) =>
        properties?.Name?.Value is { Length: > 0 } name && name != "Object " + values.Text((int)(properties.Id?.Value ?? 0)) ? name : null;

    public AnchoredObject? Read(W.Drawing drawing, DocxReadContext context, DocxBlockSource blocks, string paragraphId)
    {
        var inline = drawing.Inline;
        var anchor = drawing.Anchor;
        var data = (OpenXmlElement?)inline ?? anchor;
        if (data?.Descendants<A.GraphicData>().FirstOrDefault() is not { } graphicData) return null;
        var id = context.Document.NextId("obj");
        var root = graphicData.ChildElements.FirstOrDefault(child => _codecs.Any(codec => codec.Reads(child)));
        var graphic = root is null ? null : _codecs.Where(codec => codec.Reads(root)).Select(codec => codec.Read(root, context, blocks, id)).FirstOrDefault(found => found is not null);
        if (graphic is null)
        {
            context.Losses.Add("DOCX102");
            return null;
        }

        var extent = inline?.Extent ?? anchor?.Extent;
        var size = new DocumentSize(Length(extent?.Cx?.Value ?? 0), Length(extent?.Cy?.Value ?? 0));
        var properties = inline?.DocProperties ?? anchor?.GetFirstChild<DW.DocProperties>();
        ObjectAnchor objectAnchor = inline is not null ? new AsCharacterAnchor(paragraphId) : Marker(properties, paragraphId) ?? new CharacterAnchor(paragraphId);
        var placed = Placed(graphic, id, objectAnchor, size, inline, anchor, 0, 0, context);
        if (anchor is not null) placed.WrapFirstParagraph = WrapsFirstParagraph(properties);
        return placed;
    }

    private AnchoredObject Placed(DocxGraphic graphic, string id, ObjectAnchor objectAnchor, DocumentSize size, DW.Inline? inline, DW.Anchor? anchor, double shiftX, double shiftY, DocxReadContext context)
    {
        var properties = inline?.DocProperties ?? anchor?.GetFirstChild<DW.DocProperties>();
        var content = graphic.Content;
        if (content is EmbeddedContent embedded)
            content = embedded.With(embedded.Value with { Description = properties?.Description?.Value, Title = properties?.Title?.Value is { Length: > 0 } title ? title : embedded.Value.Title, NaturalSize = size });
        var source = (OpenXmlElement?)inline ?? anchor!;
        var turned = graphic.FlipHorizontal && Math.Abs(graphic.Rotation - 180) < 0.01;
        var item = new AnchoredObject(id, objectAnchor, content, size)
        {
            Name = Named(properties) ?? graphic.Name,
            RotationDegrees = turned ? 0 : graphic.Rotation,
            FlipHorizontal = !turned && graphic.FlipHorizontal,
            FlipVertical = turned ? !graphic.FlipVertical : graphic.FlipVertical,
            WrapSpacing = new Margins(Length(Distance(source, "distL")), Length(Distance(source, "distT")), Length(Distance(source, "distR")), Length(Distance(source, "distB"))),
            Hyperlink = properties?.GetFirstChild<A.HyperlinkOnClick>()?.Id?.Value is { Length: > 0 } relationship ? context.Link(relationship) : null,
        };
        if (anchor is not null)
        {
            var horizontal = anchor.HorizontalPosition;
            var vertical = anchor.VerticalPosition;
            item.Placement = new ObjectPlacement(
                Twip((values.Integer(horizontal?.PositionOffset?.Text) ?? 0) + shiftX),
                Twip((values.Integer(vertical?.PositionOffset?.Text) ?? 0) + shiftY))
            {
                Horizontal = _horizontal.FirstOrDefault(pair => pair.Word == horizontal?.RelativeFrom?.Value, (HorizontalRelation.Paragraph, default)).Relation,
                Vertical = _vertical.FirstOrDefault(pair => pair.Word == vertical?.RelativeFrom?.Value, (VerticalRelation.Paragraph, default)).Relation,
            };
            item.InBackground = anchor.BehindDoc?.Value ?? false;
            item.ZOrder = (int)((anchor.RelativeHeight?.Value ?? 1024) / 1024) - 1;
            item.Wrap = Wrap(anchor);
            var polygon = anchor.GetFirstChild<DW.WrapTight>()?.WrapPolygon ?? anchor.GetFirstChild<DW.WrapThrough>()?.WrapPolygon;
            item.WrapContour = anchor.GetFirstChild<DW.WrapTight>() is not null || anchor.GetFirstChild<DW.WrapThrough>() is not null;
            item.Contour = Contour(polygon);
        }

        return item;
    }

    private long Emu(Twip value) => value.Value * PulseOffice.Foundation.Units.Emu.PerTwip;

    private const int LargestLength = 633_600;

    private Twip Twip(double emu) => new((int)Math.Clamp(Math.Round(emu / PulseOffice.Foundation.Units.Emu.PerTwip), -LargestLength, LargestLength));

    private Twip Length(double emu) => new((int)Math.Clamp(Math.Round(emu / PulseOffice.Foundation.Units.Emu.PerTwip), 0, LargestLength));

    private long Distance(OpenXmlElement element, string name) =>
        long.TryParse(element.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == name).Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private OpenXmlElement Wrap(AnchoredObject item)
    {
        if (!item.WrapContour || item.Wrap is WrapMode.Through or WrapMode.None or WrapMode.TopAndBottom) return Square(item);
        var text = item.Wrap switch
        {
            WrapMode.Left => DW.WrapTextValues.Left,
            WrapMode.Right => DW.WrapTextValues.Right,
            WrapMode.Optimal => DW.WrapTextValues.Largest,
            _ => DW.WrapTextValues.BothSides,
        };
        var points = item.Contour?.Points is { Count: >= 3 } outline ? outline : [new(0, 0), new(0, 1), new(1, 1), new(1, 0)];
        var polygon = new DW.WrapPolygon(new DW.StartPoint { X = Unit(points[0].X), Y = Unit(points[0].Y) }) { Edited = item.Contour is not null };
        foreach (var point in points.Skip(1).Append(points[0])) polygon.Append(new DW.LineTo { X = Unit(point.X), Y = Unit(point.Y) });
        return new DW.WrapTight(polygon) { WrapText = text };
    }

    private long Unit(double fraction) => (long)Math.Round(Math.Clamp(fraction, 0, 1) * WrapPolygonSpan);

    private ObjectContour? Contour(DW.WrapPolygon? polygon)
    {
        if (polygon is null || polygon.Edited?.Value != true) return null;
        var points = polygon.ChildElements.Select(child => child switch
        {
            DW.StartPoint start => new ContourPoint((start.X?.Value ?? 0) / (double)WrapPolygonSpan, (start.Y?.Value ?? 0) / (double)WrapPolygonSpan),
            DW.LineTo line => new ContourPoint((line.X?.Value ?? 0) / (double)WrapPolygonSpan, (line.Y?.Value ?? 0) / (double)WrapPolygonSpan),
            _ => (ContourPoint?)null,
        }).OfType<ContourPoint>().Select(point => new ContourPoint(Math.Round(point.X, 4), Math.Round(point.Y, 4))).ToList();
        if (points.Count > 1 && points[^1] == points[0]) points.RemoveAt(points.Count - 1);
        return points.Count >= 3 ? new ObjectContour(points) : null;
    }

    private OpenXmlElement Square(AnchoredObject item) => item.Wrap switch
    {
        WrapMode.Parallel => new DW.WrapSquare { WrapText = DW.WrapTextValues.BothSides },
        WrapMode.Left => new DW.WrapSquare { WrapText = DW.WrapTextValues.Left },
        WrapMode.Right => new DW.WrapSquare { WrapText = DW.WrapTextValues.Right },
        WrapMode.Optimal => new DW.WrapSquare { WrapText = DW.WrapTextValues.Largest },
        WrapMode.Through => new DW.WrapNone(),
        _ => new DW.WrapTopBottom(),
    };

    private WrapMode Wrap(DW.Anchor anchor)
    {
        if (anchor.GetFirstChild<DW.WrapNone>() is not null) return WrapMode.Through;
        if (anchor.GetFirstChild<DW.WrapTopBottom>() is not null) return WrapMode.TopAndBottom;
        var text = anchor.GetFirstChild<DW.WrapSquare>()?.WrapText?.Value
                   ?? anchor.GetFirstChild<DW.WrapTight>()?.WrapText?.Value
                   ?? anchor.GetFirstChild<DW.WrapThrough>()?.WrapText?.Value;
        return text == DW.WrapTextValues.Left ? WrapMode.Left
            : text == DW.WrapTextValues.Right ? WrapMode.Right
            : text == DW.WrapTextValues.Largest ? WrapMode.Optimal
            : WrapMode.Parallel;
    }

    private A.NonVisualDrawingPropertiesExtensionList Marker(ObjectAnchor anchor, bool wrapFirst)
    {
        var element = new XElement(Pulse + "anchor",
            new XAttribute("type", anchor switch { PageAnchor => "page", ParagraphAnchor => "paragraph", _ => "character" }),
            anchor is PageAnchor page ? new XAttribute("page", page.PageNumber) : null,
            wrapFirst ? new XAttribute("wrap-first-paragraph", "1") : null);
        var extension = new A.NonVisualDrawingPropertiesExtension { Uri = AnchorExtension };
        extension.InnerXml = element.ToString(SaveOptions.DisableFormatting);
        return new A.NonVisualDrawingPropertiesExtensionList(extension);
    }

    private ObjectAnchor? Marker(DW.DocProperties? properties, string paragraphId)
    {
        var extension = properties?.Descendants<A.NonVisualDrawingPropertiesExtension>().FirstOrDefault(item => item.Uri?.Value == AnchorExtension);
        if (extension is null) return null;
        XElement element;
        try { element = XElement.Parse(extension.InnerXml); }
        catch (System.Xml.XmlException) { return new ParagraphAnchor(paragraphId); }
        return (string?)element.Attribute("type") switch
        {
            "page" => new PageAnchor(int.TryParse((string?)element.Attribute("page"), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var page) && page > 0 ? page : 1),
            "character" => null,
            _ => new ParagraphAnchor(paragraphId),
        };
    }

    private bool WrapsFirstParagraph(DW.DocProperties? properties)
    {
        var extension = properties?.Descendants<A.NonVisualDrawingPropertiesExtension>().FirstOrDefault(item => item.Uri?.Value == AnchorExtension);
        if (extension is null) return false;
        try { return (string?)XElement.Parse(extension.InnerXml).Attribute("wrap-first-paragraph") == "1"; }
        catch (System.Xml.XmlException) { return false; }
    }
}

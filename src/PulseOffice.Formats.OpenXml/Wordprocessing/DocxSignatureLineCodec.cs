using System.Globalization;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Document.Forms;
using PulseOffice.Documents.Objects;
using PulseOffice.Foundation.Units;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxSignatureLineCodec(VmlSignatureLineCodec vml, VmlStyle styles, DocxValues values) : DocxInlineObjectCodec
{
    private readonly XNamespace _word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private readonly XNamespace _wordVml = "urn:schemas-microsoft-com:office:word";

    private readonly IReadOnlyList<(HorizontalRelation Relation, string Word)> _horizontal =
    [
        (HorizontalRelation.Page, "page"),
        (HorizontalRelation.PageArea, "margin"),
        (HorizontalRelation.Margin, "left-margin-area"),
        (HorizontalRelation.Character, "char"),
        (HorizontalRelation.Paragraph, "text"),
        (HorizontalRelation.Column, "text"),
    ];

    private readonly IReadOnlyList<(VerticalRelation Relation, string Word)> _vertical =
    [
        (VerticalRelation.Page, "page"),
        (VerticalRelation.PageArea, "margin"),
        (VerticalRelation.Margin, "top-margin-area"),
        (VerticalRelation.Line, "line"),
        (VerticalRelation.Paragraph, "text"),
        (VerticalRelation.Character, "line"),
    ];

    public override bool Positions => true;

    public override bool Writes(AnchoredContent content) => content is EmbeddedContent { Value: SignatureLineObject };

    public bool Reads(W.Picture picture) => Shape(XElement.Parse(picture.OuterXml)) is not null;

    public override OpenXmlElement Write(AnchoredObject item, DocxWriteContext context)
    {
        var line = (SignatureLineObject)((EmbeddedContent)item.Content).Value;
        var vector = context.AddImage(ImagePartType.Svg);
        Fill(vector, vml.UnsignedImage(line, item.Size));
        var image = vector;
        if (vml.UnsignedRaster(line, item.Size) is { } raster)
        {
            image = context.AddImage(ImagePartType.Png);
            Fill(image, raster);
        }
        var shapeId = "_x0000_s" + values.Text(context.Drawings.Fresh() + 2048);
        var shape = vml.Shape(line, shapeId, Style(item), new XAttribute(vml.Relationships + "id", context.Part.GetIdOfPart(image)));
        if (item.Anchor is not AsCharacterAnchor)
            shape.Add(new XElement(_wordVml + "wrap", new XAttribute("type", Wrap(item.Wrap))));
        var picture = new XElement(_word + "pict",
            new XAttribute(XNamespace.Xmlns + "w", _word.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "v", vml.Vml.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "o", vml.Office.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "r", vml.Relationships.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "w10", _wordVml.NamespaceName),
            vml.PictureType(),
            shape);
        return new W.Run(new W.Picture(picture.ToString(SaveOptions.DisableFormatting)));
    }

    private void Fill(ImagePart part, byte[] bytes)
    {
        using var stream = part.GetStream(FileMode.Create);
        stream.Write(bytes, 0, bytes.Length);
    }

    public AnchoredObject? Read(W.Picture picture, DocxReadContext context, string paragraphId)
    {
        if (Shape(XElement.Parse(picture.OuterXml)) is not { } shape) return null;
        var style = styles.Parse((string?)shape.Attribute("style"));
        var size = new DocumentSize(styles.Length(style, "width") ?? SignatureLineObject.StandardSize.Width, styles.Length(style, "height") ?? SignatureLineObject.StandardSize.Height);
        var line = vml.Read(shape, context.Document.NextId("obj"))!;
        var absolute = style.TryGetValue("position", out var position) && position == "absolute";
        if (!absolute) return Inline(context, paragraphId, line, size);
        var item = new AnchoredObject(context.Document.NextId("obj"), new CharacterAnchor(paragraphId), new EmbeddedContent(line with { NaturalSize = size }), size)
        {
            Placement = new ObjectPlacement(styles.Length(style, "margin-left") ?? Twip.Zero, styles.Length(style, "margin-top") ?? Twip.Zero)
            {
                Horizontal = style.TryGetValue("mso-position-horizontal-relative", out var horizontal)
                    ? _horizontal.Where(pair => pair.Word == horizontal).Select(pair => pair.Relation).DefaultIfEmpty(HorizontalRelation.Paragraph).First()
                    : HorizontalRelation.Paragraph,
                Vertical = style.TryGetValue("mso-position-vertical-relative", out var vertical)
                    ? _vertical.Where(pair => pair.Word == vertical).Select(pair => pair.Relation).DefaultIfEmpty(VerticalRelation.Paragraph).First()
                    : VerticalRelation.Paragraph,
            },
            Wrap = (string?)shape.Element(_wordVml + "wrap")?.Attribute("type") switch
            {
                "topAndBottom" => WrapMode.None,
                "none" => WrapMode.Through,
                _ => WrapMode.Parallel,
            },
        };
        return item;
    }

    private XElement? Shape(XElement picture) =>
        picture.Descendants(vml.Vml + "shape").FirstOrDefault(vml.Holds);

    private string Style(AnchoredObject item)
    {
        var size = $"width:{styles.Points(item.Size.Width)};height:{styles.Points(item.Size.Height)}";
        if (item.Anchor is AsCharacterAnchor) return size;
        var horizontal = _horizontal.First(pair => pair.Relation == item.Placement.Horizontal).Word;
        var vertical = _vertical.First(pair => pair.Relation == item.Placement.Vertical).Word;
        return string.Create(CultureInfo.InvariantCulture,
            $"position:absolute;margin-left:{styles.Points(item.Placement.X)};margin-top:{styles.Points(item.Placement.Y)};{size};z-index:{Math.Max(0, item.ZOrder) + 1};mso-position-horizontal-relative:{horizontal};mso-position-vertical-relative:{vertical}");
    }

    private string Wrap(WrapMode mode) => mode switch
    {
        WrapMode.None or WrapMode.TopAndBottom => "topAndBottom",
        WrapMode.Through => "none",
        _ => "square",
    };
}

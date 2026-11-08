using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Foundation.Units;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxVmlReader(VmlStyle styles, DocxSignatureLineCodec signatures, DocxVmlPictures pictures, DocxVmlShapes shapes)
{
    public AnchoredObject? Read(W.Picture picture, DocxReadContext context, DocxBlockSource blocks, string paragraphId)
    {
        if (signatures.Reads(picture)) return signatures.Read(picture, context, paragraphId);
        if (shapes.Root(picture) is { } root) return Group(root, context, blocks, paragraphId);

        if (shapes.Drawable(picture) is { } drawable) return Single(drawable, context, blocks, paragraphId);
        if (Image(picture, context, paragraphId) is { } image) return image;

        if (picture.Descendants<W.TextBoxContent>().FirstOrDefault() is not { } content)
        {
            if (shapes.DefinesOnly(picture)) return null;
            context.Losses.Add("DOCX102");
            return null;
        }

        var shape = content.Ancestors().FirstOrDefault(ancestor => ancestor.Parent is W.Picture) ?? picture.FirstChild;
        var style = styles.Parse(shape?.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == "style").Value);
        var size = new DocumentSize(styles.Length(style, "width") ?? Twip.FromCentimeters(4), styles.Length(style, "height") ?? Twip.FromCentimeters(2));
        var box = new TextBoxContent(new BlockContainer(blocks(content.ChildElements)));
        var absolute = style.TryGetValue("position", out var position) && position == "absolute";
        var item = new AnchoredObject(context.Document.NextId("obj"),
            absolute ? new CharacterAnchor(paragraphId) : new AsCharacterAnchor(paragraphId), box, size);
        if (absolute)
            item.Placement = new ObjectPlacement(styles.Length(style, "margin-left") ?? Twip.Zero, styles.Length(style, "margin-top") ?? Twip.Zero);
        return item;
    }

    private AnchoredObject? Single(DocumentFormat.OpenXml.OpenXmlElement element, DocxReadContext context, DocxBlockSource blocks, string paragraphId)
    {
        if (shapes.Single(element, context, blocks) is not { } read)
        {
            context.Losses.Add("DOCX102");
            return null;
        }
        var style = styles.Parse(element.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == "style").Value);
        var extent = read.Extent;
        var size = new DocumentSize(
            styles.Length(style, "width") ?? extent?.Width ?? Twip.FromCentimeters(4),
            styles.Length(style, "height") ?? extent?.Height ?? Twip.FromCentimeters(2));
        var absolute = style.TryGetValue("position", out var position) && position == "absolute" || extent is not null && !style.ContainsKey("width");
        var item = new AnchoredObject(read.Value.Id, absolute ? new CharacterAnchor(paragraphId) : new AsCharacterAnchor(paragraphId),
            new EmbeddedContent(read.Value, read.Story), size);
        if (absolute)
        {
            var offset = style.ContainsKey("width") ? null : extent;
            item.Placement = new ObjectPlacement(
                (styles.Length(style, "margin-left") ?? styles.Length(style, "left") ?? Twip.Zero) + (offset?.Left ?? Twip.Zero),
                (styles.Length(style, "margin-top") ?? styles.Length(style, "top") ?? Twip.Zero) + (offset?.Top ?? Twip.Zero));
        }
        return item;
    }

    private AnchoredObject? Group(DocumentFormat.OpenXml.OpenXmlElement root, DocxReadContext context, DocxBlockSource blocks, string paragraphId)
    {
        if (shapes.Group(root, context, blocks) is not { } read)
        {
            context.Losses.Add("DOCX102");
            return null;
        }
        var style = styles.Parse(root.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == "style").Value);
        var size = new DocumentSize(styles.Length(style, "width") ?? Twip.FromCentimeters(4), styles.Length(style, "height") ?? Twip.FromCentimeters(3));
        var absolute = style.TryGetValue("position", out var position) && position == "absolute";
        var item = new AnchoredObject(context.Document.NextId("obj"), absolute ? new CharacterAnchor(paragraphId) : new AsCharacterAnchor(paragraphId),
            new EmbeddedContent(read.Group, null, read.Stories), size);
        if (absolute)
            item.Placement = new ObjectPlacement(styles.Length(style, "margin-left") ?? Twip.Zero, styles.Length(style, "margin-top") ?? Twip.Zero);
        return item;
    }

    private AnchoredObject? Image(W.Picture picture, DocxReadContext context, string paragraphId)
    {
        if (pictures.Read(picture, context) is not { } image) return null;
        var shape = picture.Descendants<DocumentFormat.OpenXml.Vml.ImageData>().First().Parent;
        var style = styles.Parse(shape?.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == "style").Value);
        var size = new DocumentSize(
            styles.Length(style, "width") ?? Twip.FromCentimeters(4),
            styles.Length(style, "height") ?? Twip.FromCentimeters(3));
        var absolute = style.TryGetValue("position", out var position) && position == "absolute";
        var item = new AnchoredObject(image.Id, absolute ? new CharacterAnchor(paragraphId) : new AsCharacterAnchor(paragraphId), new EmbeddedContent(image), size);
        if (absolute)
            item.Placement = new ObjectPlacement(styles.Length(style, "margin-left") ?? Twip.Zero, styles.Length(style, "margin-top") ?? Twip.Zero);
        return item;
    }
}

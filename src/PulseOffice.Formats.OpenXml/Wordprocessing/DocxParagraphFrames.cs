using DocumentFormat.OpenXml;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Foundation.Units;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxParagraphFrames
{
    private static readonly Twip DefaultWidth = Twip.FromCentimeters(5);

    private const int StyleDepth = 32;

    public bool Frames(W.Paragraph paragraph, DocxReadContext context) =>
        paragraph.ParagraphProperties?.SectionProperties is null
        && Effective(paragraph, context) is { } frame
        && (frame.DropCap?.Value is null || frame.DropCap.Value == W.DropCapLocationValues.None)
        && Positioned(frame);

    public string Key(W.Paragraph paragraph, DocxReadContext context)
    {
        var frame = Effective(paragraph, context)!;
        return string.Join("|", frame.GetAttributes().OrderBy(attribute => attribute.LocalName, StringComparer.Ordinal).Select(attribute => attribute.LocalName + "=" + attribute.Value));
    }

    private W.FrameProperties? Effective(W.Paragraph paragraph, DocxReadContext context)
    {
        var direct = paragraph.ParagraphProperties?.FrameProperties;
        if (direct?.DropCap?.Value is { } drop && drop != W.DropCapLocationValues.None) return direct;
        var inherited = StyleFrame(paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value, context, 0);
        if (inherited is null) return direct;
        if (direct is null) return inherited;
        var merged = (W.FrameProperties)inherited.CloneNode(true);
        foreach (var attribute in direct.GetAttributes()) merged.SetAttribute(attribute);
        return merged;
    }

    private W.FrameProperties? StyleFrame(string? styleId, DocxReadContext context, int depth)
    {
        if (styleId is null || depth > StyleDepth) return null;
        if (context.StyleFrames.TryGetValue(styleId, out var known)) return known;
        context.StyleFrames[styleId] = null;
        var style = Styles(context).FirstOrDefault(item => item.StyleId?.Value == styleId && item.Type?.Value == W.StyleValues.Paragraph);
        var own = style?.StyleParagraphProperties?.FrameProperties;
        var parent = StyleFrame(style?.BasedOn?.Val?.Value, context, depth + 1);
        W.FrameProperties? result = parent is null ? (W.FrameProperties?)own?.CloneNode(true) : (W.FrameProperties)parent.CloneNode(true);
        if (parent is not null && own is not null)
            foreach (var attribute in own.GetAttributes()) result!.SetAttribute(attribute);
        if (result?.DropCap?.Value is { } drop && drop != W.DropCapLocationValues.None) result = null;
        context.StyleFrames[styleId] = result;
        return result;
    }

    private IEnumerable<W.Style> Styles(DocxReadContext context) =>
        (context.Part.OpenXmlPackage as DocumentFormat.OpenXml.Packaging.WordprocessingDocument)?.MainDocumentPart?.StyleDefinitionsPart?.Styles?.Elements<W.Style>() ?? [];

    public AnchoredObject Build(IReadOnlyList<ParagraphBlock> blocks, W.Paragraph first, string id, string anchorParagraphId, DocxReadContext context)
    {
        var frame = Effective(first, context)!;
        var width = Twips(frame.Width) is { Value: > 0 } given ? given : DefaultWidth;
        var height = Twips(frame.Height) is { Value: > 0 } tall ? tall : Twip.FromCentimeters(0.5 * Math.Max(1, blocks.Count));
        var content = new TextBoxContent(new BlockContainer(blocks.Cast<Block>()))
        {
            AutoGrowHeight = frame.HeightType?.Value != W.HeightRuleValues.Exact,
        };
        var item = new AnchoredObject(id, new CharacterAnchor(anchorParagraphId), content, new DocumentSize(width, height))
        {
            Placement = new ObjectPlacement(Twips(frame.X) ?? Twip.Zero, Twips(frame.Y) ?? Twip.Zero)
            {
                Horizontal = Horizontal(frame.HorizontalPosition?.Value),
                Vertical = Vertical(frame.VerticalPosition?.Value),
            },
            Wrap = frame.Wrap?.Value == W.TextWrappingValues.NotBeside || frame.Wrap?.Value == W.TextWrappingValues.None ? WrapMode.TopAndBottom : WrapMode.Parallel,
        };
        var horizontalSpace = Twips(frame.HorizontalSpace) ?? Twip.Zero;
        var verticalSpace = Twips(frame.VerticalSpace) ?? Twip.Zero;
        item.WrapSpacing = new PulseOffice.Foundation.Units.Margins(verticalSpace, horizontalSpace, verticalSpace, horizontalSpace);
        return item;
    }

    private bool Positioned(W.FrameProperties frame) =>
        frame.X is not null || frame.Y is not null || frame.XAlign is not null || frame.YAlign is not null || frame.Width is not null
        || frame.HorizontalPosition is not null || frame.VerticalPosition is not null;

    private Twip? Twips(StringValue? value) =>
        int.TryParse(value?.Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var twips) ? new Twip(twips) : null;

    private Twip? Twips(UInt32Value? value) => value?.Value is { } twips ? new Twip((int)Math.Min(twips, int.MaxValue)) : null;

    private Twip? Twips(Int32Value? value) => value?.Value is { } twips ? new Twip(twips) : null;

    private HorizontalRelation Horizontal(W.HorizontalAnchorValues? anchor) =>
        anchor == W.HorizontalAnchorValues.Page ? HorizontalRelation.Page
        : anchor == W.HorizontalAnchorValues.Margin ? HorizontalRelation.Margin
        : HorizontalRelation.Column;

    private VerticalRelation Vertical(W.VerticalAnchorValues? anchor) =>
        anchor == W.VerticalAnchorValues.Page ? VerticalRelation.Page
        : anchor == W.VerticalAnchorValues.Margin ? VerticalRelation.Margin
        : VerticalRelation.Paragraph;
}

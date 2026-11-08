using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.OpenXml.Tests;

internal sealed class ObjectSample
{
    public static readonly ObjectContour Triangle = new([new ContourPoint(0.5, 0), new ContourPoint(1, 1), new ContourPoint(0, 1)]);

    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==");

    private RasterImageObject Picture(string id, string? title = null, string? description = null) => new()
    {
        Id = id,
        Data = Png,
        MediaType = "image/png",
        PixelWidth = 1,
        PixelHeight = 1,
        Title = title,
        Description = description,
        NaturalSize = new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3)),
    };

    private ParagraphBlock Text(string id, string text) => new(id, text, DefaultStyleNames.TextBody);

    public TextDocument Build()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Styles.Paragraph.Add(new ParagraphStyle("Figure") { ParentName = DefaultStyleNames.Caption });
        var words = string.Join(' ', Enumerable.Repeat("parola", 60));
        var contour = Text("contour", words);
        var first = Text("first", "Solo questo paragrafo gira intorno.");
        var after = Text("after", "Questo va sotto.");
        var holder = Text("holder", string.Empty);
        holder.Marks = [new ObjectMark(0, "inline")];
        var caption = new ParagraphBlock("caption", new TextParagraph(string.Empty) { StyleName = "Figure" }.WithRuns(
        [
            new TextRun("Figura "),
            new TextRun("1") { Field = TextFieldKind.Sequence, FieldArgument = "Figure" },
            new TextRun(": il grafico"),
        ]));
        var frames = Text("frames", "Cornici collegate.");
        document.Body.Replace([contour, first, after, holder, caption, frames]);

        document.Objects.Add(new AnchoredObject("tri", new ParagraphAnchor("contour"), new EmbeddedContent(Picture("tri-c", "Triangolo", "Un triangolo")), new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3)))
        {
            Name = "Triangolo",
            Placement = new ObjectPlacement(Twip.FromCentimeters(1), Twip.FromCentimeters(0.5)),
            Wrap = WrapMode.Right,
            WrapContour = true,
            Contour = Triangle,
            WrapSpacing = new Margins(Twip.FromPoints(6), Twip.Zero, Twip.FromPoints(6), Twip.Zero),
            FlipHorizontal = true,
            ZOrder = 0,
        });
        document.Objects.Add(new AnchoredObject("once", new ParagraphAnchor("first"), new EmbeddedContent(Picture("once-c")), new DocumentSize(Twip.FromCentimeters(3), Twip.FromCentimeters(3)))
        {
            Name = "Primo",
            Wrap = WrapMode.Right,
            WrapFirstParagraph = true,
            FlipVertical = true,
            ZOrder = 1,
        });
        document.Objects.Add(new AnchoredObject("inline", new AsCharacterAnchor("holder"), new EmbeddedContent(Picture("inline-c")), new DocumentSize(Twip.FromCentimeters(5), Twip.FromCentimeters(2)))
        {
            Name = "Grafico",
            ZOrder = 2,
        });
        var story = new BlockContainer([new ParagraphBlock("chain", new TextParagraph(words) { StyleName = DefaultStyleNames.FrameContents }.WithRuns([new TextRun(words)]))]);
        document.Objects.Add(new AnchoredObject("head", new ParagraphAnchor("frames"), new TextBoxContent(story) { AutoGrowHeight = false, NextFrameName = "Seconda" }, new DocumentSize(Twip.FromCentimeters(5), Twip.FromCentimeters(2)))
        {
            Name = "Prima",
            Placement = new ObjectPlacement(Twip.Zero, Twip.FromCentimeters(1)),
            ZOrder = 3,
        });
        document.Objects.Add(new AnchoredObject("tail", new ParagraphAnchor("frames"), new TextBoxContent(new BlockContainer([new ParagraphBlock("empty", string.Empty, DefaultStyleNames.FrameContents)])), new DocumentSize(Twip.FromCentimeters(5), Twip.FromCentimeters(2)))
        {
            Name = "Seconda",
            Placement = new ObjectPlacement(Twip.FromCentimeters(8), Twip.FromCentimeters(1)),
            ZOrder = 4,
        });
        return document;
    }
}

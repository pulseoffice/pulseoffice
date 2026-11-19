using Avalonia;
using PulseOffice.Document.Commands;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations.UI.Rendering;
using PulseOffice.Printing;
using PulseOffice.Testing;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.UI.Controls.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round265ShapeTextTagTests
{
    private static readonly UiThreadSession Session = UiThreadSession.StartNew(typeof(DeckHeadlessApp));

    private static readonly ShapeObject Repeated = new() { Id = "forma", Shape = ShapeKind.Rectangle, Text = "Same\nSame" };

    [Fact]
    public void A_shape_with_text_is_a_section_whose_paragraphs_are_tagged_by_line_position()
    {
        var (_, slide, element) = Slide(Repeated);
        var container = new SlideStructureTags().For(slide, element)!;
        var placed = new SlideElementPlacement(0, 0, 200, 100, 0.05, 0)
        {
            Lines = [new RichTextLineBox(0, null, new Rect(10, 10, 100, 20)), new RichTextLineBox(1, null, new Rect(10, 40, 100, 20))],
        };

        var locator = new SlideTextSegments().For(container, element, placed)!.Locator!;

        Assert.Equal(PrintRole.Section, container.Role);
        Assert.EndsWith(":0", locator.At(50, 20)!.Key, StringComparison.Ordinal);
        Assert.EndsWith(":1", locator.At(50, 50)!.Key, StringComparison.Ordinal);
    }

    [Fact]
    public Task Printed_shape_text_takes_the_tags_of_the_painted_lines() => Session.Dispatch(() =>
    {
        var (deck, slide, _) = Slide(Repeated);
        var canvas = new TagCanvas(new DocumentSize(deck.Size.Width, deck.Size.Height));

        new SlidePagePainter(new SlideRenderer(new DrawingObjectRenderer(), new FakeTextCatalog()))
            .For(deck, slide).Paint(canvas, new DocumentRect(Twip.Zero, Twip.Zero, deck.Size.Width, deck.Size.Height));

        var keys = canvas.TextTags.Where(tag => tag?.Role == PrintRole.Paragraph).Select(tag => tag!.Key).Distinct().ToList();
        Assert.Equal(2, keys.Count);
    }, CancellationToken.None);

    private static (Presentation Deck, Slide Slide, ObjectElement Element) Slide(ShapeObject shape)
    {
        var deck = new Presentation();
        deck.Master = new MasterFactory().CreateStandard(deck.Theme);
        var slide = new SlideFactory().Create(deck.NextId(), deck.Master, SlideLayout.Blank);
        deck.AddExisting(slide);
        var element = new ObjectElement
        {
            Id = deck.NextId(),
            Bounds = new DocumentRect(Twip.FromCentimeters(2), Twip.FromCentimeters(2), Twip.FromCentimeters(8), Twip.FromCentimeters(4)),
            ZOrder = slide.Elements.Count,
            Content = shape,
        };
        new DeckActions(deck, new UndoStack(), new SlideFactory(), new FakeTextCatalog()).InsertObject(slide, element);
        return (deck, slide, element);
    }

    private sealed class TagCanvas(DocumentSize size) : IPageCanvas
    {
        private PrintTag? _current;

        public List<PrintTag?> TextTags { get; } = [];

        public DocumentSize PageSize => size;

        public Twip MeasureText(string text, PrintFont font) => Twip.FromPoints(text.Length * font.SizePoints / 2);

        public void Tag(PrintTag? tag) => _current = tag;

        public void DrawImage(PageImage image, DocumentRect bounds)
        {
        }

        public void FillRectangle(DocumentRect rect, PrintColor color)
        {
        }

        public void DrawLine(DocumentPoint from, DocumentPoint to, PrintColor color, Twip thickness)
        {
        }

        public void DrawText(string text, DocumentRect bounds, PrintFont font, PrintColor color, PulseOffice.Printing.TextAlignment alignment, PulseOffice.Printing.TextBaseline baseline) =>
            TextTags.Add(_current);

        public void DrawPath(PrintPath path)
        {
        }
    }
}

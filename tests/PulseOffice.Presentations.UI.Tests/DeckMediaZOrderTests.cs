using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class DeckMediaZOrderTests
{
    [Fact]
    public void InsertedMediaGoesAboveEveryExistingElement()
    {
        var deck = new Presentation();
        var slide = new Slide("s1", SlideLayout.Blank);
        deck.AddExisting(slide);
        slide.Add(new TextElement { Text = new PulseOffice.Text.Rich.RichText(PlaceholderRole.Free, [new PulseOffice.Text.Rich.TextParagraph("x")]), Id = "a", ZOrder = 5, Bounds = new DocumentRect(new Twip(0), new Twip(0), new Twip(10), new Twip(10)) });
        var actions = new DeckActions(deck, new UndoStack(), new SlideFactory(), new FakeTextCatalog());

        actions.InsertMedia(slide, new MediaElement { Kind = MediaKind.Audio, Id = "m", ZOrder = slide.Elements.Count, MimeType = "audio/mpeg", Data = [1] });

        Assert.Equal(6, slide.Find("m")!.ZOrder);
    }

    [Fact]
    public void InsertedTextBoxGoesAboveEveryExistingElement()
    {
        var deck = new Presentation();
        var slide = new Slide("s1", SlideLayout.Blank);
        deck.AddExisting(slide);
        slide.Add(new TextElement { Text = new RichText(PlaceholderRole.Free, [new TextParagraph("x")]), Id = "a", ZOrder = 5, Bounds = new DocumentRect(new Twip(0), new Twip(0), new Twip(10), new Twip(10)) });
        var actions = new DeckActions(deck, new UndoStack(), new SlideFactory(), new FakeTextCatalog());

        actions.InsertElement(slide, new TextElement { Text = new RichText(PlaceholderRole.Free, [new TextParagraph("y")]), Id = "b", ZOrder = slide.Elements.Count, Bounds = new DocumentRect(new Twip(0), new Twip(0), new Twip(10), new Twip(10)) });

        Assert.Equal(6, slide.Find("b")!.ZOrder);
    }
}

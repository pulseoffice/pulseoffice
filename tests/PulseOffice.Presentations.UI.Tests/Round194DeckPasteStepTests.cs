using PulseOffice.Foundation.Zoom;
using PulseOffice.Document.Commands;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round194DeckPasteStepTests
{
    private readonly List<string?> _labels = [];
    private readonly List<string> _typed = [];

    private DeckTarget Target(bool typing)
    {
        var deck = new Presentation();
        var actions = new DeckActions(deck, new UndoStack(), new SlideFactory(), new FakeTextCatalog());
        return new DeckTarget(deck, actions, () => null, () => 0, () => { },
            _ => { }, () => false, () => null, () => [], _ => { }, _ => { }, () => null,
            () => Task.CompletedTask, _ => { }, () => DeckViewMode.Normal, () => null,
            () => new ViewZoom(ViewZoomMode.FitWhole, 1), _ => { }, () => 1, () => typing, text => { _typed.Add(text); return true; },
            new DeckViewOptions(), _ => { }, (_, _) => false, (_, _, _) => false, () => null, () => null, () => null)
        {
            InsertParagraphsAtCaret = (_, label) => { _labels.Add(label); return true; },
        };
    }

    private DeckTarget WithSlide(bool typing)
    {
        var target = Target(typing);
        var slide = new Slide("s1", SlideLayout.Blank);
        return target with { CurrentSlide = () => slide };
    }

    [Fact]
    public void Pasted_text_at_the_caret_is_its_own_paste_step()
    {
        var insertion = new DeckTextInsertion(new RichTextInsertion());

        insertion.Insert(WithSlide(typing: true), new TextRun("pasted"), 20, "deck.action.paste");

        Assert.Equal(["deck.action.paste"], _labels);
        Assert.Empty(_typed);
    }

    [Fact]
    public void A_symbol_without_a_label_still_joins_the_typing()
    {
        var insertion = new DeckTextInsertion(new RichTextInsertion());

        insertion.Insert(WithSlide(typing: true), new TextRun("€"), 20);

        Assert.Equal(["€"], _typed);
        Assert.Empty(_labels);
    }

    [Fact]
    public void Pasted_paragraphs_carry_the_paste_label()
    {
        var insertion = new DeckTextInsertion(new RichTextInsertion());

        insertion.Insert(WithSlide(typing: true), [new TextParagraph("a"), new TextParagraph("b")], 20, "deck.action.paste");

        Assert.Equal(["deck.action.paste"], _labels);
    }
}

using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class DeckRound107LayoutChangeTests
{
    private readonly ISlideFactory _factory = new SlideFactory();

    [Fact]
    public void Changing_layout_keeps_content_by_type_and_order_and_undo_restores_the_slide()
    {
        var (deck, history, actions, slide) = Deck(SlideLayout.TwoContent);
        var texts = slide.Elements.OfType<TextElement>().ToList();
        slide.Replace(texts[0] with { Text = Text(PlaceholderRole.Title, "Heading") });
        slide.Replace(texts[1] with { Text = Text(PlaceholderRole.Body, "Left") });
        slide.Replace(texts[2] with { Text = Text(PlaceholderRole.Body, "Right") });

        actions.SetLayout(slide, SlideLayout.TitleAndContent);

        var changed = deck.Slides[0];
        Assert.Equal(SlideLayout.TitleAndContent, changed.Layout);
        Assert.Equal("Heading", changed.Placeholder(PlaceholderRole.Title)!.Text.PlainText);
        Assert.Equal("Left", changed.Placeholder(PlaceholderRole.Body)!.Text.PlainText);
        var orphan = Assert.Single(changed.Elements.OfType<TextElement>(), text => text.Text.PlainText == "Right");
        Assert.Equal(PlaceholderRole.Free, orphan.Text.Role);

        history.Undo();
        Assert.Same(slide, deck.Slides[0]);
        Assert.Equal(SlideLayout.TwoContent, deck.Slides[0].Layout);
        history.Redo();
        Assert.Equal(SlideLayout.TitleAndContent, deck.Slides[0].Layout);
    }

    [Fact]
    public void A_second_layout_of_the_same_type_is_a_distinct_choice_with_its_own_geometry()
    {
        var (deck, history, actions, slide) = Deck(SlideLayout.TitleAndContent);
        var standard = deck.Master.Template(SlideLayout.TitleAndContent);
        var custom = standard with
        {
            Id = "layout.custom.21",
            Name = "Quote",
            Placeholders = [.. standard.Placeholders.Select(placeholder => placeholder with
            {
                Bounds = placeholder.Bounds with { Left = new Twip(4000) },
            })],
        };
        deck.Master = deck.Master with { Layouts = [.. deck.Master.Layouts, custom] };

        actions.SetLayout(slide, deck.Master.Template(SlideLayout.TitleAndContent, "layout.custom.21"));

        var changed = deck.Slides[0];
        Assert.Equal("layout.custom.21", changed.LayoutId);
        Assert.Same(deck.Master.Layouts[^1], deck.Master.Template(changed));
        Assert.All(changed.Elements.OfType<TextElement>(), text => Assert.Equal(4000, text.Bounds.Left.Value));

        var point = history.StatePoint;
        actions.SetLayout(changed, deck.Master.Template(changed));
        Assert.Same(point, history.StatePoint);

        actions.SetLayout(changed, standard);
        Assert.Equal(standard.Id, deck.Slides[0].LayoutId);
        history.Undo();
        Assert.Equal("layout.custom.21", deck.Slides[0].LayoutId);
    }

    private RichText Text(PlaceholderRole role, string text) => new(role, [new TextParagraph(text)]);

    private (Presentation Deck, UndoStack History, DeckActions Actions, Slide Slide) Deck(SlideLayout layout)
    {
        var deck = new Presentation();
        deck.Master = new MasterFactory().CreateStandard(deck.Theme);
        var history = new UndoStack();
        var actions = new DeckActions(deck, history, _factory, new FakeTextCatalog());
        var slide = _factory.Create(deck.NextId(), deck.Master, layout);
        deck.AddExisting(slide);
        return (deck, history, actions, slide);
    }
}

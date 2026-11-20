using PulseOffice.Document.Commands;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round336SlideSelectionTests
{
    private static (Presentation Deck, DeckActions Actions, UndoStack History) Deck(int count)
    {
        var deck = new Presentation();
        var factory = new SlideFactory();
        deck.Master = new MasterFactory().CreateStandard(deck.Theme);
        for (var i = 0; i < count; i++) deck.AddExisting(factory.Create(deck.NextId(), deck.Master, SlideLayout.Blank));
        var history = new UndoStack();
        return (deck, new DeckActions(deck, history, factory, new FakeTextCatalog()), history);
    }

    [Fact]
    public void Shift_extension_grows_from_the_current_slide()
    {
        var (deck, _, _) = Deck(4);
        var selection = new SlideSelection();

        selection.ExtendTo(deck, deck.Slides[1], deck.Slides[2]);
        selection.ExtendTo(deck, deck.Slides[2], deck.Slides[3]);

        Assert.Equal(new[] { deck.Slides[1], deck.Slides[2], deck.Slides[3] }, selection.Ordered(deck, deck.Slides[3]));
    }

    [Fact]
    public void Several_selected_slides_move_together_in_one_undo_step()
    {
        var (deck, actions, history) = Deck(5);
        var order = deck.Slides.ToList();

        actions.MoveSlides([order[0], order[2]], order[3]);

        Assert.Equal(new[] { order[1], order[3], order[0], order[2], order[4] }, deck.Slides);
        history.Undo();
        Assert.Equal(order, deck.Slides);
    }

    [Fact]
    public void Moving_before_a_slide_keeps_the_selection_order()
    {
        var (deck, actions, _) = Deck(5);
        var order = deck.Slides.ToList();

        actions.MoveSlides([order[2], order[4]], order[1]);

        Assert.Equal(new[] { order[0], order[2], order[4], order[1], order[3] }, deck.Slides);
    }
}

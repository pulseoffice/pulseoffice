using PulseOffice.Document.Commands;
using PulseOffice.Exchange;
using PulseOffice.Foundation.Zoom;
using PulseOffice.Presentations;
using PulseOffice.Presentations.UI.Exchange;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round332SlideClipboardTests
{
    private static (Presentation Deck, UndoStack History, DeckActions Actions) Deck(int slides)
    {
        var deck = new Presentation();
        deck.Master = new MasterFactory().CreateStandard(deck.Theme);
        var history = new UndoStack();
        var factory = new SlideFactory();
        for (var i = 0; i < slides; i++) deck.AddExisting(factory.Create(deck.NextId(), deck.Master.Template(SlideLayout.TitleOnly)));
        return (deck, history, new DeckActions(deck, history, factory, new FakeTextCatalog()));
    }

    private static DeckTarget Target(Presentation deck, DeckActions actions, Func<Slide?> current, DeckViewMode mode, SlideSelection? selection = null, bool paneFocused = false) =>
        new(deck, actions, current, () => current() is { } shown ? deck.IndexOf(shown) : -1, () => { },
            _ => { }, () => false, () => null, () => [], _ => { }, _ => { }, () => null,
            () => Task.CompletedTask, _ => { }, () => mode, () => null,
            () => new ViewZoom(ViewZoomMode.FitWhole, 1), _ => { }, () => 1, () => false, _ => false,
            new DeckViewOptions(), _ => { }, (_, _) => false, (_, _, _) => false, () => null, () => null, () => null)
        {
            SelectedSlides = () => selection?.Ordered(deck, current()) ?? [],
            SlidePaneFocused = () => paneFocused,
        };

    [Fact]
    public void Ctrl_and_shift_clicks_choose_slides_in_deck_order()
    {
        var (deck, _, _) = Deck(4);
        var selection = new SlideSelection();
        selection.Pick(deck, deck.Slides[2], SlidePick.Only);
        selection.Pick(deck, deck.Slides[0], SlidePick.Toggle);
        Assert.Equal(new[] { deck.Slides[0], deck.Slides[2] }, selection.Ordered(deck, deck.Slides[0]));
        selection.Pick(deck, deck.Slides[3], SlidePick.Extend);
        Assert.Equal(deck.Slides, selection.Ordered(deck, deck.Slides[3]));
        Assert.Equal(new[] { deck.Slides[1] }, new SlideSelection().Ordered(deck, deck.Slides[1]));
    }

    [Fact]
    public void Several_slides_are_copied_from_the_filmstrip_and_cut_in_one_step()
    {
        var (deck, history, actions) = Deck(3);
        var selection = new SlideSelection();
        selection.Pick(deck, deck.Slides[0], SlidePick.Only);
        selection.Pick(deck, deck.Slides[1], SlidePick.Toggle);
        var current = deck.Slides[1];
        var target = Target(deck, actions, () => current, DeckViewMode.Normal, selection, paneFocused: true);
        Assert.True(target.CopiesSlides());

        var package = new DeckSlideClipboard(new SlideFactory()).Capture("p", target, cut: true);

        var copied = Assert.IsType<DeckSlidesNative>(package?.Native);
        Assert.Equal(2, copied.Slides.Count);
        Assert.Single(deck.Slides);
        history.Undo();
        Assert.Equal(3, deck.Slides.Count);
    }

    [Fact]
    public void Slides_from_another_deck_take_the_destination_theme_by_default_or_keep_their_source_formatting()
    {
        var (source, _, sourceActions) = Deck(1);
        var copied = new DeckSlidesNative(source, [source.Slides[0]]);
        var clipboard = new DeckSlideClipboard(new SlideFactory());
        var (destination, _, actions) = Deck(1);
        var target = Target(destination, actions, () => destination.Slides[0], DeckViewMode.Sorter);

        Assert.Equal(new[] { ExchangePasteMode.Native, ExchangePasteMode.KeepSourceFormatting }, clipboard.Modes(target, copied));
        Assert.Equal(new[] { ExchangePasteMode.Native }, clipboard.Modes(Target(source, sourceActions, () => source.Slides[0], DeckViewMode.Sorter), copied));

        Assert.True(clipboard.Receive(target, copied, ExchangePasteMode.Native));
        Assert.Null(destination.Slides[1].MasterId);
        Assert.True(clipboard.Receive(target, copied, ExchangePasteMode.KeepSourceFormatting));
        Assert.Equal(2, destination.Masters.Count);
        Assert.Equal(destination.Masters[1].Id, destination.Slides[1].MasterId);
    }
}

using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class Round189ShowHiddenSlideLinkTests
{
    private readonly IAnimationScheduler _scheduler = new AnimationScheduler();
    private readonly ShowPlaylist _playlists = new();

    private static Presentation Deck(int count)
    {
        var master = new MasterFactory().CreateStandard(new ThemeCatalog().Default);
        var deck = new Presentation { Master = master, Theme = new ThemeCatalog().Default };
        var factory = new SlideFactory();
        for (var i = 0; i < count; i++) deck.AddExisting(factory.Create(deck.NextId(), master, SlideLayout.Blank));
        return deck;
    }

    [Fact]
    public void A_hidden_slide_reached_by_a_link_joins_the_show_in_deck_order_and_the_show_continues_after_it()
    {
        var deck = Deck(4);
        deck.Slides[2].Hidden = true;
        var playlist = _playlists.Main(deck);
        var controller = new PresentationController(playlist, _scheduler, startIndex: 0);

        var index = controller.Admit(deck.Slides[2], _playlists.DetourPosition(deck, controller.Slides, deck.Slides[2]));
        controller.GoTo(index);

        Assert.Same(deck.Slides[2], controller.Current);
        Assert.Equal(AdvanceResult.ChangedSlide, controller.Advance());
        Assert.Same(deck.Slides[3], controller.Current);
    }

    [Fact]
    public void Admitting_before_the_current_slide_keeps_the_current_slide()
    {
        var deck = Deck(3);
        deck.Slides[0].Hidden = true;
        var controller = new PresentationController(_playlists.Main(deck), _scheduler, startIndex: 1);

        Assert.Equal(0, controller.Admit(deck.Slides[0], _playlists.DetourPosition(deck, controller.Slides, deck.Slides[0])));
        Assert.Same(deck.Slides[2], controller.Current);
    }
}

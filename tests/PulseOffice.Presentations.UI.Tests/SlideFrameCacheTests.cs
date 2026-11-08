using PulseOffice.Document.Commands;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class SlideFrameCacheTests
{
    [Fact]
    public void Editing_one_slide_rebuilds_only_its_frame_and_the_current_change_rebuilds_two()
    {
        var deck = new Presentation();
        var factory = new SlideFactory();
        deck.Master = new MasterFactory().CreateStandard(deck.Theme);
        for (var i = 0; i < 50; i++) deck.AddExisting(factory.Create(deck.NextId(), deck.Master, SlideLayout.TitleAndContent));
        var actions = new DeckActions(deck, new UndoStack(), factory, new FakeTextCatalog());
        var cache = new SlideFrameCache<object>();
        var built = 0;
        object Build(Slide slide, int index) { built++; return new object(); }

        var first = cache.Refresh(deck, deck.Slides[0], Build);
        actions.SetSlideText(deck.Slides[10], PlaceholderRole.Title, "Changed");
        var second = cache.Refresh(deck, deck.Slides[0], Build);

        Assert.Equal(51, built);
        Assert.Equal(1, cache.Rebuilt);
        Assert.Same(first[0], second[0]);
        Assert.NotSame(first[10], second[10]);

        cache.Refresh(deck, deck.Slides[1], Build);
        Assert.Equal(2, cache.Rebuilt);
    }

    [Fact]
    public void Removed_slides_leave_the_cache()
    {
        var deck = new Presentation();
        var factory = new SlideFactory();
        deck.Master = new MasterFactory().CreateStandard(deck.Theme);
        for (var i = 0; i < 3; i++) deck.AddExisting(factory.Create(deck.NextId(), deck.Master, SlideLayout.Blank));
        var actions = new DeckActions(deck, new UndoStack(), factory, new FakeTextCatalog());
        var cache = new SlideFrameCache<object>();

        cache.Refresh(deck, null, (_, _) => new object());
        actions.DeleteSlide(deck.Slides[0]);
        var frames = cache.Refresh(deck, null, (_, _) => new object());

        Assert.Equal(2, frames.Count);
        Assert.Equal(2, cache.Rebuilt);
    }
}

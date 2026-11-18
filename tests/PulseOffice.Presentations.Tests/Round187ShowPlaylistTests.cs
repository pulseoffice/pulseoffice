using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class Round187ShowPlaylistTests
{
    [Fact]
    public void TheMainShowKeepsTheSetupRangeAndSkipsHiddenSlides()
    {
        var deck = new Presentation();
        foreach (var id in new[] { "a", "b", "c", "d" }) deck.AddExisting(new Slide(id, SlideLayout.Blank));
        deck.Slides[2].Hidden = true;
        deck.Setup = new ShowSetup { FromSlide = 2, ToSlide = 4 };

        var shown = new ShowPlaylist().Main(deck);

        Assert.Equal(["b", "d"], shown.Select(slide => slide.Id));
    }
}

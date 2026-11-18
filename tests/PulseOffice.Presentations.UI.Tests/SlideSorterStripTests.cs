using Avalonia;
using Avalonia.Controls;
using PulseOffice.Document.Commands;
using PulseOffice.Presentations;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class SlideSorterStripTests
{
    [Fact]
    public void SiRicostruisconoSoloLeMiniatureCambiate()
    {
        var deck = new Presentation();
        var factory = new SlideFactory();
        deck.Master = new MasterFactory().CreateStandard(deck.Theme);
        for (var i = 0; i < 3; i++) deck.AddExisting(factory.Create(deck.NextId(), deck.Master, SlideLayout.TitleAndContent));
        var actions = new DeckActions(deck, new UndoStack(), factory, new FakeTextCatalog());
        var strip = new SlideSorterStrip(new Thickness(0), _ => new Border(), new DeckSectionHeaders(actions, new FakeTextCatalog(), PulseOffice.UI.Theming.DesignTokens.For(PulseOffice.UI.Theming.InterfaceDensity.Compact)), new SlideStripLayout());

        strip.Update(deck, deck.Slides[0]);
        Assert.Equal(3, strip.Rebuilt);

        strip.Update(deck, deck.Slides[0]);
        Assert.Equal(0, strip.Rebuilt);

        actions.SetSlideText(deck.Slides[2], PlaceholderRole.Title, "Cambiata");
        strip.Update(deck, deck.Slides[0]);
        Assert.Equal(1, strip.Rebuilt);

        actions.AddSlide(SlideLayout.Blank, 2);
        strip.Update(deck, deck.Slides[0]);
        Assert.Equal(1, strip.Rebuilt);
        Assert.Equal(4, Assert.IsType<WrapPanel>(Assert.Single(strip.Panel.Children)).Children.Count);
    }
}

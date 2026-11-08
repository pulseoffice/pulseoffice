using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class SlideNamesTests
{
    private static Presentation Deck(params string[] ids)
    {
        var deck = new Presentation();
        foreach (var id in ids) deck.AddExisting(new Slide(id, SlideLayout.Blank));
        return deck;
    }

    [Fact]
    public void IlNomeDiPaginaEIlNomeSeCeAltrimentiLIdentificativo()
    {
        var slide = new Slide("slide4", SlideLayout.Blank);
        Assert.Equal("slide4", SlideNames.PageName(slide));

        slide.Name = "Introduzione";
        Assert.Equal("Introduzione", SlideNames.PageName(slide));
    }

    [Theory]
    [InlineData("slide1", true)]
    [InlineData("page12", true)]
    [InlineData("Page3", true)]
    [InlineData("Introduzione", false)]
    [InlineData("slide 1", false)]
    public void RiconosceINomiAutomatici(string name, bool automatic) =>
        Assert.Equal(automatic, SlideNames.IsAutomatic(name));

    [Fact]
    public void UnNomeAutomaticoNonDiventaUnNomeScelto()
    {
        Assert.Null(SlideNames.NameFromPage("page2"));
        Assert.Equal("Conclusioni", SlideNames.NameFromPage("Conclusioni"));
    }

    [Fact]
    public void DueDiapositiveNonPossonoAvereLoStessoNome()
    {
        var deck = Deck("slide1", "slide2", "slide3");
        deck.Slides[0].Name = "Introduzione";

        Assert.False(SlideNames.IsAvailable(deck, deck.Slides[1], "Introduzione"));
        Assert.False(SlideNames.IsAvailable(deck, deck.Slides[1], "slide3"));
        Assert.False(SlideNames.IsAvailable(deck, deck.Slides[1], "slide1"));
        Assert.True(SlideNames.IsAvailable(deck, deck.Slides[1], "slide2"));
        Assert.False(SlideNames.IsAvailable(deck, deck.Slides[1], "   "));
        Assert.True(SlideNames.IsAvailable(deck, deck.Slides[0], "Introduzione"));
        Assert.True(SlideNames.IsAvailable(deck, deck.Slides[1], "Conclusioni"));
    }
}

using PulseOffice.Presentations;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.Tests;

public sealed class ThemeChoicesTests
{
    private readonly ThemeCatalog _catalog = new();
    private readonly MasterFactory _masters = new();

    private ThemeChoices Choices() => new(_catalog, _masters);

    private Presentation Foreign()
    {
        var theme = new Theme { Name = "Office Theme", Colors = new ColorScheme { Accent1 = 0xFF4472C4 } };
        return new Presentation { Theme = theme, Master = _masters.CreateStandard(theme) };
    }

    [Fact]
    public void IlTemaDelDocumentoComparePrimaESelezionato()
    {
        var deck = Foreign();
        var choices = Choices().For(deck);

        Assert.Equal(ThemeOrigin.Document, choices[0].Origin);
        Assert.Equal("Office Theme", choices[0].Theme.Name);
        Assert.Equal(_catalog.Themes.Count + 1, choices.Count);
        Assert.Equal(0, Choices().IndexOfCurrent(deck, choices));
    }

    [Fact]
    public void DopoUnTemaDelCatalogoSiPuoTornareAQuelloDelDocumento()
    {
        var deck = Foreign();
        var originalMaster = deck.Master;
        var service = Choices();
        var catalogChoice = service.For(deck).First(c => c.Origin == ThemeOrigin.Catalog);

        service.Remember(deck);
        var design = service.Design(deck, catalogChoice);
        deck.Theme = design.Theme;
        deck.Master = design.Master;

        var choices = service.For(deck);
        Assert.Equal(1, service.IndexOfCurrent(deck, choices));
        var back = service.Design(deck, choices[0]);
        Assert.Equal("Office Theme", back.Theme.Name);
        Assert.Same(originalMaster, back.Master);
    }

    [Fact]
    public void UnDocumentoConUnTemaDelCatalogoNonHaSezioneDelDocumento()
    {
        var theme = _catalog.Default;
        var deck = new Presentation { Theme = theme, Master = _masters.CreateStandard(theme) };
        Assert.All(Choices().For(deck), choice => Assert.Equal(ThemeOrigin.Catalog, choice.Origin));
    }

    [Fact]
    public void IlTitoloDelMasterStandardEAncoratoComeInPowerPoint()
    {
        var master = _masters.CreateStandard(_catalog.Default);
        Assert.Equal(TextAnchor.Bottom, master.Template(SlideLayout.TitleSlide).Placeholders.Single(p => p.Role == PlaceholderRole.Title).Frame.Anchor);
        Assert.Equal(TextAnchor.Middle, master.Template(SlideLayout.TitleAndContent).Placeholders.Single(p => p.Role == PlaceholderRole.Title).Frame.Anchor);
        Assert.Equal(TextAnchor.Top, master.Template(SlideLayout.TitleAndContent).Placeholders.Single(p => p.Role == PlaceholderRole.Body).Frame.Anchor);

        var slide = new SlideFactory().Create("s1", master, SlideLayout.TitleSlide);
        Assert.Equal(TextAnchor.Bottom, slide.Elements.OfType<TextElement>().First(e => e.Text.Role == PlaceholderRole.Title).Text.Frame.Anchor);
    }
}

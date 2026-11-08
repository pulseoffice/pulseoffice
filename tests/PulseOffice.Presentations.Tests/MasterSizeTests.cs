using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class MasterSizeTests
{
    [Fact]
    public void UnMasterStandardSiAdattaAlFormatoDellaPresentazione()
    {
        var theme = new ThemeCatalog().Themes[0];
        var master = new MasterFactory().CreateStandard(theme, SlideSize.Standard);

        Assert.All(master.Layouts.SelectMany(layout => layout.Placeholders), placeholder =>
            Assert.True(placeholder.Bounds.Left.Value + placeholder.Bounds.Width.Value <= SlideSize.Standard.Width.Value));
    }

    [Fact]
    public void UnTemaDelCatalogoConservaIdentificativoEFormatoDelMaster()
    {
        var catalog = new ThemeCatalog();
        var factory = new MasterFactory();
        var deck = new Presentation { Size = SlideSize.Standard };
        deck.Master = factory.CreateStandard(catalog.Themes[0], SlideSize.Standard) with { Id = "Default" };
        var choices = new ThemeChoices(catalog, factory);

        var design = choices.Design(deck, new ThemeChoice(catalog.Themes[1], ThemeOrigin.Catalog));

        Assert.Equal("Default", design.Master.Id);
        Assert.Equal(deck.Master.Layouts[0].Placeholders[0].Bounds, design.Master.Layouts[0].Placeholders[0].Bounds);
    }

    [Fact]
    public void LaGraficaDelMasterCopreTuttaLaDiapositivaAncheIn43()
    {
        var theme = new ThemeCatalog().Themes.First(t => t.Decoration == ThemeDecorationKind.SideBand);
        var master = new MasterFactory().CreateStandard(theme, SlideSize.Standard);

        var band = master.Objects[0].Bounds;
        Assert.Equal(0, band.Top.Value);
        Assert.InRange(band.Height.Value, SlideSize.Standard.Height.Value - 1, SlideSize.Standard.Height.Value + 1);
        Assert.Equal(0, band.Left.Value);
    }
}

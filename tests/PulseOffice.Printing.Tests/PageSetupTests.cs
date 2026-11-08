using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Printing.Tests;

public class PageSetupTests
{
    private static readonly IPaperCatalog Papers = new PaperCatalog();

    private static PageSetup Standard() => new() { Paper = Papers.Default };

    [Fact]
    public void LOrizzontaleScambiaLatiSenzaCambiareLaCarta()
    {
        var portrait = Standard();
        var landscape = portrait with { Orientation = PageOrientation.Landscape };

        Assert.Equal(portrait.PageWidth, landscape.PageHeight);
        Assert.Equal(portrait.PageHeight, landscape.PageWidth);
        Assert.Equal(portrait.Paper, landscape.Paper);
    }

    [Fact]
    public void LAreaDelContenutoEscludeMarginiEIntestazioni()
    {
        var setup = Standard();
        var area = setup.ContentArea;

        Assert.Equal(setup.Margins.Left, area.Left);
        Assert.Equal(setup.Margins.Top + setup.HeaderHeight, area.Top);
        Assert.Equal((setup.PageWidth - setup.Margins.Horizontal).Value, area.Width.Value);
        Assert.Equal(
            (setup.PageHeight - setup.Margins.Vertical - setup.HeaderHeight - setup.FooterHeight).Value,
            area.Height.Value);
    }

    [Fact]
    public void IlPiedeStaSopraIlMargineInferiore()
    {
        var setup = Standard();

        Assert.Equal((setup.PageHeight - setup.Margins.Bottom).Value, setup.FooterArea.Bottom.Value);
    }

    [Fact]
    public void A4MisuraDuecentoDieciPerDuecentoNovantasette()
    {
        var a4 = Papers.Find("a4");

        Assert.NotNull(a4);
        Assert.Equal(210, a4.Width.ToCentimeters() * 10, 1);
        Assert.Equal(297, a4.Height.ToCentimeters() * 10, 1);
    }

    [Fact]
    public void UnaCartaSconosciutaNonEsiste()
    {
        Assert.Null(Papers.Find("papiro"));
    }
}

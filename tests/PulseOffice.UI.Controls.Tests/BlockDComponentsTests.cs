using Avalonia;
using PulseOffice.UI.Controls.Fields;
using PulseOffice.UI.Controls.Printing;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class BlockDComponentsTests
{
    private readonly HeaderFooterTokens _tokens = new();

    private readonly IReadOnlyList<HeaderFooterField> _fields =
    [
        new("&P", "Pagina", "", "1"),
        new("&N", "Pagine", "", "3"),
        new("&A", "Foglio", "", "Vendite"),
    ];

    [Fact]
    public void ICodiciDiventanoSegnapostoLeggibiliEPoiTornano()
    {
        const string code = "Pagina &P di &N — &A";
        var display = _tokens.ToDisplay(code, _fields);
        Assert.Equal("Pagina [Pagina] di [Pagine] — [Foglio]", display);
        Assert.Equal(code, _tokens.ToCode(display, _fields));
        Assert.Equal("Pagina 1 di 3 — Vendite", _tokens.Expand(code, _fields));
    }

    [Fact]
    public void LaEcommercialeLetteraleSiConserva()
    {
        Assert.Equal("R&D", _tokens.ToDisplay("R&&D", _fields));
        Assert.Equal("R&&D", _tokens.ToCode("R&D", _fields));
        Assert.Equal("&B&P", _tokens.ToCode("&B[Pagina]", _fields));
    }

    [Fact]
    public void IlQuadranteMezzoCerchioVaDaMenoNovantaANovanta()
    {
        var geometry = new AngleDialGeometry();
        var area = new Rect(0, 0, 64, 112);
        Assert.True(geometry.IsHalf(-90, 90));
        var centre = geometry.Centre(area, -90, 90);
        Assert.Equal(0, geometry.AngleAt(area, -90, 90, new Point(centre.X + 40, centre.Y), 1), 6);
        Assert.Equal(90, geometry.AngleAt(area, -90, 90, new Point(centre.X, centre.Y - 40), 1), 6);
        Assert.Equal(-45, geometry.AngleAt(area, -90, 90, new Point(centre.X + 30, centre.Y + 30), 5), 6);
        Assert.Equal(90, geometry.AngleAt(area, -90, 90, new Point(centre.X - 30, centre.Y - 5), 1), 6);
        var tip = geometry.Tip(area, -90, 90, 90);
        Assert.Equal(centre.X, tip.X, 6);
        Assert.True(tip.Y < centre.Y);
    }

    [Fact]
    public void IlQuadranteInteroAvvolgeLAngolo()
    {
        var geometry = new AngleDialGeometry();
        var area = new Rect(0, 0, 100, 100);
        Assert.False(geometry.IsHalf(0, 360));
        Assert.Equal(270, geometry.AngleAt(area, 0, 360, new Point(50, 90), 1), 6);
    }
}

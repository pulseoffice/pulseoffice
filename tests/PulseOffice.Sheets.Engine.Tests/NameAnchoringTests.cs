using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class NameAnchoringTests
{
    private readonly ActiveCellNameAnchoring _anchoring = new();

    [Fact]
    public void UnRiferimentoRelativoScrittoAManoSiAncoraAllaCellaAttiva()
    {
        Assert.Equal("A1", _anchoring.FromCell("B3", new CellAddress(2, 1)));
        Assert.Equal("B3", _anchoring.ToCell("A1", new CellAddress(2, 1)));
    }

    [Fact]
    public void IRiferimentiAssolutiNonCambiano()
    {
        Assert.Equal("$B$3", _anchoring.FromCell("$B$3", new CellAddress(4, 4)));
    }

    [Fact]
    public void SopraLaCellaAttivaSiGiraAlBordoDelFoglio()
    {
        var above = _anchoring.FromCell("A1", new CellAddress(1, 0));
        Assert.Equal("A1", _anchoring.ToCell(above, new CellAddress(1, 0)));
    }
}

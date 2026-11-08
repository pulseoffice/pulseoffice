using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class ValidationPlacementTests
{
    private readonly ValidationPlacement _placement = new();

    private static CellRange Column(int firstRow, int lastRow) => new(new CellAddress(firstRow, 0), new CellAddress(lastRow, 0));

    [Fact]
    public void LaNuovaRegolaRitagliaQuellaCheLaContiene()
    {
        var wide = new DataValidation { Range = Column(0, 9), Kind = ValidationKind.Custom, FirstFormula = "A1>0" };
        var narrow = new DataValidation { Range = Column(4, 5), Kind = ValidationKind.List, ListValues = ["x"] };

        var placed = _placement.Place([wide], narrow.Range, narrow);

        Assert.Equal([Column(0, 3), Column(6, 9), Column(4, 5)], placed.Select(rule => rule.Range));
        Assert.Equal("A1>0", placed[0].FirstFormula);
        Assert.Equal("A7>0", placed[1].FirstFormula);
        Assert.Same(narrow, placed[2]);
    }

    [Fact]
    public void CancellareUnaParteLasciaIlResto()
    {
        var wide = new DataValidation { Range = Column(0, 9), Kind = ValidationKind.WholeNumber, FirstFormula = "1", SecondFormula = "5" };
        var other = new DataValidation { Range = new CellRange(new CellAddress(0, 3), new CellAddress(2, 3)), Kind = ValidationKind.Decimal };

        var placed = _placement.Place([wide, other], Column(0, 0), null);

        Assert.Equal([Column(1, 9), other.Range], placed.Select(rule => rule.Range));
        Assert.Equal("1", placed[0].FirstFormula);
        Assert.Empty(_placement.Place([wide], Column(0, 20), null));
    }
}

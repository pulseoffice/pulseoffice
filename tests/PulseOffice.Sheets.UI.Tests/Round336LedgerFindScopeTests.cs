using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class Round336LedgerFindScopeTests
{
    private static readonly CellRange Block = new(new CellAddress(0, 0), new CellAddress(4, 1));

    [Fact]
    public void A_multi_cell_selection_restricts_the_search_and_survives_landing_on_a_match()
    {
        var scope = new LedgerFindScope();

        Assert.Equal(Block, scope.For(Block, unrestricted: false));
        scope.Landed(new CellAddress(2, 1));

        Assert.Equal(Block, scope.For(CellRange.Single(new CellAddress(2, 1)), unrestricted: false));
    }

    [Fact]
    public void Moving_elsewhere_or_searching_all_sheets_drops_the_restriction()
    {
        var scope = new LedgerFindScope();
        scope.For(Block, unrestricted: false);
        scope.Landed(new CellAddress(2, 1));

        Assert.Null(scope.For(CellRange.Single(new CellAddress(9, 9)), unrestricted: false));
        Assert.Null(scope.For(Block, unrestricted: true));
    }
}

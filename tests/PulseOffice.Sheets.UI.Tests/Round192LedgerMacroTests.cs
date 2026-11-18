using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class Round192LedgerMacroTests
{
    [Fact]
    public void Find_and_replace_ignore_case_by_default_and_match_it_on_request()
    {
        using var f = new LedgerFacadeContractTests.Fixture();
        f.Sheet.WriteRange("A1", [["North"], ["north"], ["South"]]);

        Assert.Equal(["A1", "A2"], f.Sheet.Find("NORTH"));
        Assert.Equal(["A2"], f.Sheet.Find("north", matchCase: true));
        Assert.Equal(1, f.Sheet.Replace("north", "East", matchCase: true));
        Assert.Equal("North", f.Sheet.Read("A1"));
        Assert.Equal(1, f.Sheet.Replace("NORTH", "West"));
        Assert.Equal("West", f.Sheet.Read("A1"));
        Assert.Equal("East", f.Sheet.Read("A2"));
    }

    [Fact]
    public void Freeze_is_view_state_and_works_on_a_read_only_workbook_without_an_undo_step()
    {
        using var f = new LedgerFacadeContractTests.Fixture();
        f.IsReadOnly = true;

        f.Sheet.Freeze(1, 2);

        Assert.Equal((1, 2), f.Frozen);
        Assert.False(f.History.CanUndo);
    }
}

using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round331LedgerFindTests
{
    private readonly WorkbookSearch _search = new();

    private Workbook Build()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("One");
        sheet[new CellAddress(0, 0)] = new Cell { Value = CellValue.Text("cat") };
        sheet[new CellAddress(0, 1)] = new Cell { Value = CellValue.Text("cat") };
        sheet[new CellAddress(1, 0)] = new Cell { Value = CellValue.Text("cat food") };
        return workbook;
    }

    [Fact]
    public void Replace_all_by_format_touches_only_cells_with_that_format()
    {
        var workbook = Build();
        var bold = workbook.Styles.Intern(new CellStyle { Font = CellFont.Default with { Bold = true } });
        workbook.Sheets[0].SetStyle(new CellAddress(0, 1), bold);

        var replaced = _search.ReplaceAll(workbook, 0, "cat", "dog",
            SearchOptions.Default with { Style = new StyleFilter { Bold = true } });

        Assert.Equal(1, replaced);
        Assert.Equal("cat", workbook.Sheets[0][new CellAddress(0, 0)].Value.ToString());
        Assert.Equal("dog", workbook.Sheets[0][new CellAddress(0, 1)].Value.ToString());
    }

    [Fact]
    public void Regular_expression_with_entire_cell_matches_only_whole_contents()
    {
        var found = _search.FindAll(Build(), 0, "c.t", SearchOptions.Default with { UseRegex = true, WholeCell = true });

        Assert.Equal(2, found.Count);
        Assert.DoesNotContain(found, match => match.Address == new CellAddress(1, 0));
    }

    [Fact]
    public void Matches_are_walked_by_rows_or_by_columns_and_wrap()
    {
        var matches = _search.FindAll(Build(), 0, "cat");
        var order = new WorkbookMatchOrder();

        Assert.Equal(new CellAddress(0, 1), order.Next(matches, 0, new CellAddress(0, 0), byColumns: false, backwards: false)!.Address);
        Assert.Equal(new CellAddress(1, 0), order.Next(matches, 0, new CellAddress(0, 0), byColumns: true, backwards: false)!.Address);
        Assert.Equal(new CellAddress(0, 0), order.Next(matches, 0, new CellAddress(1, 0), byColumns: false, backwards: false)!.Address);
        Assert.Equal(new CellAddress(1, 0), order.Next(matches, 0, new CellAddress(0, 0), byColumns: false, backwards: true)!.Address);
    }
}

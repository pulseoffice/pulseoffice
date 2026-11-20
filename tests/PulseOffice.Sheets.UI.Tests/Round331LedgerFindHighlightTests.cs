using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class Round331LedgerFindHighlightTests
{
    [Fact]
    public void Highlights_follow_the_query_and_the_sheet_content()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("One");
        workbook.AddSheet("Two");
        sheet[new CellAddress(2, 1)] = new Cell { Value = CellValue.Text("needle") };
        var highlights = new LedgerFindHighlights();
        var changes = 0;
        highlights.Changed += (_, _) => changes++;

        highlights.Show(workbook, new LedgerFindQuery("needle", SearchOptions.Default, 0));

        Assert.Equal(new CellAddress(2, 1), Assert.Single(highlights.Found(workbook, 0)));
        Assert.Empty(highlights.Found(workbook, 1));
        sheet[new CellAddress(3, 3)] = new Cell { Value = CellValue.Text("needle too") };
        Assert.Equal(2, highlights.Found(workbook, 0).Count);
        highlights.Clear(workbook);
        Assert.Empty(highlights.Found(workbook, 0));
        Assert.Equal(2, changes);
    }
}

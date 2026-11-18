using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Pivot.Tests;

public class Round175PivotRefreshTests
{
    private static readonly PivotLabels Labels = new("Total", "Total {0}");

    private static PivotCoordinator Coordinator() =>
        new(new PivotEngine(new PivotAggregator(), new PivotGrouping(), new PivotFormulas(new PulseOffice.Sheets.Engine.Parsing.FormulaParser())), new PivotRenderer());

    private static Workbook Data(params string[] regions)
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Region"));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Text("Amount"));
        for (var row = 0; row < regions.Length; row++)
        {
            sheet.SetValue(new CellAddress(row + 1, 0), CellValue.Text(regions[row]));
            sheet.SetValue(new CellAddress(row + 1, 1), CellValue.Number(1));
        }
        return workbook;
    }

    private static PivotDefinition Definition(Workbook workbook, int rows, string destination = "Pivot") => new()
    {
        Id = "p1",
        Name = "P",
        SourceSheet = workbook.Sheets[0].Name,
        SourceRange = new CellRange(new CellAddress(0, 0), new CellAddress(rows, 1)),
        DestinationSheet = destination,
        Destination = CellAddress.First,
        Fields =
        [
            new PivotField { SourceName = "Region", SourceColumnIndex = 0, Area = PivotArea.Row },
            new PivotField { SourceName = "Amount", SourceColumnIndex = 1, Area = PivotArea.Value },
        ],
    };

    [Fact]
    public void A_refresh_that_grows_does_not_overwrite_cells_below_the_pivot()
    {
        var workbook = Data("A", "A");
        var coordinator = Coordinator();
        var first = coordinator.Apply(workbook, Definition(workbook, 2), Labels).Require();
        var pivot = workbook.FindSheet("Pivot")!;
        var below = new CellAddress(first.End.Row + 1, 0);
        pivot.SetValue(below, CellValue.Text("note"));
        workbook.Sheets[0].SetValue(new CellAddress(3, 0), CellValue.Text("B"));
        workbook.Sheets[0].SetValue(new CellAddress(3, 1), CellValue.Number(1));

        var result = coordinator.Apply(workbook, Definition(workbook, 3), Labels);

        Assert.False(result.Succeeded);
        Assert.Equal("note", pivot.GetValue(below).AsText());
    }

    [Fact]
    public void Moving_a_pivot_to_another_sheet_clears_the_old_output_not_the_new_sheet()
    {
        var workbook = Data("A", "B");
        var coordinator = Coordinator();
        coordinator.Apply(workbook, Definition(workbook, 2), Labels);
        var other = workbook.AddSheet("Other");
        other.SetValue(new CellAddress(10, 0), CellValue.Text("keep"));

        var moved = Definition(workbook, 2, "Other") with { Destination = new CellAddress(0, 0) };
        var result = coordinator.Apply(workbook, moved, Labels);

        Assert.True(result.Succeeded);
        Assert.True(workbook.FindSheet("Pivot")!.GetValue(CellAddress.First).IsEmpty);
        Assert.Equal("keep", other.GetValue(new CellAddress(10, 0)).AsText());
    }
}

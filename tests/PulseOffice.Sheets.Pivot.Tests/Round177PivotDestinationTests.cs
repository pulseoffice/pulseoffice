using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Pivot.Tests;

public class Round177PivotDestinationTests
{
    private static readonly PivotLabels Labels = new("Total", "Total {0}");

    private static readonly PivotCoordinator Coordinator =
        new(new PivotEngine(new PivotAggregator(), new PivotGrouping(), new PivotFormulas(new PulseOffice.Sheets.Engine.Parsing.FormulaParser())), new PivotRenderer());

    private static Workbook Data()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Region"));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Text("Amount"));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Text("A"));
        sheet.SetValue(new CellAddress(1, 1), CellValue.Number(1));
        return workbook;
    }

    private static PivotDefinition Definition(Workbook workbook, PivotField[] fields) => new()
    {
        Id = "p1", Name = "P", SourceSheet = workbook.Sheets[0].Name,
        SourceRange = new CellRange(new CellAddress(0, 0), new CellAddress(1, 1)),
        DestinationSheet = "Pivot", Destination = CellAddress.First, Fields = fields,
    };

    private static PivotField[] Fields =>
    [
        new PivotField { SourceName = "Region", SourceColumnIndex = 0, Area = PivotArea.Row },
        new PivotField { SourceName = "Amount", SourceColumnIndex = 1, Area = PivotArea.Value },
    ];

    [Fact]
    public void A_refresh_does_not_add_the_missing_destination_sheet_to_a_protected_structure()
    {
        var workbook = Data();
        workbook.Protection = workbook.Protection with { Structure = true };

        var result = Coordinator.Apply(workbook, Definition(workbook, Fields), Labels);

        Assert.False(result.Succeeded);
        Assert.Equal("PVT007", result.Diagnostics[0].Code);
        Assert.Null(workbook.FindSheet("Pivot"));
    }

    [Fact]
    public void A_pivot_that_fails_its_checks_leaves_no_new_sheet_behind()
    {
        var workbook = Data();
        var definition = Definition(workbook, Fields) with { Destination = new CellAddress(0, CellAddress.MaxColumns - 1) };

        var result = Coordinator.Apply(workbook, definition, Labels);

        Assert.False(result.Succeeded);
        Assert.Null(workbook.FindSheet("Pivot"));
    }
}

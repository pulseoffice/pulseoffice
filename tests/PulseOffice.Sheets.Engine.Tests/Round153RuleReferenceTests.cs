using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Pivot;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round153RuleReferenceTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static ConditionalRule Scale(string range, string minimum, string maximum)
    {
        var parts = range.Split(':');
        return new ConditionalRule
        {
            Range = new CellRange(A(parts[0]), A(parts[1])),
            Visual = ConditionalVisual.ColorScale,
            ScaleColors = ["#000000", "#FFFFFF"],
            Thresholds =
            [
                new ConditionalThreshold(ConditionalThresholdKind.Formula, Formula: minimum),
                new ConditionalThreshold(ConditionalThresholdKind.Formula, Formula: maximum),
            ],
        };
    }

    [Fact]
    public void Inserting_rows_rewrites_the_formula_thresholds_of_a_colour_scale()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.ConditionalRules.Add(Scale("A10:A20", "$C$5", "$C$6"));

        new SheetStructure().InsertRows(engine, 0, 0, 2);

        var rule = Assert.Single(sheet.ConditionalRules);
        Assert.Equal(["$C$7", "$C$8"], rule.Thresholds.Select(threshold => threshold.Formula));
    }

    [Fact]
    public void Inserting_rows_rewrites_the_formula_thresholds_of_rules_on_other_sheets()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var summary = workbook.AddSheet("Summary");
        summary.ConditionalRules.Add(Scale("A1:A5", "Sheet1.$C$5", "Sheet1.$C$6"));

        new SheetStructure().InsertRows(engine, 0, 0, 2);

        Assert.Equal(["Sheet1.$C$7", "Sheet1.$C$8"], summary.ConditionalRules[0].Thresholds.Select(threshold => threshold.Formula));
    }

    [Fact]
    public void Renaming_a_sheet_rewrites_the_formula_thresholds_that_point_to_it()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var data = workbook.Sheets[0];
        var summary = workbook.AddSheet("Summary");
        summary.ConditionalRules.Add(Scale("A1:A5", "Sheet1.$C$5", "Sheet1.$C$6"));

        var renaming = new SheetRenaming();
        var plan = renaming.Plan(workbook, data, "Data");
        renaming.Apply(engine, plan);

        Assert.All(summary.ConditionalRules[0].Thresholds, threshold => Assert.StartsWith("Data.", threshold.Formula));

        renaming.Revert(engine, plan);

        Assert.Equal(["Sheet1.$C$5", "Sheet1.$C$6"], summary.ConditionalRules[0].Thresholds.Select(threshold => threshold.Formula));
    }

    [Fact]
    public void Moving_the_cells_a_threshold_refers_to_rewrites_the_threshold()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.ConditionalRules.Add(Scale("A1:A5", "$C$1", "$C$2"));

        new SheetRangeMove(new SheetStructure()).Move(engine,
            new RangeMove(sheet.Name, new CellRange(A("C1"), A("C2")), sheet.Name, A("E10")));

        Assert.Equal(["$E$10", "$E$11"], sheet.ConditionalRules[0].Thresholds.Select(threshold => threshold.Formula));
    }

    [Fact]
    public void Renaming_a_sheet_keeps_the_pivot_tables_that_read_from_it_or_live_on_it()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var data = workbook.Sheets[0];
        var report = workbook.AddSheet("Report");
        workbook.Pivots.Add(new PivotDefinition
        {
            Id = "p", Name = "p", SourceSheet = data.Name, SourceRange = new CellRange(A("A1"), A("B9")),
            Destination = A("A1"), DestinationSheet = report.Name,
        });
        workbook.Pivots.Add(new PivotDefinition
        {
            Id = "q", Name = "q", SourceSheet = data.Name, SourceRange = new CellRange(A("A1"), A("B9")),
            Destination = A("H1"), DestinationSheet = data.Name,
        });

        var renaming = new SheetRenaming();
        var plan = renaming.Plan(workbook, data, "Data");
        renaming.Apply(engine, plan);

        Assert.All(workbook.Pivots, pivot => Assert.Equal("Data", pivot.SourceSheet));
        Assert.Equal(["Report", "Data"], workbook.Pivots.Select(pivot => pivot.DestinationSheet));

        renaming.Revert(engine, plan);

        Assert.All(workbook.Pivots, pivot => Assert.Equal("Sheet1", pivot.SourceSheet));
        Assert.Equal(["Report", "Sheet1"], workbook.Pivots.Select(pivot => pivot.DestinationSheet));
    }
}

using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round130ConsolidationLinkTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static CellRange R(int top, int left, int bottom, int right) => new(new CellAddress(top, left), new CellAddress(bottom, right));

    private (Workbook Workbook, CalculationEngine Engine) Book()
    {
        var workbook = Workbook.CreateDefault();
        workbook.AddSheet("North side");
        workbook.AddSheet("Total");
        var engine = fixture.CreateEngine(workbook);
        var first = workbook.Sheets[0];
        var second = workbook.Sheets[1];
        first.SetValue(new CellAddress(0, 0), CellValue.Text("Apples"));
        first.SetValue(new CellAddress(0, 1), CellValue.Number(10));
        first.SetValue(new CellAddress(1, 0), CellValue.Text("Pears"));
        first.SetValue(new CellAddress(1, 1), CellValue.Number(4));
        second.SetValue(new CellAddress(0, 0), CellValue.Text("Pears"));
        second.SetValue(new CellAddress(0, 1), CellValue.Number(6));
        engine.RecalculateAll();
        return (workbook, engine);
    }

    [Fact]
    public void Linked_results_are_formulas_over_the_source_cells()
    {
        var (workbook, _) = Book();

        var plan = new SheetConsolidation().Plan(workbook,
            new ConsolidationRequest([new ConsolidationSource(0, R(0, 0, 1, 1)), new ConsolidationSource(1, R(0, 0, 0, 1))], new CellAddress(0, 0))
            {
                LabelsInFirstColumn = true,
                LinkToSource = true,
            });

        Assert.Equal("=SUM('Sheet1'!$B$1)", plan.Formulas[new CellAddress(0, 1)]);
        Assert.Equal("=SUM('Sheet1'!$B$2;'North side'!$B$1)", plan.Formulas[new CellAddress(1, 1)]);
        Assert.Equal("Pears", plan.Values[new CellAddress(1, 0)].AsText());
        Assert.False(plan.Formulas.ContainsKey(new CellAddress(1, 0)));
    }

    [Fact]
    public void Linked_formulas_follow_later_changes_of_the_sources()
    {
        var (workbook, engine) = Book();
        var plan = new SheetConsolidation().Plan(workbook,
            new ConsolidationRequest([new ConsolidationSource(0, R(0, 1, 1, 1)), new ConsolidationSource(1, R(0, 1, 0, 1))], CellAddress.First)
            {
                Function = SubtotalFunction.Max,
                LinkToSource = true,
            });
        foreach (var (address, formula) in plan.Formulas) engine.SetFormula(new SheetCell(2, address), formula);
        engine.RecalculateAll();
        Assert.Equal(10, workbook.Sheets[2].GetValue(CellAddress.First).AsNumber());

        engine.SetValue(new SheetCell(1, new CellAddress(0, 1)), CellValue.Number(25));
        engine.RecalculateAll();

        Assert.Equal(25, workbook.Sheets[2].GetValue(CellAddress.First).AsNumber());
        Assert.Equal("=MAX('Sheet1'!$B$1;'North side'!$B$1)", plan.Formulas[CellAddress.First]);
    }

    [Fact]
    public void Without_link_no_formula_is_planned()
    {
        var (workbook, _) = Book();

        var plan = new SheetConsolidation().Plan(workbook,
            new ConsolidationRequest([new ConsolidationSource(0, R(0, 1, 1, 1))], CellAddress.First));

        Assert.Empty(plan.Formulas);
        Assert.Equal(10, plan.Values[CellAddress.First].AsNumber());
    }
}

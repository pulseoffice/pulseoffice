using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round169StructureGrowthTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static readonly CellStyle Marked = CellStyle.Default with { BackgroundHex = "#FF0000" };

    private (Workbook Workbook, PulseOffice.Sheets.Engine.Recalculation.CalculationEngine Engine) Build(int rows)
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        for (var row = 0; row < rows; row++)
        {
            sheet.ConditionalRules.Add(new ConditionalRule
            {
                Range = CellRange.Single(new CellAddress(row, 0)), Operator = ConditionOperator.GreaterThan, Operands = ["0"], Style = Marked,
            });
            sheet.Merges.Add(new CellRange(new CellAddress(row, 2), new CellAddress(row, 3)));
        }

        return (workbook, engine);
    }

    [Fact]
    public void Deleting_the_first_half_of_rows_with_a_rule_and_a_merge_on_each_stays_linear()
    {
        var structure = new SheetStructure();
        new PulseOffice.Testing.GrowthCheck().Linear(rows =>
        {
            var (_, engine) = Build(rows);
            return () => structure.DeleteRows(engine, 0, 0, rows / 2);
        }, 4_000);

        var (workbook, sized) = Build(10);
        structure.DeleteRows(sized, 0, 0, 5);
        var sheet = workbook.Sheets[0];

        Assert.Equal(5, sheet.ConditionalRules.Count);
        Assert.Equal(CellRange.Single(new CellAddress(0, 0)), sheet.ConditionalRules[0].Range);
        Assert.Equal(CellRange.Single(new CellAddress(4, 0)), sheet.ConditionalRules[4].Range);
        Assert.Equal(5, sheet.Merges.Count);
        Assert.Equal(new CellRange(new CellAddress(4, 2), new CellAddress(4, 3)), sheet.Merges.At(new CellAddress(4, 3)));
    }
}

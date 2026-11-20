using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Formatting;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round299ConditionalFollowTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static CellRange R(string a1)
    {
        Assert.True(CellRange.TryParseA1(a1, out var range));
        return range;
    }

    [Fact]
    public void Deleting_the_first_rows_of_a_formula_rule_reanchors_its_relative_formula()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.ConditionalRules.Add(new ConditionalRule { Range = R("A2:A6"), Formula = "=A2>B2" });

        new SheetStructure().DeleteRows(engine, 0, 1, 2);

        var rule = Assert.Single(sheet.ConditionalRules);
        Assert.Equal(R("A2:A4"), rule.Range);
        Assert.Equal("=A2>B2", rule.Formula);
    }

    [Fact]
    public void Inserting_columns_before_a_rule_shifts_its_ranges_and_references()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.ConditionalRules.Add(new ConditionalRule { Range = R("B1:B3"), AdditionalRanges = [R("D1:D3")], Formula = "=B1>$C$1" });

        new SheetStructure().InsertColumns(engine, 0, 0, 1);

        var rule = Assert.Single(sheet.ConditionalRules);
        Assert.Equal([R("C1:C3"), R("E1:E3")], rule.Ranges);
        Assert.Equal("=C1>$D$1", rule.Formula);
    }

    [Fact]
    public void Moving_part_of_a_rule_splits_it_and_the_moved_part_follows_the_cells()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.ConditionalRules.Add(new ConditionalRule { Range = R("A1:A4"), Operands = ["1"] });

        new SheetRangeMove(new SheetStructure()).Move(engine, new RangeMove(sheet.Name, R("A3:A4"), sheet.Name, R("F10").Start));

        Assert.Contains(sheet.ConditionalRules, rule => rule.Ranges.SequenceEqual([R("A1:A2")]));
        Assert.Contains(sheet.ConditionalRules, rule => rule.Ranges.SequenceEqual([R("F10:F11")]));
    }
}

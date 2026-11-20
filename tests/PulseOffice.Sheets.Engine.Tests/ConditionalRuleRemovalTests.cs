using PulseOffice.Document.Commands;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Formatting;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class ConditionalRuleRemovalTests
{
    private static CellRange R(string a1)
    {
        Assert.True(CellRange.TryParseA1(a1, out var range));
        return range;
    }

    [Fact]
    public void TogliendoSiGuardanoTutteLeAreeEAnnullandoTornaLOrdine()
    {
        var sheet = new Workbook().AddSheet("Dati");
        var first = new ConditionalRule { Range = R("A1:A5"), AdditionalRanges = [R("C1:C5")], Visual = ConditionalVisual.DataBar };
        var second = new ConditionalRule { Range = R("E1:E5"), Visual = ConditionalVisual.ColorScale };
        sheet.ConditionalRules.Add(first);
        sheet.ConditionalRules.Add(second);
        var history = new UndoStack();

        var count = new ConditionalRuleRemoval().Remove(sheet, R("C2"), "x", history);

        Assert.Equal(1, count);
        Assert.Equal([R("A1:A5"), R("C1:C1"), R("C3:C5")], sheet.ConditionalRules[0].Ranges);
        Assert.Same(second, sheet.ConditionalRules[1]);

        history.Undo();
        Assert.Equal([first, second], sheet.ConditionalRules);
    }

    [Fact]
    public void Clearing_the_first_cells_of_a_formula_rule_keeps_the_rest_with_its_formula_reanchored()
    {
        var sheet = new Workbook().AddSheet("Dati");
        sheet.ConditionalRules.Add(new ConditionalRule { Range = R("A1:A4"), Formula = "=A1>B1" });

        new ConditionalRuleRemoval().Remove(sheet, R("A1:A2"), "x");

        var rule = Assert.Single(sheet.ConditionalRules);
        Assert.Equal(R("A3:A4"), rule.Range);
        Assert.Equal("=A3>B3", rule.Formula);
    }
}

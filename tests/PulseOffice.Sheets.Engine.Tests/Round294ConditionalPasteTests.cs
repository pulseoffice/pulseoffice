using PulseOffice.Document.Commands;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Formatting;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round294ConditionalPasteTests
{
    private static CellRange R(string a1)
    {
        Assert.True(CellRange.TryParseA1(a1, out var range));
        return range;
    }

    [Fact]
    public void Pasting_formats_carries_the_conditional_rules_of_the_copied_cells_and_undo_removes_them()
    {
        var sheet = new Worksheet("Sheet1");
        sheet.ConditionalRules.Add(new ConditionalRule { Range = R("A1:A4"), Formula = "=A1>B1" });
        var clipboard = new SheetClipboard();
        var history = new UndoStack();

        var snapshot = clipboard.Copy(sheet, R("A3:A4"));
        clipboard.Paste(snapshot, sheet, R("C10").Start, R("A3").Start, PasteParts.StylesOnly, history);

        var pasted = Assert.Single(sheet.ConditionalRules, rule => rule.Range == R("C10:C11"));
        Assert.Equal("=C10>D10", pasted.Formula);
        history.Undo();
        Assert.Single(sheet.ConditionalRules);
    }

    [Fact]
    public void Pasting_values_only_leaves_the_conditional_rules_alone()
    {
        var sheet = new Worksheet("Sheet1");
        sheet.ConditionalRules.Add(new ConditionalRule { Range = R("A1:A2"), Operator = ConditionOperator.GreaterThan, Operands = ["1"] });
        var clipboard = new SheetClipboard();

        clipboard.Paste(clipboard.Copy(sheet, R("A1:A2")), sheet, R("E1").Start, R("A1").Start, PasteParts.ValuesOnly);

        Assert.Single(sheet.ConditionalRules);
    }
}

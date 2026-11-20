using PulseOffice.Document.Commands;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Formatting;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round299ConditionalInsertionTests
{
    private static readonly CellRange A1 = new(new CellAddress(0, 0), new CellAddress(0, 0));

    [Fact]
    public void A_new_rule_goes_first_and_the_others_are_renumbered_once_undo_restores_them()
    {
        var sheet = new Worksheet("Sheet1");
        var old = new ConditionalRule { Range = A1, Priority = 5, Operands = ["1"] };
        sheet.ConditionalRules.Add(old);
        var history = new UndoStack();

        new ConditionalRuleInsertion().InsertFirst(sheet, new ConditionalRule { Range = A1, Priority = 9, Operands = ["2"] }, "Add", history);

        Assert.Equal([0, 1], sheet.ConditionalRules.Select(rule => rule.Priority));
        Assert.Equal("2", sheet.ConditionalRules[0].Operands[0]);
        history.Undo();
        Assert.Same(old, Assert.Single(sheet.ConditionalRules));
    }
}

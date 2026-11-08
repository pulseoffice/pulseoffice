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
        Assert.Equal([second], sheet.ConditionalRules);

        history.Undo();
        Assert.Equal([first, second], sheet.ConditionalRules);
    }
}

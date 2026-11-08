using PulseOffice.Document.Commands;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class SheetSortWholeColumnTests
{
    [Fact]
    public void OrdinareUnaColonnaInteraToccaSoloLeRigheUsate()
    {
        var sheet = new Worksheet("Dati");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(3));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Number(1));
        sheet.SetValue(new CellAddress(2, 0), CellValue.Number(2));
        var history = new UndoStack();
        var column = new CellRange(new CellAddress(0, 0), new CellAddress(CellAddress.MaxRows - 1, CellAddress.MaxColumns - 1));

        new SheetSorter().Sort(sheet, column, [new SortKey(0)], history: history, historyLabel: "Sort");

        Assert.Equal(1d, sheet[new CellAddress(0, 0)].Value.AsNumber());
        Assert.Equal(3d, sheet[new CellAddress(2, 0)].Value.AsNumber());
        Assert.Equal("Sort", history.UndoDescription);
        history.Undo();
        Assert.Equal(3d, sheet[new CellAddress(0, 0)].Value.AsNumber());
    }
}

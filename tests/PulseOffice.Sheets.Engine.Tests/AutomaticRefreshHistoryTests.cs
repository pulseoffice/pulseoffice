using Xunit;
using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Tests;

public class AutomaticRefreshHistoryTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private readonly IAutomaticRefreshHistory _history = new AutomaticRefreshHistory(new WorkbookCheckpoints(new SheetSnapshots()));

    private static readonly AutomaticRefreshKey Key = new(0, new CellAddress(0, 0));

    [Fact]
    public void OgniAggiornamentoEUnAnnullamentoComeInCalc()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var stack = new UndoStack();
        var sheet = workbook.Sheets[0];

        _history.Record(stack, engine, workbook, Key, "auto", () => { sheet.SetValue(new CellAddress(0, 0), CellValue.Number(1)); return true; });
        _history.Record(stack, engine, workbook, Key, "auto", () => { sheet.SetValue(new CellAddress(0, 0), CellValue.Number(2)); return true; });

        stack.Undo();
        Assert.Equal(CellValue.Number(1), sheet.GetValue(new CellAddress(0, 0)));
        stack.Undo();
        Assert.False(stack.CanUndo);
        Assert.Equal(CellValue.Empty, sheet.GetValue(new CellAddress(0, 0)));
    }

    [Fact]
    public void UnAggiornamentoSenzaCambiamentiEntraComunqueNellaCronologiaComeInCalc()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var stack = new UndoStack();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(5));
        var state = stack.StatePoint;

        var changed = _history.Record(stack, engine, workbook, Key, "auto", () => { sheet.SetValue(new CellAddress(0, 0), CellValue.Number(5)); return true; });

        Assert.True(changed);
        Assert.NotSame(state, stack.StatePoint);
        Assert.True(stack.CanUndo);
    }

    [Fact]
    public void UnAggiornamentoFallitoNonEntraNellaCronologia()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var stack = new UndoStack();

        var changed = _history.Record(stack, engine, workbook, Key, "auto", () => false);

        Assert.False(changed);
        Assert.False(stack.CanUndo);
    }
}

using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.UI;

public interface ISheetValueWrites
{
    void Write(
        IActiveSheetContext context,
        CalculationEngine engine,
        IReadOnlyList<(CellAddress Address, CellValue Value)> values,
        string description);
}

public sealed class SheetValueWrites(ISheetSnapshots snapshots) : ISheetValueWrites
{
    public void Write(
        IActiveSheetContext context,
        CalculationEngine engine,
        IReadOnlyList<(CellAddress Address, CellValue Value)> values,
        string description)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(values);

        if (context.Workbook is not { } workbook || values.Count == 0) return;

        var sheetIndex = context.SheetIndex;
        var sheet = workbook.Sheets[sheetIndex];
        var before = snapshots.Capture(sheet);

        engine.Batch(() =>
        {
            foreach (var (address, value) in values)
                engine.SetValue(new SheetCell(sheetIndex, address), value);
        });

        var after = snapshots.Capture(sheet);

        context.History?.Push(new DelegateAction(
            description,
            redo: () => snapshots.Restore(engine, sheetIndex, after),
            undo: () => snapshots.Restore(engine, sheetIndex, before)));

        context.RequestRefresh();
    }
}

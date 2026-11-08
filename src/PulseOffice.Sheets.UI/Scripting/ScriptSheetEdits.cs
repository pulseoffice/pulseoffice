using PulseOffice.Document.Commands;
using PulseOffice.Sheets.Engine.Operations;

namespace PulseOffice.Sheets.UI.Scripting;

public interface IScriptSheetEdits
{
    T Record<T>(string label, Func<T> change, Func<T, bool> applied);
}

public sealed class ScriptSheetEdits(IActiveSheetContext context, ISheetSnapshots snapshots, ISheetRangeFilters filters) : IScriptSheetEdits
{
    public T Record<T>(string label, Func<T> change, Func<T, bool> applied)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(applied);

        if (context.Workbook is not { } workbook || context.Engine is not { } engine) return change();

        var index = context.SheetIndex;
        var sheet = workbook.Sheets[index];
        var before = Capture(sheet);
        var result = change();
        if (!applied(result)) return result;

        var after = Capture(sheet);
        context.History?.Push(new DelegateAction(
            label,
            redo: () => Restore(engine, sheet, index, after),
            undo: () => Restore(engine, sheet, index, before)));

        return result;
    }

    private SheetState Capture(Worksheet sheet) =>
        new(snapshots.Capture(sheet), [.. sheet.Scenarios], sheet.AutoFilter.Save());

    private void Restore(Engine.Recalculation.CalculationEngine engine, Worksheet sheet, int index, SheetState state)
    {
        snapshots.Restore(engine, index, state.Snapshot);
        sheet.Scenarios.Clear();
        sheet.Scenarios.AddRange(state.Scenarios);
        sheet.AutoFilter.Restore(state.Filter);

        filters.Refresh(context, sheet);
    }

    private sealed record SheetState(SheetSnapshot Snapshot, IReadOnlyList<Scenario> Scenarios, AutoFilterMemento Filter);
}

using PulseOffice.UI.Controls.Notifications;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Foundation.Modules;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.UI.Commands;

namespace PulseOffice.Sheets.UI;

public abstract class LedgerCommandSet(IActiveSheetContext context)
{
    protected IActiveSheetContext Context => context;

    protected CommandDefinition Prototype(string id) => new()
    {
        Id = new CommandId(id),
        Module = ModuleKind.Sheets,
        Execute = (_, _) => Task.CompletedTask,
    };

    protected void OutsideHistory(Action change)
    {
        if (Context.History is { } history) history.ApplyOutsideHistory(change);
        else change();
    }

    protected bool ObjectsEditable => SheetUnprotected;

    protected bool SheetUnprotected => Context.Workbook is { } workbook
        && Context.SheetIndex >= 0 && Context.SheetIndex < workbook.Sheets.Count
        && !workbook.Sheets[Context.SheetIndex].Protection.IsEnabled;

    protected bool StructureLocked => Context.Workbook?.Protection.Structure == true;

    protected bool RefuseStructureChange(PulseOffice.UI.Controls.Services.IStatusBarService status, PulseOffice.Localization.ITextCatalog texts)
    {
        if (!StructureLocked) return false;

        status.Report(texts["status.protection.structure"], NotificationSeverity.Error, LedgerRemedies.UnprotectStructure);
        return true;
    }

    protected CellRange? BoundedToUsed(Worksheet sheet, CellRange range)
    {
        if (sheet.UsedRange() is not { } used) return null;

        var start = new CellAddress(Math.Max(range.Start.Row, used.Start.Row), Math.Max(range.Start.Column, used.Start.Column));
        var end = new CellAddress(Math.Min(range.End.Row, used.End.Row), Math.Min(range.End.Column, used.End.Column));
        return start.Row <= end.Row && start.Column <= end.Column ? new CellRange(start, end) : null;
    }

    protected T Checkpointed<T>(IWorkbookCheckpoints checkpoints, string description, Func<T> change) =>
        Checkpointed(checkpoints, description, change, _ => true);

    protected T Checkpointed<T>(IWorkbookCheckpoints checkpoints, string description, Func<T> change, Func<T, bool> applied)
    {
        if (Context.Workbook is not { } workbook || Context.Engine is not { } engine || Context.History is not { } history)
            return change();

        var before = checkpoints.Capture(workbook);
        var result = change();
        if (!applied(result))
        {
            checkpoints.Restore(engine, workbook, before);
            return result;
        }

        var after = checkpoints.Capture(workbook);

        history.Push(new PulseOffice.Document.Commands.DelegateAction(description,
            redo: () => checkpoints.Restore(engine, workbook, after),
            undo: () => checkpoints.Restore(engine, workbook, before)));

        return result;
    }
}

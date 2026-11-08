using PulseOffice.Document.Commands;
using PulseOffice.Localization;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.UI;

public interface ISheetTextTransform
{
    bool ApplyToSelection(string historyLabel, Func<string, string> change);
}

public sealed class SheetTextTransform(
    IActiveSheetContext context,
    ISheetSnapshots snapshots,
    ISheetEditGuard guard,
    PulseOffice.UI.Controls.Services.IStatusBarService status,
    ITextCatalog texts) : ISheetTextTransform
{
    public bool ApplyToSelection(string historyLabel, Func<string, string> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (context.Workbook is not { } workbook) return false;
        if (context.Engine is not { } engine) return false;

        var sheetIndex = context.SheetIndex;
        var sheet = workbook.Sheets[sheetIndex];

        if (!guard.CanWrite(workbook, sheet, context.Selection))
        {
            status.Report(texts["status.protection.locked"], PulseOffice.UI.Controls.Notifications.NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
            return false;
        }

        var before = snapshots.Capture(sheet);
        var touched = false;

        foreach (var address in sheet.NonEmptyCellsIn(context.Selection).Select(pair => pair.Key).ToList())
        {
            var cell = sheet[address];
            if (cell.HasFormula || cell.Value.Kind != ValueKind.Text) continue;

            var text = cell.Value.AsText();
            var changed = change(text);

            if (changed == text) continue;

            engine.SetValue(new SheetCell(sheetIndex, address), CellValue.Text(changed));
            touched = true;
        }

        if (!touched) return false;

        var after = snapshots.Capture(sheet);

        context.History?.Push(new DelegateAction(
            historyLabel,
            redo: () => snapshots.Restore(engine, sheetIndex, after),
            undo: () => snapshots.Restore(engine, sheetIndex, before)));

        context.RequestRefresh();

        return true;
    }
}

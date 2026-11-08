using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public interface ISheetSortGate
{
    bool Refuses(Workbook workbook, Worksheet sheet, CellRange range);
}

public sealed class SheetSortGate(ISheetEditGuard guard, IStatusBarService status, ITextCatalog texts, Engine.Operations.ISheetSort sorter) : ISheetSortGate
{
    public bool Refuses(Workbook workbook, Worksheet sheet, CellRange range)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(sheet);
        if (sorter.Splits(sheet, range, hasHeader: false))
        {
            status.Report(texts["status.sort.arrays"], NotificationSeverity.Error);
            return true;
        }

        if (!sheet.Protection.IsEnabled) return false;

        if (!sheet.Protection.AllowSort)
        {
            status.Report(texts["status.protection.sort"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
            return true;
        }

        if (guard.CanWrite(workbook, sheet, range)) return false;

        status.Report(texts["status.protection.sortLocked"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
        return true;
    }
}

using PulseOffice.Localization;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI;

public interface ISheetFormatGate
{
    bool Refuses(Worksheet sheet);
}

public sealed class SheetFormatGate(ISheetEditGuard guard, IStatusBarService status, ITextCatalog texts) : ISheetFormatGate
{
    public bool Refuses(Worksheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        if (guard.CanFormat(sheet)) return false;

        status.Report(texts["status.protection.cellFormat"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
        return true;
    }
}

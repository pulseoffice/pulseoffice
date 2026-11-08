using PulseOffice.UI.Controls.Notifications;

namespace PulseOffice.Sheets.UI;

public static class LedgerRemedies
{
    public static CommandNotificationAction UnprotectSheet { get; } =
        new("ledger.worksheet.protect", "notification.action.unprotectSheet");

    public static CommandNotificationAction UnprotectStructure { get; } =
        new("ledger.file.protectStructure", "notification.action.unprotectStructure");
}

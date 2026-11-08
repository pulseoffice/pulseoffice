using PulseOffice.Documents.UI.Editing;
using PulseOffice.UI.Commands;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioNavigationCommands(
    IActiveFolioContext context,
    IFolioPageStatus pages,
    PulseOffice.UI.Controls.Services.IStatusBarService status,
    PulseOffice.Localization.ITextCatalog texts) : FolioCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Viewing("folio.edit.goToPage") with
        {
            DefaultGesture = "Ctrl+G",
            Execute = (_, _) =>
            {
                pages.Update();
                if (pages.Segment.IsVisible) pages.Segment.RequestFocus();
                else status.Report(texts["folio.goToPage.noPages"], PulseOffice.UI.Controls.Notifications.NotificationSeverity.Warning);
                return Task.CompletedTask;
            },
        },
    ];
}

using PulseOffice.Documents.Protection;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Documents.UI.Editing;

public interface IFolioEditRefusals
{
    void Report(EditVerdict verdict);
}

public sealed class FolioEditRefusals(IStatusBarService status, ITextCatalog texts) : IFolioEditRefusals
{
    public void Report(EditVerdict verdict)
    {
        if (verdict == EditVerdict.Allowed) return;
        status.Report(texts["folio.protect.blocked"], NotificationSeverity.Error);
    }
}

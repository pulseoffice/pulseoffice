using PulseOffice.Localization;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Shell;

public interface IFileWriteAccess
{
    bool CanWrite(string path);
}

public sealed class FileWriteAccess : IFileWriteAccess
{
    public bool CanWrite(string path)
    {
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReadOnly) != 0) return false;
            using var probe = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

public sealed class ReadOnlyOnOpen(
    IDocumentWorkspace workspace,
    IDocumentSharing sharing,
    IFileWriteAccess access,
    IStatusBarService status,
    ITextCatalog texts)
{
    private bool _started;

    public void Start()
    {
        if (_started) return;
        _started = true;

        workspace.DocumentOpened += (_, document) => Apply(document);
    }

    public void Apply(OpenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Path is not { Length: > 0 } path || !File.Exists(path)) return;

        var name = Path.GetFileName(path);

        if (sharing.Holder(path) is { IsMine: false } holder)
        {
            document.IsReadOnly = true;
            status.Report(texts.Format("status.open.heldByOther", name, holder.User, holder.Machine), NotificationSeverity.Warning);
            return;
        }

        if (!access.CanWrite(path))
        {
            document.IsReadOnly = true;
            status.Report(texts.Format("status.open.readOnlyFile", name), NotificationSeverity.Warning);
        }
    }
}

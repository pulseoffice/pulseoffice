using Microsoft.Extensions.Logging;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.UI.Controls.Notifications;

namespace PulseOffice.UI.Controls.Services;

public sealed class ReportingFileDialogService(IFileDialogService inner, IStatusBarService status, ITextCatalog texts, ILogger<ReportingFileDialogService> log) : IFileDialogService
{
    public Task<ChosenFile?> OpenFileAsync(string title, IReadOnlyList<FileFilter> filters, CancellationToken ct = default) =>
        Guarded(() => inner.OpenFileAsync(title, filters, ct), null);

    public Task<ChosenFile?> OpenFileInAsync(string title, IReadOnlyList<FileFilter> filters, string startFolder, CancellationToken ct = default) =>
        Guarded(() => inner.OpenFileInAsync(title, filters, startFolder, ct), null);

    public Task<IReadOnlyList<ChosenFile>> OpenFilesAsync(string title, IReadOnlyList<FileFilter> filters, CancellationToken ct = default) =>
        Guarded(() => inner.OpenFilesAsync(title, filters, ct), []);

    public Task<ChosenFile?> SaveFileAsync(string title, string suggestedName, IReadOnlyList<FileFilter> filters, CancellationToken ct = default) =>
        Guarded(() => inner.SaveFileAsync(title, suggestedName, filters, ct), null);

    private async Task<T> Guarded<T>(Func<Task<T>> pick, T nothing)
    {
        try
        {
            return await pick().ConfigureAwait(true);
        }
        catch (Exception fault) when (fault is not OperationCanceledException)
        {
            log.LogError(fault, "The system file dialog failed");
            status.Report(texts["status.fileDialog.failed"], NotificationSeverity.Error);
            return nothing;
        }
    }
}

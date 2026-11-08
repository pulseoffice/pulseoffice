using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class ReportingFileDialogServiceTests
{
    private sealed class FailingDialogs : IFileDialogService
    {
        public Task<ChosenFile?> OpenFileAsync(string title, IReadOnlyList<FileFilter> filters, CancellationToken ct = default) =>
            throw new InvalidOperationException("picker unavailable");

        public Task<ChosenFile?> SaveFileAsync(string title, string suggestedName, IReadOnlyList<FileFilter> filters, CancellationToken ct = default) =>
            Task.FromException<ChosenFile?>(new IOException("portal gone"));
    }

    private sealed class CancelledDialogs : IFileDialogService
    {
        public Task<ChosenFile?> OpenFileAsync(string title, IReadOnlyList<FileFilter> filters, CancellationToken ct = default) =>
            throw new OperationCanceledException();

        public Task<ChosenFile?> SaveFileAsync(string title, string suggestedName, IReadOnlyList<FileFilter> filters, CancellationToken ct = default) =>
            throw new OperationCanceledException();
    }

    private readonly ManualTime _time = new();
    private readonly NotificationHistory _history;
    private readonly StatusBarService _status;
    private readonly TextCatalog _texts = new(UiLanguage.English);

    public ReportingFileDialogServiceTests()
    {
        _history = new NotificationHistory(_time);
        _status = new StatusBarService(_time, new NotificationCenter(_time), _history);
    }

    [Fact]
    public async Task A_failing_system_dialog_reports_an_error_and_returns_no_file()
    {
        var dialogs = new ReportingFileDialogService(new FailingDialogs(), _status, _texts, NullLogger<ReportingFileDialogService>.Instance);

        var opened = await dialogs.OpenFileAsync("Open", [], TestContext.Current.CancellationToken);
        var saved = await dialogs.SaveFileAsync("Save", "a.txt", [], TestContext.Current.CancellationToken);

        Assert.Null(opened);
        Assert.Null(saved);
        Assert.Contains(_history.Entries, entry => entry.Notification.Message == _texts["status.fileDialog.failed"] && entry.Notification.Severity == NotificationSeverity.Error);
    }

    [Fact]
    public async Task A_cancelled_dialog_is_not_reported_as_a_failure()
    {
        var dialogs = new ReportingFileDialogService(new CancelledDialogs(), _status, _texts, NullLogger<ReportingFileDialogService>.Instance);

        await Assert.ThrowsAsync<OperationCanceledException>(() => dialogs.OpenFileAsync("Open", [], TestContext.Current.CancellationToken));
        Assert.Empty(_history.Entries);
    }
}

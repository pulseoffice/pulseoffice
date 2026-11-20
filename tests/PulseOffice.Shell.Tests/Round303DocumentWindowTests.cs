using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class Round303DocumentWindowTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "pulseoffice-round303-" + Guid.NewGuid().ToString("N"));

    private readonly NotificationCenter _notifications = new(TimeProvider.System);
    private readonly NotificationHistory _history = new(TimeProvider.System);

    private static OpenDocument Tab(string title) =>
        new(ModuleKind.Sheets, title, new Avalonia.Controls.Panel(), new UndoStack());

    private sealed class TitleProvider : IDocumentSnapshotProvider
    {
        public ModuleKind Module => ModuleKind.Sheets;

        public IRecoveryCapture Capture(OpenDocument document) =>
            new DelegateCapture(document.Title, async (model, destination, ct) =>
            {
                var writer = new StreamWriter(destination);
                await writer.WriteAsync((string)model!);
                await writer.FlushAsync(ct);
            });

        public Task<OpenDocument?> RestoreAsync(Stream source, string title, CancellationToken ct = default) =>
            Task.FromResult<OpenDocument?>(null);
    }

    private sealed class WindowSource : IDocumentViewSource
    {
        public ModuleKind Module => ModuleKind.Sheets;

        public OpenDocument? CreateSecondView(OpenDocument document, string title) =>
            document.CreateWindow(new Avalonia.Controls.Panel(), title);
    }

    private sealed class NoDialogs : IDialogService
    {
        public bool IsDialogOpen => false;

        public Task<TResult?> ShowAsync<TResult>(Avalonia.Controls.Control content, DialogOptions options, CancellationToken ct = default) =>
            Task.FromResult(default(TResult));

        public Task ShowAsync(Avalonia.Controls.Control content, DialogOptions options, CancellationToken ct = default) => Task.CompletedTask;

        public void Close(object? result)
        {
        }
    }

    [Fact]
    public void A_new_window_opens_next_to_the_last_window_of_its_document()
    {
        var workspace = new DocumentWorkspace();
        var (a, b, c) = (Tab("A"), Tab("B"), Tab("C"));
        foreach (var tab in new[] { a, b, c }) workspace.Open(tab);
        var windows = new DocumentWindows(workspace, [new WindowSource()]);

        var second = windows.OpenAnother(a)!;
        var third = windows.OpenAnother(a)!;

        Assert.Equal([a, second, third, b, c], workspace.Documents);
        Assert.Same(third, workspace.Active);
    }

    private sealed class ReleasableView : Avalonia.Controls.Panel, IDocumentViewRelease
    {
        public bool IsSharedModelReleased { get; private set; }

        public void ReleaseSharedModel() => IsSharedModelReleased = true;
    }

    [Fact]
    public void Closing_a_window_releases_its_view_only_while_another_window_keeps_the_document()
    {
        var workspace = new DocumentWorkspace();
        var firstView = new ReleasableView();
        var first = new OpenDocument(ModuleKind.Sheets, "A", firstView, new UndoStack());
        var secondView = new ReleasableView();
        var second = first.CreateWindow(secondView, "A : 2");
        workspace.Open(first);
        workspace.Open(second);

        workspace.Close(second);
        Assert.True(secondView.IsSharedModelReleased);

        workspace.Close(first);
        Assert.False(firstView.IsSharedModelReleased);
    }

    [Fact]
    public async Task Two_windows_of_one_document_keep_one_recovery_snapshot_until_the_last_closes()
    {
        var recovery = new DocumentRecovery([new TitleProvider()], NullLogger<DocumentRecovery>.Instance, _directory);
        var workspace = new DocumentWorkspace();
        using var coordinator = new RecoveryCoordinator(recovery, workspace, new NoDialogs(),
            new StatusBarService(TimeProvider.System, _notifications, _history), new FakeTextCatalog(),
            NullLogger<RecoveryCoordinator>.Instance);
        var first = Tab("Conti");
        workspace.Open(first);
        var second = new DocumentWindows(workspace, [new WindowSource()]).OpenAnother(first)!;
        first.MarkModified();

        await recovery.SnapshotAsync(first, TestContext.Current.CancellationToken);
        await recovery.SnapshotAsync(second, TestContext.Current.CancellationToken);
        Assert.Single(Directory.GetFiles(_directory, "*.json"));

        workspace.Close(first);
        Assert.Single(Directory.GetFiles(_directory, "*.json"));

        workspace.Close(second);
        Assert.Empty(Directory.GetFiles(_directory, "*.json"));
    }

    public void Dispose()
    {
        _notifications.Dispose();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}

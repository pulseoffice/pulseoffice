using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Shell;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;
using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class Round290RecoveryChoiceTests : IDisposable
{
    private readonly NotificationCenter _notifications = new(TimeProvider.System);
    private readonly NotificationHistory _history = new(TimeProvider.System);

    public void Dispose() => _notifications.Dispose();

    private sealed class Recovery(params string[] unreadable) : IDocumentRecovery
    {
        public List<string> Discarded { get; } = [];

        public List<string> Restored { get; } = [];

        public bool DiscardedAll { get; private set; }

        public IReadOnlyList<RecoverySnapshot> Pending { get; set; } = [];

        public Task SnapshotAsync(OpenDocument document, CancellationToken ct = default) => Task.CompletedTask;

        public Task WhenIdleAsync() => Task.CompletedTask;

        public Task<OpenDocument?> RestoreAsync(RecoverySnapshot snapshot, CancellationToken ct = default)
        {
            if (unreadable.Contains(snapshot.Id)) return Task.FromResult<OpenDocument?>(null);
            Restored.Add(snapshot.Id);
            return Task.FromResult<OpenDocument?>(new OpenDocument(ModuleKind.Sheets, snapshot.Title, new Avalonia.Controls.Border(), new UndoStack()));
        }

        public void Discard(string id) => Discarded.Add(id);

        public void DiscardFor(OpenDocument document)
        {
        }

        public void DiscardAll() => DiscardedAll = true;

        public void DiscardSession()
        {
        }
    }

    private static readonly RecoverySnapshot[] Pending =
    [
        new("a", ModuleKind.Sheets, "Conti", null, DateTimeOffset.UnixEpoch),
        new("b", ModuleKind.Sheets, "Bozza", null, DateTimeOffset.UnixEpoch),
    ];

    private (RecoveryCoordinator, DocumentWorkspace) Coordinator(Recovery recovery)
    {
        var workspace = new DocumentWorkspace();
        var status = new StatusBarService(TimeProvider.System, _notifications, _history);
        var texts = new FakeTextCatalog();
        texts.Add("status.recovery.failed", "{0} / {1}");
        return (new RecoveryCoordinator(recovery, workspace, new NoDialogs(), status, texts, NullLogger<RecoveryCoordinator>.Instance), workspace);
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
    public async Task Only_the_selected_documents_are_restored_and_the_others_discarded()
    {
        var recovery = new Recovery();
        var (coordinator, workspace) = Coordinator(recovery);
        using (coordinator)
            await coordinator.ApplyAsync(new RecoveryPromptView.Selection(new HashSet<string> { "b" }), Pending, TestContext.Current.CancellationToken);

        Assert.Equal(["b"], recovery.Restored);
        Assert.Equal(["a"], recovery.Discarded);
        Assert.Single(workspace.Documents);
    }

    [Fact]
    public async Task A_snapshot_that_cannot_be_read_is_reported_as_a_warning_and_kept()
    {
        var recovery = new Recovery("a");
        var (coordinator, _) = Coordinator(recovery);
        using (coordinator)
            await coordinator.ApplyAsync(new RecoveryPromptView.Selection(new HashSet<string> { "a", "b" }), Pending, TestContext.Current.CancellationToken);

        Assert.Empty(recovery.Discarded);
        Assert.Contains(_history.Entries, entry => entry.Notification.Severity == NotificationSeverity.Warning && entry.Notification.Message.Contains("Conti", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Discard_drops_every_snapshot_and_a_dismissed_dialog_keeps_them()
    {
        var recovery = new Recovery();
        var (coordinator, _) = Coordinator(recovery);
        using (coordinator)
        {
            await coordinator.ApplyAsync(null, Pending, TestContext.Current.CancellationToken);
            Assert.False(recovery.DiscardedAll);
            await coordinator.ApplyAsync(false, Pending, TestContext.Current.CancellationToken);
        }

        Assert.True(recovery.DiscardedAll);
    }
}

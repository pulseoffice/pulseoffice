using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class Round174RecoveryOwnershipTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "pulseoffice-recovery-174-" + Guid.NewGuid().ToString("N"));

    private sealed class TextProvider : IDocumentSnapshotProvider
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

    private sealed class Ownership(int process, ISet<int> running) : IRecoveryOwnership
    {
        public RecoveryOwner Current { get; } = new(process, DateTimeOffset.UnixEpoch);

        public bool IsAlive(RecoveryOwner owner) => running.Contains(owner.ProcessId);
    }

    private static OpenDocument Document(string title) =>
        new(ModuleKind.Sheets, title, new Avalonia.Controls.Panel(), new UndoStack());

    private DocumentRecovery Session(int process, ISet<int> running) =>
        new([new TextProvider()], NullLogger<DocumentRecovery>.Instance, _directory, ownership: new Ownership(process, running));

    [Fact]
    public async Task Leaving_normally_keeps_the_snapshots_of_an_earlier_crash_that_were_not_decided()
    {
        var running = new HashSet<int>();
        await Session(1, running).SnapshotAsync(Document("Crashed"), TestContext.Current.CancellationToken);

        var second = Session(2, running);
        Assert.Single(second.Pending);
        await second.SnapshotAsync(Document("Current"), TestContext.Current.CancellationToken);
        second.DiscardSession();

        var third = Session(3, running);
        Assert.Equal("Crashed", Assert.Single(third.Pending).Title);
    }

    [Fact]
    public async Task Another_running_instance_does_not_see_or_discard_live_snapshots()
    {
        var running = new HashSet<int> { 1 };
        await Session(1, running).SnapshotAsync(Document("Live"), TestContext.Current.CancellationToken);

        var other = Session(2, running);
        Assert.Empty(other.Pending);
        other.DiscardAll();
        Assert.Single(Directory.GetFiles(_directory, "*.json"));

        running.Clear();
        Assert.Equal("Live", Assert.Single(Session(3, running).Pending).Title);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}

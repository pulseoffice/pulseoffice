using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class Round163RecoveryLifetimeTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "pulseoffice-recovery-lifetime-" + Guid.NewGuid().ToString("N"));

    private sealed class GatedProvider : IDocumentSnapshotProvider
    {
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ModuleKind Module => ModuleKind.Sheets;

        public IRecoveryCapture Capture(OpenDocument document) =>
            new DelegateCapture(document.Title, async (model, destination, ct) =>
            {
                await Release.Task.WaitAsync(ct);
                var writer = new StreamWriter(destination);
                await writer.WriteAsync((string)model!);
                await writer.FlushAsync(ct);
            });

        public Task<OpenDocument?> RestoreAsync(Stream source, string title, CancellationToken ct = default) =>
            Task.FromResult<OpenDocument?>(null);
    }

    private static OpenDocument Document(string title) =>
        new(ModuleKind.Sheets, title, new Avalonia.Controls.Panel(), new UndoStack());

    [Fact]
    public async Task A_snapshot_queued_before_the_document_closed_writes_nothing_and_keeps_no_reference()
    {
        var provider = new GatedProvider();
        var recovery = new DocumentRecovery([provider], NullLogger<DocumentRecovery>.Instance, _directory);
        var first = Document("Primo");
        var closed = Document("Chiuso");

        var running = recovery.SnapshotAsync(first, TestContext.Current.CancellationToken);
        var queued = recovery.SnapshotAsync(closed, TestContext.Current.CancellationToken);
        recovery.DiscardFor(closed);
        provider.Release.SetResult();
        await running;
        await queued;

        var manifest = Assert.Single(Directory.GetFiles(_directory, "*.json"));
        Assert.Contains("Primo", await File.ReadAllTextAsync(manifest, TestContext.Current.CancellationToken), StringComparison.Ordinal);
        recovery.DiscardFor(first);
        Assert.Empty(Directory.GetFiles(_directory, "*.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}

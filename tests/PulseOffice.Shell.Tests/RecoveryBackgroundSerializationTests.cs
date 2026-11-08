using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class RecoveryBackgroundSerializationTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "pulseoffice-recovery-background-" + Guid.NewGuid().ToString("N"));

    private sealed class ThreadProbeProvider : IDocumentSnapshotProvider
    {
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<int> Writing { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CaptureThread { get; private set; }

        public ModuleKind Module => ModuleKind.Sheets;

        public IRecoveryCapture Capture(OpenDocument document)
        {
            CaptureThread = Environment.CurrentManagedThreadId;
            var frozen = string.Concat(document.Model as List<string> ?? []);
            return new DelegateCapture(frozen, async (model, destination, ct) =>
            {
                Writing.SetResult(Environment.CurrentManagedThreadId);
                await Release.Task.WaitAsync(ct);
                var writer = new StreamWriter(destination);
                await writer.WriteAsync((string)model!);
                await writer.FlushAsync(ct);
            });
        }

        public async Task<OpenDocument?> RestoreAsync(Stream source, string title, CancellationToken ct = default) =>
            new OpenDocument(ModuleKind.Sheets, title, new Avalonia.Controls.Panel(), new UndoStack())
            {
                Model = await new StreamReader(source).ReadToEndAsync(ct),
            };
    }

    [Fact]
    public async Task The_capture_runs_on_the_caller_and_the_serialization_does_not_hold_it()
    {
        var provider = new ThreadProbeProvider();
        var recovery = new DocumentRecovery([provider], NullLogger<DocumentRecovery>.Instance, _directory);
        var document = new OpenDocument(ModuleKind.Sheets, "Conti", new Avalonia.Controls.Panel(), new UndoStack()) { Model = new List<string> { "prima" } };
        var caller = Environment.CurrentManagedThreadId;

        var snapshot = recovery.SnapshotAsync(document, TestContext.Current.CancellationToken);
        Assert.Equal(caller, provider.CaptureThread);
        await provider.Writing.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.False(snapshot.IsCompleted);
        ((List<string>)document.Model!).Add("dopo");
        provider.Release.SetResult();
        await snapshot;

        Assert.False(snapshot.IsFaulted);
        var reopened = new DocumentRecovery([provider], NullLogger<DocumentRecovery>.Instance, _directory);
        var restored = await reopened.RestoreAsync(Assert.Single(reopened.Pending), TestContext.Current.CancellationToken);
        Assert.Equal("prima", restored!.Model);
    }

    [Fact]
    public async Task The_manifest_is_written_without_indentation()
    {
        var provider = new ThreadProbeProvider();
        provider.Release.SetResult();
        var recovery = new DocumentRecovery([provider], NullLogger<DocumentRecovery>.Instance, _directory);
        var document = new OpenDocument(ModuleKind.Sheets, "Conti", new Avalonia.Controls.Panel(), new UndoStack()) { Model = new List<string> { "x" } };

        await recovery.SnapshotAsync(document, TestContext.Current.CancellationToken);

        var manifest = await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(_directory, "*.json")), TestContext.Current.CancellationToken);
        Assert.DoesNotContain('\n', manifest);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}

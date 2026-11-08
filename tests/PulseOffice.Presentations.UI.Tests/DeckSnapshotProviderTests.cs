using PulseOffice.Formats;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Foundation.Modules;
using PulseOffice.Shell;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class DeckSnapshotProviderTests
{
    private static readonly PresentationFormat NativeFormat = new()
    {
        Id = "odp", DisplayName = "ODP", Extensions = [".odp"], MimeType = "application/x-test", IsNative = true,
    };

    [Fact]
    public async Task A_failed_snapshot_write_is_reported_instead_of_leaving_an_empty_recovery_file()
    {
        var provider = new DeckSnapshotProvider(new Formats(new FailingWriter()), new UnusedFactory());

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.Capture(DeckDocument()).WriteAsync(new MemoryStream(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_missing_native_writer_is_reported()
    {
        var provider = new DeckSnapshotProvider(new Formats(null), new UnusedFactory());

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.Capture(DeckDocument()).WriteAsync(new MemoryStream(), TestContext.Current.CancellationToken));
    }

    private static OpenDocument DeckDocument() =>
        new(ModuleKind.Presentations, "deck", new Avalonia.Controls.Panel(), new PulseOffice.Document.Commands.UndoStack())
        {
            Model = new Presentation(),
        };

    private sealed class FailingWriter : IPresentationWriter
    {
        public PresentationFormat Format => NativeFormat;
        public FidelityReport CheckFidelity(Presentation document) => throw new NotSupportedException();
        public OperationResult<bool> Write(Presentation document, Stream stream) =>
            OperationResult<bool>.Fail(Diagnostic.Error("TEST", "disk full"));
    }

    private sealed class Formats(IPresentationWriter? writer) : IPresentationFormatRegistry
    {
        public IReadOnlyList<PresentationFormat> ReadableFormats => [NativeFormat];
        public IReadOnlyList<PresentationFormat> WritableFormats => [NativeFormat];
        public PresentationFormat Native => NativeFormat;
        public IPresentationReader? ReaderFor(string path) => null;
        public IPresentationWriter? WriterFor(string path) => writer;
        public IPresentationReader? ReaderForContent(Stream stream) => null;
        public OperationResult<Presentation> Read(string path, Stream stream) => throw new NotSupportedException();
    }

    private sealed class UnusedFactory : IDeckDocumentFactory
    {
        public OpenDocument CreateEmpty() => throw new NotSupportedException();
        public OpenDocument CreateFrom(Presentation deck, string title) => throw new NotSupportedException();
    }
}

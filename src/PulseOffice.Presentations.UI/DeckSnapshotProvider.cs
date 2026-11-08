using PulseOffice.Formats;
using PulseOffice.Foundation.Modules;
using PulseOffice.Presentations;
using PulseOffice.Shell;

namespace PulseOffice.Presentations.UI;

public sealed class DeckSnapshotProvider(
    IPresentationFormatRegistry formats, IDeckDocumentFactory documents) : IDocumentSnapshotProvider
{
    public ModuleKind Module => ModuleKind.Presentations;

    public IRecoveryCapture Capture(OpenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Model is not Presentation deck) return new DeckRecoveryCapture(null, null);

        var writer = formats.WriterFor(formats.Native.Extensions[0])
            ?? throw new InvalidOperationException("No native presentation writer is registered for the recovery snapshot.");
        return new DeckRecoveryCapture(writer, deck.Copy());
    }

    public Task<OpenDocument?> RestoreAsync(Stream source, string title, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var reader = formats.ReaderForContent(source);
        if (reader is null) return Task.FromResult<OpenDocument?>(null);

        source.Position = 0;
        var result = reader.Read(source);
        if (!result.Succeeded || result.Value is null) return Task.FromResult<OpenDocument?>(null);

        return Task.FromResult<OpenDocument?>(documents.CreateFrom(result.Value, title));
    }
}

public sealed class DeckRecoveryCapture(IPresentationWriter? writer, Presentation? copy) : IRecoveryCapture
{
    public Task WriteAsync(Stream destination, CancellationToken ct = default)
    {
        if (writer is null || copy is null) return Task.CompletedTask;

        var written = writer.Write(copy, destination);
        if (!written.Succeeded)
            throw new InvalidOperationException(
                $"The recovery snapshot was not written: {written.Diagnostics.FirstOrDefault()?.Message}");

        return Task.CompletedTask;
    }
}

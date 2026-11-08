using PulseOffice.Formats;
using PulseOffice.Foundation.Modules;
using PulseOffice.Sheets;
using PulseOffice.Shell;

namespace PulseOffice.Sheets.UI;

public sealed class SpreadsheetSnapshotProvider(
    ISpreadsheetFormatRegistry formats, ILedgerDocumentFactory documents, IWorkbookCopier copier) : IDocumentSnapshotProvider
{
    public ModuleKind Module => ModuleKind.Sheets;

    public IRecoveryCapture Capture(OpenDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Model is not Workbook workbook) return new LedgerRecoveryCapture(null, null);

        var writer = formats.WriterFor(formats.Native)
            ?? throw new InvalidOperationException("No native spreadsheet writer is registered for the recovery snapshot.");
        return new LedgerRecoveryCapture(writer, copier.Copy(workbook));
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

public sealed class LedgerRecoveryCapture(ISpreadsheetWriter? writer, Workbook? copy) : IRecoveryCapture
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

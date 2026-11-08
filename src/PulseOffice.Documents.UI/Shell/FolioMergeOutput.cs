using System.Globalization;
using PulseOffice.Document.Embedding;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.UI.Rendering;
using PulseOffice.Formats;
using PulseOffice.Formats.Pdf;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Printing;

namespace PulseOffice.Documents.UI.Shell;

public enum MergeTarget
{
    SingleDocument,
    OdtFiles,
    DocxFiles,
    PdfFiles,
    SinglePdf,
}

public interface IMergeDataReader
{
    MergeData From(DataSnapshot snapshot, CultureInfo culture);
}

public sealed class MergeDataReader : IMergeDataReader
{
    public MergeData From(DataSnapshot snapshot, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(culture);
        return new MergeData(
            [.. snapshot.Columns.Select(column => column.Name)],
            [.. snapshot.Rows.Select(row => (IReadOnlyList<string>)[.. row.Select(value => Text(value, culture))])]);
    }

    private string Text(object? value, CultureInfo culture) => value switch
    {
        null or DBNull => string.Empty,
        DateTime date => date.TimeOfDay == TimeSpan.Zero ? date.ToString("d", culture) : date.ToString("g", culture),
        DateTimeOffset moment => moment.LocalDateTime.ToString("g", culture),
        DateOnly day => day.ToString("d", culture),
        IFormattable formattable => formattable.ToString(null, culture),
        _ => value.ToString() ?? string.Empty,
    };
}

public interface IMergeFileNames
{
    string For(MergeData data, int row, string? column, string stem, string extension);
}

public sealed class MergeFileNames : IMergeFileNames
{
    private const int MaximumNameLength = 120;

    private readonly System.Buffers.SearchValues<char> _nonPortable = System.Buffers.SearchValues.Create("/\\:*?\"<>|");

    public string For(MergeData data, int row, string? column, string stem, string extension)
    {
        ArgumentNullException.ThrowIfNull(data);
        var value = column is null ? null : Portable(data.Value(row, column));
        var name = string.IsNullOrEmpty(value)
            ? stem + "-" + (row + 1).ToString("D3", CultureInfo.InvariantCulture)
            : stem + "-" + value;
        var room = Math.Max(1, MaximumNameLength - extension.Length - 1);
        if (name.Length > room) name = name[..room].TrimEnd(' ', '.');
        return name + "." + extension;
    }

    private string? Portable(string? value)
    {
        if (value is null) return null;
        var cleaned = new string([.. value.Select(character => char.IsControl(character) || _nonPortable.Contains(character) ? '_' : character)]);
        return cleaned.Trim().TrimEnd('.').Trim();
    }
}

public interface IFolioMergeOutput
{
    TextDocument Single(TextDocument template, MergeData data, IReadOnlyList<int> rows);

    OperationResult<int> Files(TextDocument template, MergeData data, IReadOnlyList<int> rows, MergeTarget target, string folder, string stem, string? nameColumn);

    OperationResult<int> Pdf(TextDocument template, MergeData data, IReadOnlyList<int> rows, Stream stream, string title);
}

public sealed class FolioMergeOutput(
    IMailMergeEngine engine,
    ITextDocumentFormatRegistry formats,
    IFolioPrintables printables,
    PulseOffice.Documents.Layout.Flow.IPrintLayouts layouts,
    IPdfExporter exporter,
    IMergeFileNames names,
    PulseOffice.Platform.IAtomicFileWriter atomic) : IFolioMergeOutput
{
    public TextDocument Single(TextDocument template, MergeData data, IReadOnlyList<int> rows) => engine.Combine(template, data, rows);

    public OperationResult<int> Files(TextDocument template, MergeData data, IReadOnlyList<int> rows, MergeTarget target, string folder, string stem, string? nameColumn)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var extension = target switch
        {
            MergeTarget.DocxFiles => "docx",
            MergeTarget.PdfFiles => "pdf",
            _ => "odt",
        };
        var written = 0;
        var diagnostics = new List<Diagnostic>();
        foreach (var row in rows)
        {
            var record = engine.Record(template, data, row);
            var path = Unique(Path.Combine(folder, names.For(data, row, nameColumn, stem, extension)));
            var result = atomic.Replace(path,
                stream => target == MergeTarget.PdfFiles ? WritePdf(record, stream, Path.GetFileNameWithoutExtension(path)) : WriteDocument(record, stream, path),
                written => written.Succeeded);
            if (!result.Succeeded) return OperationResult<int>.Fail([.. result.Diagnostics]);
            diagnostics.AddRange(result.Diagnostics);
            written++;
        }
        return OperationResult<int>.Ok(written, diagnostics.DistinctBy(diagnostic => diagnostic.Code));
    }

    public OperationResult<int> Pdf(TextDocument template, MergeData data, IReadOnlyList<int> rows, Stream stream, string title) =>
        WritePdf(engine.Combine(template, data, rows), stream, title);

    private OperationResult<int> WriteDocument(TextDocument record, Stream stream, string path)
    {
        if (formats.WriterFor(path) is not { } writer)
            return OperationResult<int>.Fail(Diagnostic.Error("MERGE001", $"No writer for '{Path.GetExtension(path)}'.").With(Path.GetExtension(path)));
        var result = writer.Write(record, stream);
        return result.Succeeded ? OperationResult<int>.Ok(1, result.Diagnostics) : OperationResult<int>.Fail([.. result.Diagnostics]);
    }

    private OperationResult<int> WritePdf(TextDocument record, Stream stream, string title)
    {
        var layout = layouts.Layout(record);
        if (printables.From(layout, record, false) is not IPrintableDocument printable)
            return OperationResult<int>.Fail(Diagnostic.Error("PDF001", "The document has no pages to write."));
        return exporter.Write(printable, stream, new PdfExportOptions(title, record.Properties.Author ?? string.Empty) { Language = record.Language });
    }

    private string Unique(string path)
    {
        if (!File.Exists(path)) return path;
        var folder = Path.GetDirectoryName(path) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        for (var index = 2; ; index++)
        {
            var candidate = Path.Combine(folder, $"{stem} ({index.ToString(CultureInfo.InvariantCulture)}){extension}");
            if (!File.Exists(candidate)) return candidate;
        }
    }
}

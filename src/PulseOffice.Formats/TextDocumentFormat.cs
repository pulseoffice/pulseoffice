using PulseOffice.Formats.Intake;
using PulseOffice.Documents;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Formats;

public sealed record TextDocumentFormat : IDocumentFormat
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required IReadOnlyList<string> Extensions { get; init; }
    public required string MimeType { get; init; }
    public bool IsNative { get; init; }
    public bool ExportOnly { get; init; }

    public bool Matches(string path) =>
        Extensions.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase));
}

public interface ITextDocumentReader : IDocumentReader<TextDocument, TextDocumentFormat>;

public interface ITextDocumentWriter : IDocumentWriter<TextDocument, TextDocumentFormat>;

public interface ITextDocumentFormatRegistry
{
    IReadOnlyList<TextDocumentFormat> ReadableFormats { get; }
    IReadOnlyList<TextDocumentFormat> WritableFormats { get; }

    IReadOnlyList<TextDocumentFormat> ExportFormats { get; }

    TextDocumentFormat Native { get; }

    ITextDocumentReader? ReaderFor(string path);
    ITextDocumentWriter? WriterFor(string path);

    ITextDocumentReader? ReaderForContent(Stream stream);

    OperationResult<TextDocument> Read(string path, Stream stream);
}

public sealed class TextDocumentFormatRegistry(IEnumerable<ITextDocumentReader> readers, IEnumerable<ITextDocumentWriter> writers, IDocumentIntake intake)
    : DocumentFormatRegistry<TextDocument, TextDocumentFormat, ITextDocumentReader, ITextDocumentWriter>(readers, writers, intake), ITextDocumentFormatRegistry
{
    protected override string Family => "text document";

    protected override string UnknownFormatCode => "FMT003";

    public override IReadOnlyList<TextDocumentFormat> WritableFormats => [.. Writers.Select(w => w.Format).Where(format => !format.ExportOnly)];

    public IReadOnlyList<TextDocumentFormat> ExportFormats => [.. Writers.Select(w => w.Format).Where(format => format.ExportOnly)];
}

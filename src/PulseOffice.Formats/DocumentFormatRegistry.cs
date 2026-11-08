using PulseOffice.Formats.Intake;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Formats;

public interface IDocumentFormat
{
    string Id { get; }

    IReadOnlyList<string> Extensions { get; }

    bool IsNative { get; }

    bool Matches(string path);
}

public interface IDocumentReader<TDocument, out TFormat> where TFormat : IDocumentFormat
{
    TFormat Format { get; }

    bool CanRead(Stream stream);

    OperationResult<TDocument> Read(Stream stream);
}

public interface IDocumentWriter<in TDocument, out TFormat> where TFormat : IDocumentFormat
{
    TFormat Format { get; }

    FidelityReport CheckFidelity(TDocument document);

    OperationResult<bool> Write(TDocument document, Stream stream);
}

public abstract class DocumentFormatRegistry<TDocument, TFormat, TReader, TWriter>
    where TFormat : IDocumentFormat
    where TReader : class, IDocumentReader<TDocument, TFormat>
    where TWriter : class, IDocumentWriter<TDocument, TFormat>
{
    private readonly IDocumentIntake _intake;
    private readonly IReadOnlyList<TReader> _readers;

    protected DocumentFormatRegistry(IEnumerable<TReader> readers, IEnumerable<TWriter> writers, IDocumentIntake intake)
    {
        ArgumentNullException.ThrowIfNull(readers);
        ArgumentNullException.ThrowIfNull(writers);
        ArgumentNullException.ThrowIfNull(intake);

        _intake = intake;
        _readers = [.. readers];
        Writers = [.. writers];

        Native = Writers.FirstOrDefault(w => w.Format.IsNative) is { } native ? native.Format
                 : Writers.FirstOrDefault() is { } first ? first.Format
                 : throw new InvalidOperationException($"No {Family} writer is registered.");
    }

    protected abstract string Family { get; }

    protected abstract string UnknownFormatCode { get; }

    protected IReadOnlyList<TWriter> Writers { get; }

    public IReadOnlyList<TFormat> ReadableFormats => [.. _readers.Select(r => r.Format)];

    public virtual IReadOnlyList<TFormat> WritableFormats => [.. Writers.Select(w => w.Format)];

    public TFormat Native { get; }

    public TReader? ReaderFor(string path) => _readers.FirstOrDefault(r => r.Format.Matches(path));

    public TWriter? WriterFor(string path) => Writers.FirstOrDefault(w => w.Format.Matches(path));

    public TReader? ReaderForContent(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        foreach (var reader in _readers)
        {
            var position = stream.Position;
            try
            {
                if (_intake.Accepts(stream, reader.CanRead)) return reader;
            }
            finally
            {
                stream.Position = position;
            }
        }

        return null;
    }

    public OperationResult<TDocument> Read(string path, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return _intake.Read(stream, inspected => ReadInspected(path, inspected));
    }

    private OperationResult<TDocument> ReadInspected(string path, Stream stream)
    {
        var reader = (stream.CanSeek ? ReaderForContent(stream) : null) ?? ReaderFor(path);
        if (reader is null)
            return OperationResult<TDocument>.Fail(Diagnostic.Error(UnknownFormatCode, $"No recognised {Family} format for '{Path.GetFileName(path)}'.").With(Path.GetFileName(path)));

        return reader.Read(stream);
    }
}

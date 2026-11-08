using PulseOffice.Documents;
using PulseOffice.Formats.Intake;

namespace PulseOffice.Formats;

public interface ITextDocumentInterchange
{
    TextDocument? Read(string formatId, ReadOnlyMemory<byte> data);

    byte[]? Write(string formatId, TextDocument document);
}

public sealed class TextDocumentInterchange(IEnumerable<ITextDocumentReader> readers, IEnumerable<ITextDocumentWriter> writers, IDocumentIntake intake)
    : ITextDocumentInterchange
{
    private readonly IReadOnlyList<ITextDocumentReader> _readers = [.. readers];
    private readonly IReadOnlyList<ITextDocumentWriter> _writers = [.. writers];

    public TextDocument? Read(string formatId, ReadOnlyMemory<byte> data)
    {
        if (data.IsEmpty || _readers.FirstOrDefault(reader => reader.Format.Id == formatId) is not { } reader) return null;
        using var stream = new MemoryStream(data.ToArray(), writable: false);
        var result = intake.Read(stream, reader.Read);
        return result.Succeeded ? result.Value : null;
    }

    public byte[]? Write(string formatId, TextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (_writers.FirstOrDefault(writer => writer.Format.Id == formatId) is not { } writer) return null;
        using var stream = new MemoryStream();
        return writer.Write(document, stream).Succeeded ? stream.ToArray() : null;
    }
}

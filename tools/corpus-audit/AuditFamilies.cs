using PulseOffice.Formats;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Tools.CorpusAudit;

public sealed record DeclaredLoss(string Code, string Message);

public sealed record WrittenCopy(string Target, string Status, byte[]? Package, IReadOnlyList<DeclaredLoss> DeclaredCodes);

public sealed record SourceReading(string Status, IReadOnlyList<DeclaredLoss> DeclaredCodes, IReadOnlyList<WrittenCopy> Copies);

public interface IAuditFamily
{
    IReadOnlyList<string> Extensions { get; }

    SourceReading RoundTrip(string extension, byte[] package);
}

public sealed class AuditFamily<TDocument, TFormat>(
    IReadOnlyDictionary<string, IDocumentReader<TDocument, TFormat>> readers,
    IReadOnlyDictionary<string, IDocumentWriter<TDocument, TFormat>> writers) : IAuditFamily
    where TDocument : class
    where TFormat : IDocumentFormat
{
    public IReadOnlyList<string> Extensions { get; } = [.. readers.Keys];

    public SourceReading RoundTrip(string extension, byte[] package)
    {
        OperationResult<TDocument> read;
        try
        {
            read = readers[extension].Read(new MemoryStream(package));
        }
        catch (Exception exception)
        {
            return new SourceReading("READCRASH:" + exception.GetType().Name, [], []);
        }

        if (!read.Succeeded || read.Value is null) return new SourceReading("READFAIL", Codes(read.Diagnostics), []);
        var copies = writers.Keys.Select(target => Write(read.Value, target)).ToList();
        return new SourceReading("OK", Codes(read.Diagnostics), copies);
    }

    private WrittenCopy Write(TDocument document, string target)
    {
        var writer = writers[target];
        try
        {
            var declared = new List<DeclaredLoss>(Codes(writer.CheckFidelity(document).Losses));
            using var stream = new MemoryStream();
            var written = writer.Write(document, stream);
            declared.AddRange(Codes(written.Diagnostics));
            if (!written.Succeeded) return new WrittenCopy(target, "WRITEFAIL", null, declared);
            var bytes = stream.ToArray();
            var reread = readers[target].Read(new MemoryStream(bytes));
            declared.AddRange(Codes(reread.Diagnostics));
            return new WrittenCopy(target, reread.Succeeded ? "OK" : "REREADFAIL", bytes, declared);
        }
        catch (Exception exception)
        {
            return new WrittenCopy(target, "CRASH:" + exception.GetType().Name, null, []);
        }
    }

    private static List<DeclaredLoss> Codes(IEnumerable<Diagnostic> diagnostics) =>
        [.. diagnostics.Select(diagnostic => new DeclaredLoss(diagnostic.Code, diagnostic.Message)).Distinct()];
}

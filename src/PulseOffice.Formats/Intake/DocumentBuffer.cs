using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Formats.Intake;

public interface IDocumentBuffer
{
    Task<OperationResult<MemoryStream>> ReadAsync(Stream source, CancellationToken ct = default);
}

public sealed class DocumentBuffer(IncomingDocumentLimits limits) : IDocumentBuffer
{
    public async Task<OperationResult<MemoryStream>> ReadAsync(Stream source, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.CanSeek && source.Length - source.Position > limits.MaximumFileBytes) return TooLarge();

        var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(chunk, ct).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > limits.MaximumFileBytes)
            {
                await buffer.DisposeAsync().ConfigureAwait(false);
                return TooLarge();
            }

            buffer.Write(chunk, 0, read);
        }

        buffer.Position = 0;
        return OperationResult<MemoryStream>.Ok(buffer);
    }

    private OperationResult<MemoryStream> TooLarge() =>
        OperationResult<MemoryStream>.Fail(Diagnostic.Error("FMT015", $"The file is larger than {limits.MaximumFileBytes >> 20} MB, the most a document can be.").With(limits.MaximumFileBytes >> 20));
}

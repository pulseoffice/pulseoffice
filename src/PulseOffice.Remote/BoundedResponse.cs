namespace PulseOffice.Remote;

public sealed class BoundedResponse(long maximumBytes = BoundedResponse.DefaultMaximum)
{
    public const long DefaultMaximum = 512L * 1024 * 1024;

    public const long TextMaximum = 16L * 1024 * 1024;

    public long MaximumBytes { get; } = maximumBytes;

    public async Task<byte[]> BytesAsync(HttpContent content, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.Headers.ContentLength is { } declared && declared > MaximumBytes) throw TooLarge();

        await using var stream = await content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;

        while ((read = await stream.ReadAsync(chunk, ct).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > MaximumBytes) throw TooLarge();
            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    public async Task<string> TextAsync(HttpContent content, CancellationToken ct)
    {
        var bytes = await BytesAsync(content, ct).ConfigureAwait(false);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    private HttpRequestException TooLarge() =>
        new($"The response is larger than {MaximumBytes} bytes.");
}

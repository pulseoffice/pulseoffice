using System.Text;
using PulseOffice.Foundation.Network;
using PulseOffice.Formats.Interchange;

namespace PulseOffice.Sheets.UI.ExternalData;

public interface IExternalDataFetcher
{
    Task<string> FetchAsync(string location, string? baseDirectory, CancellationToken ct);

    string Resolve(string location, string? baseDirectory);
}

public sealed class ExternalDataFetcher(IOutboundHttpClients clients, INetworkSharePaths shares) : IExternalDataFetcher, IDisposable
{
    public const int MaximumBytes = 16 * 1024 * 1024;

    private readonly HttpClient _http = clients.Create(OutboundAudience.UserChosen, TimeSpan.FromSeconds(30));

    public void Dispose() => _http.Dispose();

    public string Resolve(string location, string? baseDirectory)
    {
        var trimmed = location.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute) && (absolute.IsFile || absolute.Scheme is "http" or "https"))
            return trimmed;
        if (Path.IsPathRooted(trimmed) || baseDirectory is not { Length: > 0 }) return trimmed;

        var relative = Uri.UnescapeDataString(trimmed).Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(baseDirectory, relative));
    }

    public async Task<string> FetchAsync(string location, string? baseDirectory, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(location);
        if (shares.IsNetworkShare(location)) throw new UnauthorizedAccessException("External sources on network shares are not opened.");
        var trimmed = Resolve(location, baseDirectory);
        if (shares.IsNetworkShare(trimmed)) throw new UnauthorizedAccessException("External sources on network shares are not opened.");

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaximumBytes)
                throw new InvalidOperationException("The external source is too large.");
            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            return await ReadAsync(stream, ct).ConfigureAwait(false);
        }

        var path = uri is { IsFile: true } ? uri.LocalPath : trimmed;
        if (!File.Exists(path)) throw new FileNotFoundException("The external source does not exist.", path);
        if (new FileInfo(path).Length > MaximumBytes) throw new InvalidOperationException("The external source is too large.");

        await using var file = File.OpenRead(path);
        return await ReadAsync(file, ct).ConfigureAwait(false);
    }

    private async Task<string> ReadAsync(Stream stream, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > MaximumBytes) throw new InvalidOperationException("The external source is too large.");
            buffer.Write(chunk, 0, read);
        }

        buffer.Position = 0;
        using var reader = new StreamReader(buffer, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(ct).ConfigureAwait(false);
    }
}

public sealed record ExternalDocument(string Text, ITabularSource Source)
{
    public IReadOnlyList<string> Tables => Source.Tables(Text);

    public TabularData Read(string table) => Source.Read(Text, table);
}

public interface IExternalDataService
{
    Task<ExternalDocument> OpenAsync(string location, ExternalDataFormat format, string? baseDirectory, CancellationToken ct);

    bool IsFailure(Exception exception);
}

public sealed class ExternalDataService(IExternalDataFetcher fetcher, ITabularSources sources) : IExternalDataService
{
    public async Task<ExternalDocument> OpenAsync(string location, ExternalDataFormat format, string? baseDirectory, CancellationToken ct)
    {
        var text = await fetcher.FetchAsync(location, baseDirectory, ct).ConfigureAwait(true);
        return new ExternalDocument(text, sources.For(format));
    }

    public bool IsFailure(Exception exception) => exception is HttpRequestException or IOException
        or UnauthorizedAccessException or TaskCanceledException or InvalidOperationException
        or ArgumentException or UriFormatException or InvalidDataException or TimeoutException or ObjectDisposedException;
}

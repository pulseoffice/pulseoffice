using System.Net.Http.Headers;
using System.Text.Json;
using PulseOffice.Remote;

namespace PulseOffice.Text.Spelling.Hunspell.Catalog;

public interface IDictionaryHttp
{
    Task<string?> TryTextAsync(Uri uri, CancellationToken ct);

    Task<JsonDocument> JsonAsync(Uri uri, CancellationToken ct);

    Task<byte[]> BytesAsync(Uri uri, CancellationToken ct);

    Task<string?> TryVersionAsync(Uri uri, CancellationToken ct);
}

public sealed class DictionaryHttp(HttpClient http) : IDictionaryHttp
{
    public const long FileMaximum = 96L * 1024 * 1024;

    private readonly BoundedResponse _text = new(BoundedResponse.TextMaximum);
    private readonly BoundedResponse _files = new(FileMaximum);

    public async Task<string?> TryTextAsync(Uri uri, CancellationToken ct)
    {
        using var response = await SendAsync(uri, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;
        return await _text.TextAsync(response.Content, ct).ConfigureAwait(false);
    }

    public async Task<JsonDocument> JsonAsync(Uri uri, CancellationToken ct)
    {
        var text = await TryTextAsync(uri, ct).ConfigureAwait(false)
            ?? throw new HttpRequestException($"The dictionary service answered with an error for {uri}.");
        return JsonDocument.Parse(text);
    }

    public async Task<byte[]> BytesAsync(Uri uri, CancellationToken ct)
    {
        using var response = await SendAsync(uri, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await _files.BytesAsync(response.Content, ct).ConfigureAwait(false);
    }

    public async Task<string?> TryVersionAsync(Uri uri, CancellationToken ct)
    {
        using var response = await SendAsync(uri, ct, HttpMethod.Head).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;
        return response.Headers.ETag?.Tag
            ?? response.Content.Headers.LastModified?.ToUniversalTime().ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
    }

    private async Task<HttpResponseMessage> SendAsync(Uri uri, CancellationToken ct, HttpMethod? method = null)
    {
        if (uri.Scheme != Uri.UriSchemeHttps) throw new InvalidDataException("Dictionary downloads must use HTTPS.");

        using var request = new HttpRequestMessage(method ?? HttpMethod.Get, uri);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("PulseOffice", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
    }
}

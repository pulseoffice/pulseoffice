using System.Text.Json;
using PulseOffice.Document.Fonts;
using PulseOffice.Typography.Catalog;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Typography.Sources;

public abstract class OnlineFontSource(HttpClient http, IClassicFaceSelector faces, OpenType.IOpenTypeReader reader) : IFontSource
{
    private const long MaximumFileSize = 32L * 1024 * 1024;
    private const long MaximumTextSize = 64L * 1024 * 1024;

    protected HttpClient Http => http;

    public abstract string Id { get; }

    public FontOrigin Origin => FontOrigin.Online;

    protected abstract bool Enabled { get; }

    public async Task<IReadOnlyList<FontFamilyInfo>> ListAsync(CancellationToken ct = default)
    {
        if (!Enabled) return [];
        using var document = await GetJsonAsync(ListingUri, ct).ConfigureAwait(false);
        return ParseListing(document.RootElement);
    }

    public async Task<OperationResult<IReadOnlyList<EmbeddedFontFile>>> FetchAsync(
        FontFamilyInfo family, IProgress<FontProgress>? progress = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(family);
        if (!Enabled)
            return OperationResult<IReadOnlyList<EmbeddedFontFile>>.Fail(Diagnostic.Error("FONT020", $"The font service '{Id}' is disabled.").With(Id));

        try
        {
            var available = await ResolveFacesAsync(family, ct).ConfigureAwait(false);
            var selected = faces.Select(available);
            if (selected.Count == 0)
                return OperationResult<IReadOnlyList<EmbeddedFontFile>>.Fail(
                    Diagnostic.Error("FONT021", $"'{family.Family}' has no downloadable TrueType file.").With(family.Family));

            var files = new List<EmbeddedFontFile>();
            long total = 0;
            progress?.Report(new FontProgress(0, selected.Count, 0));
            foreach (var face in selected)
            {
                var data = await DownloadAsync(new Uri(face.Location), ct).ConfigureAwait(false);
                total += data.Length;
                files.Add(new EmbeddedFontFile(family.Family, face.Weight, face.Italic, FileName(family.Family, face), data));
                progress?.Report(new FontProgress(files.Count, selected.Count, total));
            }

            return OperationResult<IReadOnlyList<EmbeddedFontFile>>.Ok(files);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidDataException or UriFormatException
            || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            return OperationResult<IReadOnlyList<EmbeddedFontFile>>.Fail(Diagnostic.Error("FONT022", ex.Message).With(ex.Message));
        }
    }

    protected abstract Uri ListingUri { get; }

    protected abstract IReadOnlyList<FontFamilyInfo> ParseListing(JsonElement root);

    protected abstract Task<IReadOnlyList<FontFaceRef>> ResolveFacesAsync(FontFamilyInfo family, CancellationToken ct);

    protected async Task<JsonDocument> GetJsonAsync(Uri uri, CancellationToken ct)
    {
        var text = await GetTextAsync(uri, ct).ConfigureAwait(false);
        var start = text.IndexOfAny(['{', '[']);
        return JsonDocument.Parse(start > 0 ? text[start..] : text);
    }

    protected async Task<string> GetTextAsync(Uri uri, CancellationToken ct) =>
        await TryGetTextAsync(uri, ct).ConfigureAwait(false) ?? throw new HttpRequestException($"The font service answered with an error for {uri}.");

    protected async Task<string?> TryGetTextAsync(Uri uri, CancellationToken ct)
    {
        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;
        if (response.Content.Headers.ContentLength > MaximumTextSize) throw new InvalidDataException("The font listing is too large.");
        return System.Text.Encoding.UTF8.GetString(await ReadLimitedAsync(response, MaximumTextSize, ct).ConfigureAwait(false));
    }

    protected async Task<bool> ExistsAsync(Uri uri, CancellationToken ct)
    {
        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    private async Task<byte[]> DownloadAsync(Uri uri, CancellationToken ct)
    {
        if (uri.Scheme != Uri.UriSchemeHttps) throw new InvalidDataException("Font downloads must use HTTPS.");

        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MaximumFileSize) throw new InvalidDataException("The font file is too large.");

        var data = await ReadLimitedAsync(response, MaximumFileSize, ct).ConfigureAwait(false);
        if (data.Length < 12) throw new InvalidDataException("The downloaded font file is empty.");
        if (!reader.LooksLikeFont(data)) throw new InvalidDataException("The downloaded file is not a TrueType or OpenType font.");
        return data;
    }

    private async Task<byte[]> ReadLimitedAsync(HttpResponseMessage response, long limit, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct).ConfigureAwait(false)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > limit) throw new InvalidDataException("The downloaded data is too large.");
        }

        return buffer.ToArray();
    }

    private string FileName(string family, FontFaceRef face) =>
        $"{new string([.. family.Where(char.IsLetterOrDigit)])}-{face.Weight}{(face.Italic ? "i" : string.Empty)}.ttf";

    protected string? String(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    protected IEnumerable<JsonElement> Array(JsonElement element, string? name = null)
    {
        var target = element;
        if (name is not null && (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out target))) yield break;
        if (target.ValueKind != JsonValueKind.Array) yield break;
        foreach (var item in target.EnumerateArray()) yield return item;
    }
}

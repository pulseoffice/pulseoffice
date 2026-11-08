using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Foundation.Network;
using PulseOffice.Settings;

namespace PulseOffice.Sheets.UI;

public sealed class WebResourceAccess : IWebResourceAccess, IDisposable
{
    public const string SettingKey = "security.webAccess";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private const int MaximumLength = 2 * 1024 * 1024;

    private readonly ISettingsService _settings;
    private readonly HttpClient _client;

    public WebResourceAccess(ISettingsService settings, IOutboundHttpClients clients)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(clients);

        _settings = settings;
        _client = clients.Create(OutboundAudience.DocumentContent, Timeout);
    }

    public bool IsAllowed => _settings.Get<bool>(SettingKey);

    public string? Fetch(string url)
    {
        if (!IsAllowed) return null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = _client.Send(request, HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode) return null;

            using var stream = response.Content.ReadAsStream();
            using var reader = new StreamReader(stream, EncodingOf(response.Content.Headers.ContentType?.CharSet), detectEncodingFromByteOrderMarks: true);
            var buffer = new char[MaximumLength];
            var length = 0;
            int read;
            while (length < buffer.Length && (read = reader.Read(buffer, length, buffer.Length - length)) > 0) length += read;

            return new string(buffer, 0, length);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private System.Text.Encoding EncodingOf(string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset)) return System.Text.Encoding.UTF8;
        try
        {
            return System.Text.Encoding.GetEncoding(charset.Trim('"', ' '));
        }
        catch (ArgumentException)
        {
            return System.Text.Encoding.UTF8;
        }
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}

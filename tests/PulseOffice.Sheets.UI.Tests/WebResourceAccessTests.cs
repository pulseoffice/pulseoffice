using System.Net;
using PulseOffice.Settings;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class WebResourceAccessTests
{
    [Fact]
    public void Una_risposta_enorme_si_tronca_senza_leggerla_tutta()
    {
        var body = new EndlessStream();
        using var access = new WebResourceAccess(new AllowingSettings(), new HandlerClients(new FixedHandler(() => new StreamContent(body))));

        var text = access.Fetch("https://example.test/big");

        Assert.Equal(2 * 1024 * 1024, text!.Length);
        Assert.True(body.Served < 64L * 1024 * 1024);
    }

    [Fact]
    public void Un_charset_sconosciuto_ripiega_su_utf8_invece_di_lanciare()
    {
        using var access = new WebResourceAccess(new AllowingSettings(), new HandlerClients(new FixedHandler(() =>
        {
            var content = new ByteArrayContent("città"u8.ToArray());
            content.Headers.TryAddWithoutValidation("Content-Type", "text/plain; charset=nonexistent-charset");
            return content;
        })));

        Assert.Equal("città", access.Fetch("https://example.test/odd"));
    }

    private sealed class FixedHandler(Func<HttpContent> content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Send(request, cancellationToken));

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) =>
            new(HttpStatusCode.OK) { Content = content() };
    }

    private sealed class EndlessStream : Stream
    {
        public long Served { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => Served; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Array.Fill(buffer, (byte)'a', offset, count);
            Served += count;
            return count;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class AllowingSettings : ISettingsService
    {
        public IReadOnlyCollection<SettingDefinition> Definitions => [];
        public SettingDefinition? Find(string key) => null;
        public T Get<T>(string key) => (T)(object)true;
        public SettingScope EffectiveScope(string key) => SettingScope.User;
        public bool IsLocked(string key) => false;
        public void Set(string key, object? value, SettingScope scope = SettingScope.User) { }
        public void Reset(string key, SettingScope scope = SettingScope.User) { }
        public IEnumerable<SettingDefinition> Search(string query) => [];
        public event EventHandler<SettingChangedEventArgs>? Changed { add { } remove { } }
    }
}

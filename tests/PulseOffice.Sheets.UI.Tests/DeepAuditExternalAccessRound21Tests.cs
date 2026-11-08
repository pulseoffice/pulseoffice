using System.Net;
using System.Net.Sockets;
using System.Text;
using PulseOffice.Settings;
using PulseOffice.Sheets.UI.ExternalData;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class DeepAuditExternalAccessRound21Tests
{
    [Fact]
    public async Task Webservice_does_not_reach_a_server_on_loopback()
    {
        using var server = new LocalHttpServer("secret");
        using var access = new WebResourceAccess(new AllowingSettings(), new PulseOffice.Foundation.Network.OutboundHttpClients(new PulseOffice.Foundation.Network.AddressClassifier()));

        var text = await Task.Run(() => access.Fetch($"http://127.0.0.1:{server.Port}/admin"), TestContext.Current.CancellationToken);

        Assert.Null(text);
        Assert.Equal(0, server.Served);
    }

    [Fact]
    public async Task Webservice_does_not_reach_loopback_through_a_host_name()
    {
        using var server = new LocalHttpServer("secret");
        using var access = new WebResourceAccess(new AllowingSettings(), new PulseOffice.Foundation.Network.OutboundHttpClients(new PulseOffice.Foundation.Network.AddressClassifier()));

        var text = await Task.Run(() => access.Fetch($"http://localhost:{server.Port}/admin"), TestContext.Current.CancellationToken);

        Assert.Null(text);
        Assert.Equal(0, server.Served);
    }

    [Fact]
    public async Task External_data_refuses_a_network_share()
    {
        using var fetcher = new ExternalDataFetcher(new PulseOffice.Foundation.Network.OutboundHttpClients(new PulseOffice.Foundation.Network.AddressClassifier()), new PulseOffice.Foundation.Network.NetworkSharePaths());

        Assert.IsType<UnauthorizedAccessException>(await Record.ExceptionAsync(() => fetcher.FetchAsync(@"\\attacker.example\share\data.csv", null, TestContext.Current.CancellationToken)));
        Assert.IsType<UnauthorizedAccessException>(await Record.ExceptionAsync(() => fetcher.FetchAsync("file://attacker.example/share/data.csv", null, TestContext.Current.CancellationToken)));
        Assert.IsType<UnauthorizedAccessException>(await Record.ExceptionAsync(() => fetcher.FetchAsync("//attacker.example/share/data.csv", null, TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task External_data_does_not_reach_the_cloud_metadata_address()
    {
        using var fetcher = new ExternalDataFetcher(new PulseOffice.Foundation.Network.OutboundHttpClients(new PulseOffice.Foundation.Network.AddressClassifier()), new PulseOffice.Foundation.Network.NetworkSharePaths());

        var refused = await Record.ExceptionAsync(() => fetcher.FetchAsync("http://169.254.169.254/latest/meta-data/", null, TestContext.Current.CancellationToken));

        Assert.IsType<HttpRequestException>(refused);
        Assert.IsType<PulseOffice.Foundation.Network.DestinationRefusedException>(refused.InnerException);
    }

    [Fact]
    public async Task External_data_still_reaches_a_server_on_loopback_chosen_by_the_user()
    {
        using var server = new LocalHttpServer("a;b");
        using var fetcher = new ExternalDataFetcher(new PulseOffice.Foundation.Network.OutboundHttpClients(new PulseOffice.Foundation.Network.AddressClassifier()), new PulseOffice.Foundation.Network.NetworkSharePaths());

        Assert.Equal("a;b", await fetcher.FetchAsync($"http://127.0.0.1:{server.Port}/data.csv", null, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("8.8.8.8", PulseOffice.Foundation.Network.AddressScope.Public)]
    [InlineData("2606:4700::1111", PulseOffice.Foundation.Network.AddressScope.Public)]
    [InlineData("127.0.0.2", PulseOffice.Foundation.Network.AddressScope.Loopback)]
    [InlineData("::1", PulseOffice.Foundation.Network.AddressScope.Loopback)]
    [InlineData("::ffff:127.0.0.1", PulseOffice.Foundation.Network.AddressScope.Loopback)]
    [InlineData("0.0.0.0", PulseOffice.Foundation.Network.AddressScope.Unspecified)]
    [InlineData("10.1.2.3", PulseOffice.Foundation.Network.AddressScope.Private)]
    [InlineData("172.31.0.1", PulseOffice.Foundation.Network.AddressScope.Private)]
    [InlineData("192.168.1.1", PulseOffice.Foundation.Network.AddressScope.Private)]
    [InlineData("100.64.0.1", PulseOffice.Foundation.Network.AddressScope.Private)]
    [InlineData("fd00::1", PulseOffice.Foundation.Network.AddressScope.Private)]
    [InlineData("169.254.169.254", PulseOffice.Foundation.Network.AddressScope.LinkLocal)]
    [InlineData("fe80::1", PulseOffice.Foundation.Network.AddressScope.LinkLocal)]
    [InlineData("::ffff:169.254.169.254", PulseOffice.Foundation.Network.AddressScope.LinkLocal)]
    [InlineData("64:ff9b::a9fe:a9fe", PulseOffice.Foundation.Network.AddressScope.LinkLocal)]
    [InlineData("2002:7f00:1::", PulseOffice.Foundation.Network.AddressScope.Loopback)]
    [InlineData("224.0.0.1", PulseOffice.Foundation.Network.AddressScope.Multicast)]
    [InlineData("255.255.255.255", PulseOffice.Foundation.Network.AddressScope.Reserved)]
    public void Addresses_are_classified_by_scope(string text, PulseOffice.Foundation.Network.AddressScope expected)
    {
        Assert.Equal(expected, new PulseOffice.Foundation.Network.AddressClassifier().Classify(IPAddress.Parse(text)));
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

    private sealed class LocalHttpServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private int _served;

        public LocalHttpServer(string body)
        {
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _ = Task.Run(() => ServeAsync(body));
        }

        public int Port { get; }

        public int Served => Volatile.Read(ref _served);

        private async Task ServeAsync(string body)
        {
            try
            {
                while (true)
                {
                    using var client = await _listener.AcceptTcpClientAsync();
                    Interlocked.Increment(ref _served);
                    var stream = client.GetStream();
                    var buffer = new byte[4096];
                    _ = await stream.ReadAsync(buffer);
                    var reply = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}");
                    await stream.WriteAsync(reply);
                }
            }
            catch (Exception exception) when (exception is SocketException or ObjectDisposedException or IOException)
            {
                return;
            }
        }

        public void Dispose() => _listener.Stop();
    }
}

using System.Text;
using PulseOffice.Platform;
using PulseOffice.Remote;
using Xunit;

namespace PulseOffice.Remote.Tests;

public class WebDavLiveTests : IDisposable
{
    private readonly WebDavTestServer _server = new(Port());
    private readonly HttpClient _client = new();
    private readonly SessionSecretVault _vault = new();
    private readonly WebDavStorage _storage;
    private readonly RemoteConnection _connection;

    private static int _next = 18500;

    private static int Port() => Interlocked.Increment(ref _next);

    public WebDavLiveTests()
    {
        _storage = new WebDavStorage(_client, new FixedVault(_vault), new WebDavResponseReader());

        _connection = new RemoteConnection
        {
            Name = "prova",
            Url = _server.Address,
            User = "ada",
        };

        _vault.WriteAsync("PulseOffice", _connection.SecretKey, "segreta").GetAwaiter().GetResult();
    }

    [Fact]
    public async Task UnFileCaricatoSiRilegge()
    {
        var content = Encoding.UTF8.GetBytes("bilancio");

        var written = await _storage.WriteAsync(
            _connection, "/conti/bilancio.ods", content, null, TestContext.Current.CancellationToken);

        Assert.True(written.Succeeded, Message(written.Diagnostics));

        var read = await _storage.ReadAsync(
            _connection, "/conti/bilancio.ods", TestContext.Current.CancellationToken);

        Assert.True(read.Succeeded, Message(read.Diagnostics));
        Assert.Equal("bilancio", Encoding.UTF8.GetString(read.Value!.Bytes));
    }

    [Fact]
    public async Task LeCredenzialiViaggianoDalPortachiaviENonDallaConnessione()
    {
        await _storage.ListAsync(_connection, "/", TestContext.Current.CancellationToken);

        var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("ada:segreta"));

        Assert.Equal(expected, _server.Authorization);
    }

    [Fact]
    public async Task IlContenutoDiUnaCartellaSiElenca()
    {
        _server.Put("/conti/bilancio.ods", [1, 2, 3]);

        var listing = await _storage.ListAsync(_connection, "/conti", TestContext.Current.CancellationToken);

        Assert.True(listing.Succeeded, Message(listing.Diagnostics));

        var file = Assert.Single(listing.Value!, entry => !entry.IsFolder);

        Assert.Equal("bilancio.ods", file.Name);
        Assert.Equal(3, file.Size);
    }

    [Fact]
    public async Task UnFileMancanteDiceCheNonEsiste()
    {
        var read = await _storage.ReadAsync(
            _connection, "/assente.ods", TestContext.Current.CancellationToken);

        Assert.False(read.Succeeded);
        Assert.Equal("REM012", read.Diagnostics[0].Code);
    }

    [Fact]
    public async Task PresaInConsegnaEScritturaConIlProprioGettone()
    {
        var ct = TestContext.Current.CancellationToken;

        var locked = await _storage.LockAsync(_connection, "/conti/bilancio.ods", ct);

        Assert.True(locked.Succeeded, Message(locked.Diagnostics));
        Assert.StartsWith("opaquelocktoken:", locked.Value!.Token, StringComparison.Ordinal);

        var mine = await _storage.WriteAsync(
            _connection, "/conti/bilancio.ods", [7], new RemoteWriteConditions(locked.Value), ct);

        Assert.True(mine.Succeeded, Message(mine.Diagnostics));

        var theirs = await _storage.WriteAsync(
            _connection, "/conti/bilancio.ods", [8], null, ct);

        Assert.False(theirs.Succeeded);
        Assert.Equal("REM013", theirs.Diagnostics[0].Code);

        Assert.True((await _storage.UnlockAsync(_connection, locked.Value, ct)).Succeeded);

        Assert.True((await _storage.WriteAsync(
            _connection, "/conti/bilancio.ods", [9], null, ct)).Succeeded);
    }

    private static string Message(IReadOnlyList<Foundation.Diagnostics.Diagnostic> diagnostics) =>
        string.Join("; ", diagnostics.Select(item => $"{item.Code} {item.Message}"));

    public void Dispose()
    {
        _server.Dispose();
        _client.Dispose();
    }

    [Theory]
    [InlineData("nas.local/dav")]
    [InlineData("ftp://nas.local/dav")]
    [InlineData("")]
    public async Task UnIndirizzoNonValidoDaUnErroreInveceDiUnEccezione(string url)
    {
        var broken = new RemoteConnection { Name = "rotta", Url = url, User = "ada" };

        Assert.False((await _storage.ListAsync(broken, "/", TestContext.Current.CancellationToken)).Succeeded);
        Assert.False((await _storage.ReadAsync(broken, "a.ods", TestContext.Current.CancellationToken)).Succeeded);
        Assert.False((await _storage.WriteAsync(broken, "a.ods", [1], null, TestContext.Current.CancellationToken)).Succeeded);
        Assert.False((await _storage.LockAsync(broken, "a.ods", TestContext.Current.CancellationToken)).Succeeded);
    }

    private sealed class FixedVault(ISecretVault vault) : ISecretVaultProvider
    {
        public ISecretVault Current { get; } = vault;
    }
}

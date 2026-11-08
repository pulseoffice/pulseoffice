using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Platform;
using PulseOffice.Remote;
using PulseOffice.Remote.Cloud;
using Xunit;

namespace PulseOffice.Remote.Tests;

public class CloudSignInTests
{
    private sealed class MemoryVault : ISecretVault, ISecretVaultProvider
    {
        private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);

        public string DisplayName => "Memoria";

        public bool IsAvailable => true;

        public ISecretVault Current => this;

        public Task<string?> ReadAsync(string service, string account, CancellationToken ct = default) =>
            Task.FromResult(_secrets.GetValueOrDefault($"{service}/{account}"));

        public Task<bool> WriteAsync(string service, string account, string secret, CancellationToken ct = default)
        {
            _secrets[$"{service}/{account}"] = secret;
            return Task.FromResult(true);
        }

        public Task<bool> RemoveAsync(string service, string account, CancellationToken ct = default) =>
            Task.FromResult(_secrets.Remove($"{service}/{account}"));

        public bool Holds(string account) => _secrets.ContainsKey($"PulseOffice/{account}");
    }

    private sealed class FakeFlow : IOAuthFlow
    {
        public int SignIns { get; private set; }

        public int Refreshes { get; private set; }

        public string? SeenRefreshToken { get; private set; }

        public OAuthEndpoints EndpointsFor(RemoteProtocol protocol) => new("https://a", "https://b");

        public Task<OperationResult<OAuthTokens>> SignInAsync(
            CloudApplication application, CancellationToken ct = default)
        {
            SignIns++;

            return Task.FromResult(OperationResult<OAuthTokens>.Ok(
                new OAuthTokens("access-1", "refresh-1", DateTimeOffset.UtcNow.AddHours(1))));
        }

        public Task<OperationResult<OAuthTokens>> RefreshAsync(
            CloudApplication application, string refreshToken, CancellationToken ct = default)
        {
            Refreshes++;
            SeenRefreshToken = refreshToken;

            return Task.FromResult(OperationResult<OAuthTokens>.Ok(
                new OAuthTokens("access-2", null, DateTimeOffset.UtcNow.AddHours(1))));
        }
    }

    private sealed class OneApplication : ICloudApplications
    {
        public OperationResult<CloudApplication> For(RemoteProtocol protocol) =>
            OperationResult<CloudApplication>.Ok(new CloudApplication
            {
                Protocol = protocol,
                ClientId = "client",
            });
    }

    private static RemoteConnection Connection() =>
        new() { Name = "OneDrive", Protocol = RemoteProtocol.OneDrive };

    [Fact]
    public async Task IlGettoneDiRinnovoFinisceNelPortachiaviNonSuDisco()
    {
        var vault = new MemoryVault();
        var sessions = new CloudSessions(new OneApplication(), new FakeFlow(), vault, TimeProvider.System);

        await sessions.SignInAsync(Connection(), TestContext.Current.CancellationToken);

        Assert.True(vault.Holds("remote-token:OneDrive"));
    }

    [Fact]
    public async Task UnGettoneFrescoNonSiRinnova()
    {
        var flow = new FakeFlow();
        var sessions = new CloudSessions(new OneApplication(), flow, new MemoryVault(), TimeProvider.System);

        await sessions.SignInAsync(Connection(), TestContext.Current.CancellationToken);

        var token = await sessions.AccessTokenAsync(Connection(), TestContext.Current.CancellationToken);

        Assert.Equal("access-1", token.Value);
        Assert.Equal(0, flow.Refreshes);
    }

    [Fact]
    public async Task SenzaSessioneSiRipartDalGettoneConservato()
    {
        var vault = new MemoryVault();
        await vault.WriteAsync("PulseOffice", "remote-token:OneDrive", "refresh-conservato", TestContext.Current.CancellationToken);

        var flow = new FakeFlow();
        var sessions = new CloudSessions(new OneApplication(), flow, vault, TimeProvider.System);

        var token = await sessions.AccessTokenAsync(Connection(), TestContext.Current.CancellationToken);

        Assert.Equal("access-2", token.Value);
        Assert.Equal("refresh-conservato", flow.SeenRefreshToken);
    }

    [Fact]
    public async Task IlRinnovoConservaIlGettonePrecedenteQuandoIlServizioNonNeMandaUnoNuovo()
    {
        var vault = new MemoryVault();
        await vault.WriteAsync("PulseOffice", "remote-token:OneDrive", "refresh-conservato", TestContext.Current.CancellationToken);

        var sessions = new CloudSessions(new OneApplication(), new FakeFlow(), vault, TimeProvider.System);

        await sessions.AccessTokenAsync(Connection(), TestContext.Current.CancellationToken);

        Assert.Equal("refresh-conservato",
            await vault.ReadAsync("PulseOffice", "remote-token:OneDrive", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DisconnettersiDimenticaIlGettone()
    {
        var vault = new MemoryVault();
        var sessions = new CloudSessions(new OneApplication(), new FakeFlow(), vault, TimeProvider.System);

        await sessions.SignInAsync(Connection(), TestContext.Current.CancellationToken);
        await sessions.SignOutAsync(Connection(), TestContext.Current.CancellationToken);

        Assert.False(vault.Holds("remote-token:OneDrive"));
        Assert.False(sessions.IsSignedIn(Connection()));
    }

    [Fact]
    public void SenzaRegistrazioneLApplicazioneLoDiceInveceDiProvarci()
    {
        var applications = new CloudApplications(Path.Combine(Path.GetTempPath(), "cloud-che-non-esiste.json"));

        var outcome = applications.For(RemoteProtocol.Dropbox);

        Assert.False(outcome.Succeeded);
        Assert.Equal("CLOUD001", outcome.Diagnostics[0].Code);
    }

    [Fact]
    public void LaProvaDiPossessoUsaSha256SenzaRiempimento()
    {
        var proof = new ProofKeys().Create();

        Assert.DoesNotContain('=', proof.Challenge);
        Assert.DoesNotContain('+', proof.Challenge);
        Assert.DoesNotContain('/', proof.Challenge);
        Assert.NotEqual(proof.Verifier, proof.Challenge);
    }

    [Fact]
    public void LIndirizzoDiAccessoPortaLaSfidaELoStato()
    {
        var application = new CloudApplication
        {
            Protocol = RemoteProtocol.GoogleDrive,
            ClientId = "client",
            Scopes = "drive.file",
        };

        var address = OAuthFlow.Authorize(
            new OAuthEndpoints("https://accounts", "https://token"),
            application, "http://127.0.0.1:1234/", new ProofKey("verifier", "challenge"), "stato");

        Assert.Contains("code_challenge=challenge", address, StringComparison.Ordinal);
        Assert.Contains("code_challenge_method=S256", address, StringComparison.Ordinal);
        Assert.Contains("state=stato", address, StringComparison.Ordinal);
        Assert.Contains("access_type=offline", address, StringComparison.Ordinal);
        Assert.DoesNotContain("client_secret", address, StringComparison.Ordinal);
    }

    [Fact]
    public void IGettoniSiLeggonoConLaLoroScadenza()
    {
        var now = new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
        var tokens = OAuthFlow.ReadTokens(
            """{"access_token":"a","refresh_token":"r","expires_in":60}""", now);

        Assert.True(tokens.Succeeded);
        Assert.Equal("r", tokens.Value!.Refresh);
        Assert.Equal(now.AddSeconds(60), tokens.Value!.Expires);
        Assert.False(tokens.Value!.IsFreshAt(now));
        Assert.True(tokens.Value!.IsFreshAt(now.AddMinutes(-5)));
    }
}

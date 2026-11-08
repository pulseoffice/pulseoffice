using System.Net;
using System.Text;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Platform;
using PulseOffice.Remote;
using PulseOffice.Remote.Cloud;
using Xunit;

namespace PulseOffice.Remote.Tests;

public class CloudRound30Tests
{
    private sealed class MemoryVault : ISecretVault, ISecretVaultProvider
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _secrets = new(StringComparer.Ordinal);

        public string DisplayName => "Memory";

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
            Task.FromResult(_secrets.TryRemove($"{service}/{account}", out _));

        public bool Holds(string account) => _secrets.ContainsKey($"PulseOffice/{account}");
    }

    private sealed class Flow : IOAuthFlow
    {
        public OAuthEndpoints EndpointsFor(RemoteProtocol protocol) => new("https://a", "https://b");

        public Task<OperationResult<OAuthTokens>> SignInAsync(CloudApplication application, CancellationToken ct = default) =>
            Task.FromResult(OperationResult<OAuthTokens>.Ok(new OAuthTokens("access", "refresh", DateTimeOffset.UtcNow.AddHours(1))));

        public Task<OperationResult<OAuthTokens>> RefreshAsync(CloudApplication application, string refreshToken, CancellationToken ct = default) =>
            Task.FromResult(OperationResult<OAuthTokens>.Ok(new OAuthTokens("renewed", null, DateTimeOffset.UtcNow.AddHours(1))));
    }

    private sealed class Applications : ICloudApplications
    {
        public OperationResult<CloudApplication> For(RemoteProtocol protocol) =>
            OperationResult<CloudApplication>.Ok(new CloudApplication { Protocol = protocol, ClientId = "client" });
    }

    private sealed class ReadySession : ICloudSessions
    {
        public Task<OperationResult<string>> AccessTokenAsync(RemoteConnection connection, CancellationToken ct = default) =>
            Task.FromResult(OperationResult<string>.Ok("token"));

        public bool Handles(RemoteProtocol protocol) => true;

        public bool IsSignedIn(RemoteConnection connection) => true;

        public Task<OperationResult<bool>> SignInAsync(RemoteConnection connection, CancellationToken ct = default) =>
            Task.FromResult(OperationResult<bool>.Ok(true));

        public Task SignOutAsync(RemoteConnection connection, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class Answering(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Seen { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Seen.Add(request);
            return Task.FromResult(answer(request));
        }
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Theory]
    [InlineData("[1, 2]")]
    [InlineData("\"text\"")]
    [InlineData("""{"OneDrive": {"clientId": 42}}""")]
    [InlineData("""{"OneDrive": {"clientId": "id", "scopes": 7}}""")]
    public void AMalformedApplicationFileDoesNotThrow(string json)
    {
        var folder = Directory.CreateTempSubdirectory("pulse-cloud30-");
        var file = Path.Combine(folder.FullName, "cloud.json");
        File.WriteAllText(file, json);

        var applications = new CloudApplications(file);

        Assert.NotNull(applications.For(RemoteProtocol.GoogleDrive));
    }

    [Fact]
    public async Task ForgettingACloudConnectionDropsTheRefreshTokenAndTheLiveSession()
    {
        var vault = new MemoryVault();
        var sessions = new CloudSessions(new Applications(), new Flow(), vault, TimeProvider.System);
        var connection = new RemoteConnection { Name = "Drive", Protocol = RemoteProtocol.OneDrive };
        var ct = TestContext.Current.CancellationToken;

        await sessions.SignInAsync(connection, ct);
        Assert.True(vault.Holds(connection.TokenKey));

        await new RemoteCredentials(vault, [sessions]).ForgetAsync(connection, ct);

        Assert.False(vault.Holds(connection.TokenKey));
        Assert.False(sessions.IsSignedIn(connection));
    }

    [Fact]
    public async Task ForgettingAWebDavConnectionDropsItsPassword()
    {
        var vault = new MemoryVault();
        var connection = new RemoteConnection { Name = "Server", Protocol = RemoteProtocol.WebDav };
        var credentials = new RemoteCredentials(vault, []);
        var ct = TestContext.Current.CancellationToken;

        await credentials.KeepPasswordAsync(connection, "secret", ct);
        await credentials.ForgetAsync(connection, ct);

        Assert.Null(await credentials.PasswordAsync(connection, ct));
    }

    [Theory]
    [InlineData("report.odt", true)]
    [InlineData("rapporto finale.ods", true)]
    [InlineData("../escape.odt", false)]
    [InlineData("sub/report.odt", false)]
    [InlineData("sub\\report.odt", false)]
    [InlineData("..", false)]
    [InlineData(".", false)]
    [InlineData("line\nbreak.odt", false)]
    [InlineData(" padded.odt", false)]
    [InlineData("", false)]
    public void RemoteFileNamesRefuseSeparatorsAndClimbing(string name, bool valid) =>
        Assert.Equal(valid, new RemoteFileNames().IsValid(name));

    [Fact]
    public async Task GoogleDriveDoesNotCreateADuplicateWhenTheLookupFails()
    {
        var handler = new Answering(request =>
            request.Method == HttpMethod.Get
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("") }
                : Json("""{"id":"new"}"""));

        var storage = new GoogleDriveStorage(new HttpClient(handler), new ReadySession(), new GoogleDriveReader());

        var written = await storage.WriteAsync(
            new RemoteConnection { Name = "G", Protocol = RemoteProtocol.GoogleDrive },
            "/report.odt", [1, 2, 3], null, TestContext.Current.CancellationToken);

        Assert.False(written.Succeeded);
        Assert.DoesNotContain(handler.Seen, request => request.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task GoogleDrivePrefersTheExactlyNamedFile()
    {
        var handler = new Answering(request =>
            request.Method == HttpMethod.Get
                ? Json("""{"files":[{"id":"upper","name":"Report.odt","mimeType":"x"},{"id":"lower","name":"report.odt","mimeType":"x"}]}""")
                : Json("{}"));

        var storage = new GoogleDriveStorage(new HttpClient(handler), new ReadySession(), new GoogleDriveReader());

        var written = await storage.WriteAsync(
            new RemoteConnection { Name = "G", Protocol = RemoteProtocol.GoogleDrive },
            "/report.odt", [1], null, TestContext.Current.CancellationToken);

        Assert.True(written.Succeeded);
        Assert.Contains(handler.Seen, request => request.Method == HttpMethod.Patch && request.RequestUri!.AbsolutePath.EndsWith("/lower", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConcurrentTokenReadsAndSignOutsDoNotCorruptTheSessions()
    {
        var sessions = new CloudSessions(new Applications(), new Flow(), new MemoryVault(), TimeProvider.System);
        var ct = TestContext.Current.CancellationToken;
        var connections = Enumerable.Range(0, 32)
            .Select(index => new RemoteConnection { Name = $"c{index}", Protocol = RemoteProtocol.Dropbox }).ToList();

        await Task.WhenAll(connections.Select(connection => Task.Run(async () =>
        {
            for (var round = 0; round < 50; round++)
            {
                await sessions.SignInAsync(connection, ct);
                await sessions.AccessTokenAsync(connection, ct);
                await sessions.SignOutAsync(connection, ct);
            }
        }, ct)));

        Assert.All(connections, connection => Assert.False(sessions.IsSignedIn(connection)));
    }
}

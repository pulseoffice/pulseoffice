using System.Net;
using System.Text;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Platform;
using PulseOffice.Remote;
using PulseOffice.Remote.Cloud;
using Xunit;

namespace PulseOffice.Remote.Tests;

public class CloudStorageTests
{
    private sealed class Answering(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Seen { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            Seen.Add(request);
            return Task.FromResult(answer(request));
        }
    }

    private sealed class ReadySession : ICloudSessions
    {
        public Task<OperationResult<string>> AccessTokenAsync(
            RemoteConnection connection, CancellationToken ct = default) =>
            Task.FromResult(OperationResult<string>.Ok("token-di-prova"));

        public bool Handles(RemoteProtocol protocol) => true;

        public bool IsSignedIn(RemoteConnection connection) => true;

        public Task<OperationResult<bool>> SignInAsync(
            RemoteConnection connection, CancellationToken ct = default) =>
            Task.FromResult(OperationResult<bool>.Ok(true));

        public Task SignOutAsync(RemoteConnection connection, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static RemoteConnection Connection(RemoteProtocol protocol) =>
        new() { Name = protocol.ToString(), Protocol = protocol };

    [Fact]
    public async Task OneDriveChiedeIFigliConIlGettoneInTesta()
    {
        var handler = new Answering(_ => Json("""{"value":[{"name":"a.ods","file":{},"size":3}]}"""));

        var storage = new OneDriveStorage(new HttpClient(handler), new ReadySession(), new OneDriveReader());

        var listing = await storage.ListAsync(
            Connection(RemoteProtocol.OneDrive), "/Conti", TestContext.Current.CancellationToken);

        Assert.True(listing.Succeeded);
        Assert.Equal("/Conti/a.ods", listing.Value![0].Path);
        Assert.Equal("Bearer", handler.Seen[0].Headers.Authorization!.Scheme);
        Assert.Equal("token-di-prova", handler.Seen[0].Headers.Authorization!.Parameter);
    }

    [Fact]
    public async Task OneDriveSegueLePagineSuccessive()
    {
        var handler = new Answering(request => request.RequestUri!.ToString().Contains("skiptoken", StringComparison.Ordinal)
            ? Json("""{"value":[{"name":"b.ods","file":{},"size":3}]}""")
            : Json("""{"value":[{"name":"a.ods","file":{},"size":3}],"@odata.nextLink":"https://graph.microsoft.com/v1.0/me/drive/root/children?$skiptoken=x"}"""));

        var listing = await new OneDriveStorage(new HttpClient(handler), new ReadySession(), new OneDriveReader())
            .ListAsync(Connection(RemoteProtocol.OneDrive), "/", TestContext.Current.CancellationToken);

        Assert.Equal(["a.ods", "b.ods"], listing.Value!.Select(entry => entry.Name));
    }

    [Fact]
    public async Task UnServizioCheMandaPagineSenzaFineSiInterrompe()
    {
        var handler = new Answering(_ => Json("""{"entries":[],"has_more":true,"cursor":"sempre"}"""));

        var listing = await new DropboxStorage(new HttpClient(handler), new ReadySession(), new DropboxReader())
            .ListAsync(Connection(RemoteProtocol.Dropbox), "/", TestContext.Current.CancellationToken);

        Assert.False(listing.Succeeded);
        Assert.Equal("CLOUD032", listing.Diagnostics[0].Code);
    }

    [Fact]
    public async Task DropboxSegueIlCursore()
    {
        var handler = new Answering(request => request.RequestUri!.ToString().EndsWith("/continue", StringComparison.Ordinal)
            ? Json("""{"entries":[{".tag":"file","name":"b.ods","path_display":"/b.ods","size":1}],"has_more":false}""")
            : Json("""{"entries":[{".tag":"file","name":"a.ods","path_display":"/a.ods","size":1}],"has_more":true,"cursor":"c1"}"""));

        var listing = await new DropboxStorage(new HttpClient(handler), new ReadySession(), new DropboxReader())
            .ListAsync(Connection(RemoteProtocol.Dropbox), "/", TestContext.Current.CancellationToken);

        Assert.Equal(["a.ods", "b.ods"], listing.Value!.Select(entry => entry.Name));
    }

    [Fact]
    public async Task DropboxScaricaDichiarandoIlPercorsoNellIntestazione()
    {
        var handler = new Answering(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([1, 2, 3]),
        });

        var storage = new DropboxStorage(new HttpClient(handler), new ReadySession(), new DropboxReader());

        var content = await storage.ReadAsync(
            Connection(RemoteProtocol.Dropbox), "/Conti/a.ods", TestContext.Current.CancellationToken);

        Assert.True(content.Succeeded);
        Assert.Equal([1, 2, 3], content.Value!.Bytes);
        Assert.Contains("/Conti/a.ods", handler.Seen[0].Headers.GetValues("Dropbox-API-Arg").First(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task GoogleCamminaLeCartellePerNomePrimaDiLeggere()
    {
        var handler = new Answering(request =>
            request.RequestUri!.ToString().Contains("alt=media", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([9]) }
                : request.RequestUri!.ToString().Contains("'root'", StringComparison.Ordinal)
                  || request.RequestUri!.ToString().Contains(Uri.EscapeDataString("'root'"), StringComparison.Ordinal)
                    ? Json("""{"files":[{"id":"cartella","name":"Conti","mimeType":"application/vnd.google-apps.folder"}]}""")
                    : Json("""{"files":[{"id":"documento","name":"a.ods","mimeType":"application/octet-stream","size":"1"}]}"""));

        var storage = new GoogleDriveStorage(
            new HttpClient(handler), new ReadySession(), new GoogleDriveReader());

        var content = await storage.ReadAsync(
            Connection(RemoteProtocol.GoogleDrive), "/Conti/a.ods", TestContext.Current.CancellationToken);

        Assert.True(content.Succeeded);
        Assert.Equal([9], content.Value!.Bytes);
        Assert.Contains(handler.Seen, request =>
            request.RequestUri!.ToString().Contains("files/documento?alt=media", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UnGettoneRifiutatoDiceDiAccedereDiNuovo()
    {
        var handler = new Answering(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent(string.Empty),
        });

        var storage = new OneDriveStorage(new HttpClient(handler), new ReadySession(), new OneDriveReader());

        var listing = await storage.ListAsync(
            Connection(RemoteProtocol.OneDrive), "/", TestContext.Current.CancellationToken);

        Assert.False(listing.Succeeded);
        Assert.Equal("CLOUD013", listing.Diagnostics[0].Code);
    }

    [Fact]
    public async Task NessunServizioCloudSaPrendereInConsegna()
    {
        var storage = new DropboxStorage(new HttpClient(), new ReadySession(), new DropboxReader());

        var held = await storage.LockAsync(
            Connection(RemoteProtocol.Dropbox), "/a.ods", TestContext.Current.CancellationToken);

        Assert.False(held.Succeeded);
        Assert.Equal("CLOUD030", held.Diagnostics[0].Code);
        Assert.False(storage.SupportsCheckOut);
    }
}

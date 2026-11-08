using System.Net;
using System.Net.Http.Headers;
using System.Text;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.Remote;
using PulseOffice.Remote.Cloud;
using PulseOffice.Remote.UI;
using Xunit;

namespace PulseOffice.Remote.Tests;

public class RemoteVersionTests : IDisposable
{
    private readonly string _folder =
        Path.Combine(Path.GetTempPath(), "pulseoffice-versions-" + Guid.NewGuid().ToString("N"));

    public RemoteVersionTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);

        GC.SuppressFinalize(this);
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

    private sealed class NoVault : ISecretVaultProvider
    {
        public ISecretVault Current => throw new InvalidOperationException("No credentials are needed without a user.");
    }

    private sealed class Folder(string path) : ISyncedFolderLocator
    {
        public OperationResult<string> ICloudDrive() => OperationResult<string>.Ok(path);
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static RemoteConnection Connection(RemoteProtocol protocol) =>
        new() { Name = "c", Protocol = protocol, Url = "https://dav.example.test/files" };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string? Header(HttpRequestMessage request, string name) =>
        request.Headers.TryGetValues(name, out var values) ? string.Join(',', values) : null;

    private static WebDavStorage WebDav(HttpMessageHandler handler) =>
        new(new HttpClient(handler), new NoVault(), new WebDavResponseReader());

    [Fact]
    public async Task WebDavReadsTheETagAndSendsItAsIfMatch()
    {
        var handler = new Answering(request => request.Method == HttpMethod.Get
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1]), Headers = { ETag = new EntityTagHeaderValue("\"v1\"") } }
            : new HttpResponseMessage(HttpStatusCode.Created) { Content = new ByteArrayContent([]), Headers = { ETag = new EntityTagHeaderValue("\"v2\"") } });
        var storage = WebDav(handler);
        var connection = Connection(RemoteProtocol.WebDav);

        var read = await storage.ReadAsync(connection, "/a.odt", Ct);
        var written = await storage.WriteAsync(connection, "/a.odt", [2], new RemoteWriteConditions(null, read.Value!.Version), Ct);

        Assert.Equal("\"v1\"", Header(handler.Seen[1], "If-Match"));
        Assert.Equal(RemoteWriteStatus.Written, written.Value!.Status);
        Assert.NotEqual(read.Value!.Version, written.Value!.Version);
        Assert.Contains("v2", written.Value!.Version, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WebDavReportsAPreconditionFailureAsAConflict()
    {
        var handler = new Answering(_ => new HttpResponseMessage(HttpStatusCode.PreconditionFailed) { Content = new ByteArrayContent([]) });

        var written = await WebDav(handler).WriteAsync(
            Connection(RemoteProtocol.WebDav), "/a.odt", [2], new RemoteWriteConditions(null, "etag:\"old\""), Ct);

        Assert.True(written.Succeeded);
        Assert.True(written.Value!.IsConflict);
    }

    [Fact]
    public async Task WebDavForcedWriteSendsNoPreconditionAndAsksTheVersionWhenThePutOmitsIt()
    {
        var handler = new Answering(request => request.Method == HttpMethod.Head
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]), Headers = { ETag = new EntityTagHeaderValue("\"v9\"") } }
            : new HttpResponseMessage(HttpStatusCode.NoContent) { Content = new ByteArrayContent([]) });

        var written = await WebDav(handler).WriteAsync(
            Connection(RemoteProtocol.WebDav), "/a.odt", [2], new RemoteWriteConditions(), Ct);

        Assert.Null(Header(handler.Seen[0], "If-Match"));
        Assert.Equal("etag:\"v9\"", written.Value!.Version);
    }

    [Fact]
    public async Task WebDavFallsBackToTheModificationTimeForAWeakETag()
    {
        var modified = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);
        var handler = new Answering(request =>
        {
            var response = new HttpResponseMessage(request.Method == HttpMethod.Get ? HttpStatusCode.OK : HttpStatusCode.PreconditionFailed)
            {
                Content = new ByteArrayContent([1]),
                Headers = { ETag = new EntityTagHeaderValue("\"w\"", isWeak: true) },
            };
            response.Content.Headers.LastModified = modified;
            return response;
        });
        var storage = WebDav(handler);
        var connection = Connection(RemoteProtocol.WebDav);

        var read = await storage.ReadAsync(connection, "/a.odt", Ct);
        var written = await storage.WriteAsync(connection, "/a.odt", [2], new RemoteWriteConditions(null, read.Value!.Version), Ct);

        Assert.Equal(modified, handler.Seen[1].Headers.IfUnmodifiedSince);
        Assert.True(written.Value!.IsConflict);
    }

    [Fact]
    public async Task OneDriveReadsTheETagAndSendsItAsIfMatch()
    {
        var handler = new Answering(request => request.Method == HttpMethod.Put
            ? Json("""{"eTag":"\"{B},2\""}""")
            : request.RequestUri!.AbsolutePath.EndsWith("/content", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([5]) }
                : Json("""{"eTag":"\"{B},1\""}"""));
        var storage = new OneDriveStorage(new HttpClient(handler), new ReadySession(), new OneDriveReader());
        var connection = Connection(RemoteProtocol.OneDrive);

        var read = await storage.ReadAsync(connection, "/a.odt", Ct);
        var written = await storage.WriteAsync(connection, "/a.odt", [6], new RemoteWriteConditions(null, read.Value!.Version), Ct);

        Assert.Equal([5], read.Value!.Bytes);
        Assert.Equal("\"{B},1\"", read.Value!.Version);
        Assert.Equal("\"{B},1\"", Header(handler.Seen[^1], "If-Match"));
        Assert.Equal("\"{B},2\"", written.Value!.Version);
    }

    [Fact]
    public async Task OneDriveReportsAPreconditionFailureAsAConflictAndOverwritesWhenForced()
    {
        var handler = new Answering(request => Header(request, "If-Match") is null
            ? Json("""{"eTag":"\"{B},3\""}""")
            : Json("""{"error":{"code":"resourceModified"}}""", HttpStatusCode.PreconditionFailed));
        var storage = new OneDriveStorage(new HttpClient(handler), new ReadySession(), new OneDriveReader());
        var connection = Connection(RemoteProtocol.OneDrive);

        var conflict = await storage.WriteAsync(connection, "/a.odt", [6], new RemoteWriteConditions(null, "\"{B},1\""), Ct);
        var forced = await storage.WriteAsync(connection, "/a.odt", [6], new RemoteWriteConditions(), Ct);

        Assert.True(conflict.Value!.IsConflict);
        Assert.Equal(RemoteWriteStatus.Written, forced.Value!.Status);
        Assert.Equal("\"{B},3\"", forced.Value!.Version);
    }

    [Fact]
    public async Task DropboxReadsTheRevisionAndUploadsInUpdateMode()
    {
        var handler = new Answering(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/download", StringComparison.Ordinal))
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1]) };
                response.Headers.Add("Dropbox-API-Result", """{"name":"a.odt","rev":"015a"}""");
                return response;
            }

            return Json("""{"name":"a.odt","rev":"015b"}""");
        });
        var storage = new DropboxStorage(new HttpClient(handler), new ReadySession(), new DropboxReader());
        var connection = Connection(RemoteProtocol.Dropbox);

        var read = await storage.ReadAsync(connection, "/a.odt", Ct);
        var written = await storage.WriteAsync(connection, "/a.odt", [2], new RemoteWriteConditions(null, read.Value!.Version), Ct);

        Assert.Equal("015a", read.Value!.Version);
        var argument = Header(handler.Seen[1], "Dropbox-API-Arg")!;
        Assert.Contains("\"update\": \"015a\"", argument, StringComparison.Ordinal);
        Assert.DoesNotContain("overwrite", argument, StringComparison.Ordinal);
        Assert.Equal("015b", written.Value!.Version);
    }

    [Fact]
    public async Task DropboxReportsAPathConflictAndOverwritesWhenForced()
    {
        var handler = new Answering(request => Header(request, "Dropbox-API-Arg")!.Contains("update", StringComparison.Ordinal)
            ? Json("""{"error_summary":"path/conflict/file/..","error":{".tag":"path"}}""", HttpStatusCode.Conflict)
            : Json("""{"rev":"0200"}"""));
        var storage = new DropboxStorage(new HttpClient(handler), new ReadySession(), new DropboxReader());
        var connection = Connection(RemoteProtocol.Dropbox);

        var conflict = await storage.WriteAsync(connection, "/a.odt", [2], new RemoteWriteConditions(null, "015a"), Ct);
        var forced = await storage.WriteAsync(connection, "/a.odt", [2], null, Ct);

        Assert.True(conflict.Value!.IsConflict);
        Assert.Contains("\"mode\": \"overwrite\"", Header(handler.Seen[1], "Dropbox-API-Arg"), StringComparison.Ordinal);
        Assert.Equal("0200", forced.Value!.Version);
    }

    [Fact]
    public async Task DropboxDoesNotMistakeAnotherConflictForAVersionConflict()
    {
        var handler = new Answering(_ => Json("""{"error_summary":"path/malformed_path/.."}""", HttpStatusCode.Conflict));
        var storage = new DropboxStorage(new HttpClient(handler), new ReadySession(), new DropboxReader());

        var written = await storage.WriteAsync(Connection(RemoteProtocol.Dropbox), "/a.odt", [2], new RemoteWriteConditions(null, "015a"), Ct);

        Assert.False(written.Succeeded);
    }

    private static Answering Drive(string revision) => new(request =>
        request.Method == HttpMethod.Patch
            ? Json("""{"id":"doc","name":"a.odt","mimeType":"x","headRevisionId":"r9"}""")
            : request.RequestUri!.Query.Contains("alt=media", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([3]) }
                : Json($$"""{"files":[{"id":"doc","name":"a.odt","mimeType":"x","headRevisionId":"{{revision}}"}]}"""));

    [Fact]
    public async Task GoogleDriveReadsTheHeadRevisionAndUpdatesItAfterSaving()
    {
        var handler = Drive("r1");
        var storage = new GoogleDriveStorage(new HttpClient(handler), new ReadySession(), new GoogleDriveReader());
        var connection = Connection(RemoteProtocol.GoogleDrive);

        var read = await storage.ReadAsync(connection, "/a.odt", Ct);
        var written = await storage.WriteAsync(connection, "/a.odt", [4], new RemoteWriteConditions(null, read.Value!.Version), Ct);

        Assert.Equal("r1", read.Value!.Version);
        Assert.Equal(RemoteWriteStatus.Written, written.Value!.Status);
        Assert.Equal("r9", written.Value!.Version);
    }

    [Fact]
    public async Task GoogleDriveDetectsANewerRevisionWithoutUploadingAndOverwritesWhenForced()
    {
        var handler = Drive("r2");
        var storage = new GoogleDriveStorage(new HttpClient(handler), new ReadySession(), new GoogleDriveReader());
        var connection = Connection(RemoteProtocol.GoogleDrive);

        var conflict = await storage.WriteAsync(connection, "/a.odt", [4], new RemoteWriteConditions(null, "r1"), Ct);

        Assert.True(conflict.Value!.IsConflict);
        Assert.DoesNotContain(handler.Seen, request => request.Method == HttpMethod.Patch);

        var forced = await storage.WriteAsync(connection, "/a.odt", [4], new RemoteWriteConditions(), Ct);

        Assert.Equal("r9", forced.Value!.Version);
        Assert.Contains(handler.Seen, request => request.Method == HttpMethod.Patch);
    }

    [Fact]
    public async Task ICloudDriveDetectsAFileChangedByAnotherDevice()
    {
        var storage = new ICloudDriveStorage(new Folder(_folder), new PulseOffice.Platform.RuntimeHostSystem());
        var connection = Connection(RemoteProtocol.ICloudDrive);

        var first = await storage.WriteAsync(connection, "/a.odt", [1], null, Ct);
        var read = await storage.ReadAsync(connection, "/a.odt", Ct);
        Assert.Equal(first.Value!.Version, read.Value!.Version);

        await File.WriteAllBytesAsync(Path.Combine(_folder, "a.odt"), [7, 7], Ct);
        File.SetLastWriteTimeUtc(Path.Combine(_folder, "a.odt"), DateTime.UtcNow.AddMinutes(1));

        var conflict = await storage.WriteAsync(connection, "/a.odt", [2], new RemoteWriteConditions(null, read.Value!.Version), Ct);
        Assert.True(conflict.Value!.IsConflict);
        Assert.Equal([7, 7], await File.ReadAllBytesAsync(Path.Combine(_folder, "a.odt"), Ct));

        var forced = await storage.WriteAsync(connection, "/a.odt", [2], new RemoteWriteConditions(), Ct);
        var again = await storage.WriteAsync(connection, "/a.odt", [3], new RemoteWriteConditions(null, forced.Value!.Version), Ct);
        Assert.Equal(RemoteWriteStatus.Written, again.Value!.Status);
    }

    private sealed class ScriptedStorage(params RemoteWriteOutcome[] outcomes) : IRemoteStorage
    {
        private int _next;

        public List<(string Path, RemoteWriteConditions? Conditions)> Writes { get; } = [];

        public RemoteProtocol Protocol => RemoteProtocol.Dropbox;

        public Task<OperationResult<IReadOnlyList<RemoteEntry>>> ListAsync(RemoteConnection connection, string path, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<OperationResult<RemoteContent>> ReadAsync(RemoteConnection connection, string path, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<OperationResult<RemoteWriteOutcome>> WriteAsync(
            RemoteConnection connection, string path, byte[] content, RemoteWriteConditions? conditions, CancellationToken ct = default)
        {
            Writes.Add((path, conditions));
            return Task.FromResult(OperationResult<RemoteWriteOutcome>.Ok(outcomes[Math.Min(_next++, outcomes.Length - 1)]));
        }

        public Task<OperationResult<RemoteLock>> LockAsync(RemoteConnection connection, string path, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<OperationResult<bool>> UnlockAsync(RemoteConnection connection, RemoteLock held, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class Prompt(RemoteConflictChoice choice) : IRemoteConflictPrompt
    {
        public int Asked { get; private set; }

        public Task<RemoteConflictChoice> ChooseAsync(string remotePath, CancellationToken ct = default)
        {
            Asked++;
            return Task.FromResult(choice);
        }
    }

    private sealed class CopyTarget(string? path) : IRemoteCopyTarget
    {
        public string? Suggested { get; private set; }

        public Task<RemoteChoice?> ChooseAsync(RemoteConnection connection, string suggestedName, CancellationToken ct = default)
        {
            Suggested = suggestedName;
            return Task.FromResult(path is null ? null : new RemoteChoice(connection, path));
        }
    }

    private static RemoteWriteOutcome Conflict => new(RemoteWriteStatus.Conflict, null);

    private static RemoteWriteOutcome WrittenAs(string version) => new(RemoteWriteStatus.Written, version);

    private static RemoteUploadRequest Request() =>
        new("/tmp/a.odt", Connection(RemoteProtocol.Dropbox), "/docs/a.odt", [1], null, "v1");

    private static RemoteUploads Uploads(ScriptedStorage storage, Prompt prompt) =>
        new(new RemoteStorageProvider([storage]), prompt, new TextCatalog(UiLanguage.English));

    [Fact]
    public async Task ANormalSaveSendsTheKnownVersionAndKeepsTheNewOne()
    {
        var storage = new ScriptedStorage(WrittenAs("v2"));
        var prompt = new Prompt(RemoteConflictChoice.Cancel);

        var result = await Uploads(storage, prompt).UploadAsync(Request(), new CopyTarget(null), Ct);

        Assert.Equal(RemoteUploadStatus.Uploaded, result.Status);
        Assert.Equal("v2", result.Document!.Version);
        Assert.Equal("v1", storage.Writes[0].Conditions!.ExpectedVersion);
        Assert.Equal(0, prompt.Asked);
    }

    [Fact]
    public async Task ChoosingToOverwriteWritesAgainWithoutTheVersion()
    {
        var storage = new ScriptedStorage(Conflict, WrittenAs("v3"));
        var prompt = new Prompt(RemoteConflictChoice.Overwrite);

        var result = await Uploads(storage, prompt).UploadAsync(Request(), new CopyTarget(null), Ct);

        Assert.Equal(1, prompt.Asked);
        Assert.Equal(2, storage.Writes.Count);
        Assert.Null(storage.Writes[1].Conditions!.ExpectedVersion);
        Assert.Equal("/docs/a.odt", result.Document!.RemotePath);
        Assert.Equal("v3", result.Document!.Version);
    }

    [Fact]
    public async Task ChoosingACopyWritesUnderTheNewNameAndFollowsIt()
    {
        var storage = new ScriptedStorage(Conflict, WrittenAs("c1"));
        var copies = new CopyTarget("/docs/a (copy).odt");

        var result = await Uploads(storage, new Prompt(RemoteConflictChoice.SaveCopy)).UploadAsync(Request(), copies, Ct);

        Assert.Equal("a (copy).odt", copies.Suggested);
        Assert.Equal("/docs/a (copy).odt", storage.Writes[1].Path);
        Assert.Null(storage.Writes[1].Conditions!.ExpectedVersion);
        Assert.Equal("/docs/a (copy).odt", result.Document!.RemotePath);
        Assert.Equal("c1", result.Document!.Version);
    }

    [Theory]
    [InlineData(RemoteConflictChoice.Cancel, null)]
    [InlineData(RemoteConflictChoice.SaveCopy, null)]
    public async Task CancellingLeavesTheServerUntouched(RemoteConflictChoice choice, string? copyPath)
    {
        var storage = new ScriptedStorage(Conflict, WrittenAs("x"));

        var result = await Uploads(storage, new Prompt(choice)).UploadAsync(Request(), new CopyTarget(copyPath), Ct);

        Assert.Equal(RemoteUploadStatus.Cancelled, result.Status);
        Assert.Single(storage.Writes);
    }

    [Fact]
    public async Task AFailureIsReportedWithoutAskingAboutConflicts()
    {
        var prompt = new Prompt(RemoteConflictChoice.Overwrite);
        var storage = new FailingStorage();
        var uploads = new RemoteUploads(new RemoteStorageProvider([storage]), prompt, new TextCatalog(UiLanguage.English));

        var result = await uploads.UploadAsync(Request(), new CopyTarget(null), Ct);

        Assert.Equal(RemoteUploadStatus.Failed, result.Status);
        Assert.Equal("CLOUD014", result.Failure!.Code);
        Assert.Equal(0, prompt.Asked);
    }

    private sealed class FailingStorage : IRemoteStorage
    {
        public RemoteProtocol Protocol => RemoteProtocol.Dropbox;

        public Task<OperationResult<IReadOnlyList<RemoteEntry>>> ListAsync(RemoteConnection connection, string path, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<OperationResult<RemoteContent>> ReadAsync(RemoteConnection connection, string path, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<OperationResult<RemoteWriteOutcome>> WriteAsync(
            RemoteConnection connection, string path, byte[] content, RemoteWriteConditions? conditions, CancellationToken ct = default) =>
            Task.FromResult(OperationResult<RemoteWriteOutcome>.Fail(Diagnostic.Error("CLOUD014", "Access denied.")));

        public Task<OperationResult<RemoteLock>> LockAsync(RemoteConnection connection, string path, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<OperationResult<bool>> UnlockAsync(RemoteConnection connection, RemoteLock held, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}

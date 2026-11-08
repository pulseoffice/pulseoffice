using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Localization;

namespace PulseOffice.Remote.UI;

public enum RemoteConflictChoice { Overwrite, SaveCopy, Cancel }

public interface IRemoteConflictPrompt
{
    Task<RemoteConflictChoice> ChooseAsync(string remotePath, CancellationToken ct = default);
}

public interface IRemoteCopyTarget
{
    Task<RemoteChoice?> ChooseAsync(RemoteConnection connection, string suggestedName, CancellationToken ct = default);
}

public sealed record RemoteUploadRequest(
    string LocalPath, RemoteConnection Connection, string RemotePath, byte[] Content,
    RemoteLock? Held, string? ExpectedVersion);

public enum RemoteUploadStatus { Uploaded, Cancelled, Failed }

public sealed record RemoteUploadResult(RemoteUploadStatus Status, RemoteDocument? Document, Diagnostic? Failure);

public interface IRemoteUploads
{
    Task<RemoteUploadResult> UploadAsync(RemoteUploadRequest request, IRemoteCopyTarget copies, CancellationToken ct = default);
}

public sealed class RemoteUploads(
    IRemoteStorageProvider storages, IRemoteConflictPrompt prompt, ITextCatalog texts) : IRemoteUploads
{
    public async Task<RemoteUploadResult> UploadAsync(
        RemoteUploadRequest request, IRemoteCopyTarget copies, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(copies);

        var conditions = new RemoteWriteConditions(request.Held, request.ExpectedVersion);
        var first = await WriteAsync(request, request.Connection, request.RemotePath, conditions, ct).ConfigureAwait(true);

        if (!first.Succeeded || !first.Value!.IsConflict)
            return Settled(request, request.Connection, request.RemotePath, request.Held, first);

        switch (await prompt.ChooseAsync(request.RemotePath, ct).ConfigureAwait(true))
        {
            case RemoteConflictChoice.Overwrite:
                var forced = await WriteAsync(
                    request, request.Connection, request.RemotePath, conditions with { ExpectedVersion = null }, ct).ConfigureAwait(true);
                return Settled(request, request.Connection, request.RemotePath, request.Held, forced);

            case RemoteConflictChoice.SaveCopy:
                if (await copies.ChooseAsync(request.Connection, CopyName(request.RemotePath), ct).ConfigureAwait(true) is not { } target)
                    return Cancelled;

                var copied = await WriteAsync(request, target.Connection, target.Path, new RemoteWriteConditions(), ct).ConfigureAwait(true);
                return Settled(request, target.Connection, target.Path, null, copied);

            default:
                return Cancelled;
        }
    }

    private RemoteUploadResult Cancelled { get; } = new(RemoteUploadStatus.Cancelled, null, null);

    private Task<OperationResult<RemoteWriteOutcome>> WriteAsync(
        RemoteUploadRequest request, RemoteConnection connection, string remotePath,
        RemoteWriteConditions conditions, CancellationToken ct) =>
        storages.For(connection.Protocol).WriteAsync(connection, remotePath, request.Content, conditions, ct);

    private RemoteUploadResult Settled(
        RemoteUploadRequest request, RemoteConnection connection, string remotePath, RemoteLock? held,
        OperationResult<RemoteWriteOutcome> written)
    {
        if (!written.Succeeded) return new RemoteUploadResult(RemoteUploadStatus.Failed, null, written.Diagnostics[0]);

        if (written.Value!.IsConflict) return Cancelled;

        return new RemoteUploadResult(RemoteUploadStatus.Uploaded, new RemoteDocument
        {
            LocalPath = request.LocalPath,
            Connection = connection,
            RemotePath = remotePath,
            Held = held,
            Version = written.Value!.Version,
        }, null);
    }

    public string CopyName(string remotePath)
    {
        var name = (remotePath ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
        var extension = Path.GetExtension(name);
        var stem = name[..^extension.Length];

        return texts.Format("remote.conflict.copyName", stem.Length == 0 ? "document" : stem, extension);
    }
}

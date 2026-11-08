namespace PulseOffice.Remote;

public enum RemoteProtocol { WebDav, OneDrive, GoogleDrive, Dropbox, ICloudDrive }

public static class RemoteProtocols
{
    public static bool IsCloud(this RemoteProtocol protocol) => protocol is not RemoteProtocol.WebDav;

    public static bool NeedsSignIn(this RemoteProtocol protocol) =>
        protocol is RemoteProtocol.OneDrive or RemoteProtocol.GoogleDrive or RemoteProtocol.Dropbox;

    public static bool NeedsAddress(this RemoteProtocol protocol) => protocol is RemoteProtocol.WebDav;
}

public sealed record RemoteConnection
{
    public required string Name { get; init; }

    public RemoteProtocol Protocol { get; init; } = RemoteProtocol.WebDav;

    public string Url { get; init; } = string.Empty;

    public string User { get; init; } = string.Empty;

    public string BasePath { get; init; } = "/";

    public string SecretKey => $"remote:{Name}";

    public string TokenKey => $"remote-token:{Name}";
}

public interface IRemoteConnectionDirectory
{
    IReadOnlyList<RemoteConnection> All { get; }

    RemoteConnection? Find(string name);

    void Save(RemoteConnection connection);

    bool Remove(string name);
}

public sealed record RemoteEntry(string Name, string Path, bool IsFolder, long Size, DateTimeOffset? Modified);

public sealed record RemoteLock(string Token, string Path);

public sealed record RemoteContent(byte[] Bytes, string? Version);

public sealed record RemoteWriteConditions(RemoteLock? Held = null, string? ExpectedVersion = null);

public enum RemoteWriteStatus { Written, Conflict }

public sealed record RemoteWriteOutcome(RemoteWriteStatus Status, string? Version)
{
    public bool IsConflict => Status == RemoteWriteStatus.Conflict;
}

public interface IRemoteStorage
{
    RemoteProtocol Protocol { get; }

    bool SupportsCheckOut => true;

    Task<Foundation.Diagnostics.OperationResult<IReadOnlyList<RemoteEntry>>> ListAsync(
        RemoteConnection connection, string path, CancellationToken ct = default);

    Task<Foundation.Diagnostics.OperationResult<RemoteContent>> ReadAsync(
        RemoteConnection connection, string path, CancellationToken ct = default);

    Task<Foundation.Diagnostics.OperationResult<RemoteWriteOutcome>> WriteAsync(
        RemoteConnection connection, string path, byte[] content, RemoteWriteConditions? conditions,
        CancellationToken ct = default);

    Task<Foundation.Diagnostics.OperationResult<RemoteLock>> LockAsync(
        RemoteConnection connection, string path, CancellationToken ct = default);

    Task<Foundation.Diagnostics.OperationResult<bool>> UnlockAsync(
        RemoteConnection connection, RemoteLock held, CancellationToken ct = default);
}

public interface IRemoteSignIn
{
    bool Handles(RemoteProtocol protocol);

    bool IsSignedIn(RemoteConnection connection);

    Task<Foundation.Diagnostics.OperationResult<bool>> SignInAsync(
        RemoteConnection connection, CancellationToken ct = default);

    Task SignOutAsync(RemoteConnection connection, CancellationToken ct = default);
}

public interface IRemoteStorageProvider
{
    IRemoteStorage For(RemoteProtocol protocol);
}

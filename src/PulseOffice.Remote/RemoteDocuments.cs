namespace PulseOffice.Remote;

public sealed record RemoteDocument
{
    public required string LocalPath { get; init; }

    public required RemoteConnection Connection { get; init; }

    public required string RemotePath { get; init; }

    public RemoteLock? Held { get; init; }

    public string? Version { get; init; }

    public bool IsCheckedOut => Held is not null;
}

public interface IRemoteDocuments
{
    IReadOnlyList<RemoteDocument> All { get; }

    RemoteDocument? For(string localPath);

    RemoteDocument? Following(RemoteConnection connection, string remotePath);

    void Follow(RemoteDocument document);

    void Forget(string localPath);
}

public sealed class RemoteDocuments : IRemoteDocuments
{
    private readonly Dictionary<string, RemoteDocument> _documents =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<RemoteDocument> All => [.. _documents.Values];

    public RemoteDocument? For(string localPath) => _documents.GetValueOrDefault(localPath);

    public RemoteDocument? Following(RemoteConnection connection, string remotePath)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return _documents.Values.FirstOrDefault(document =>
            document.Connection.Protocol == connection.Protocol
            && string.Equals(document.Connection.Url.TrimEnd('/'), connection.Url.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
            && string.Equals(document.Connection.User, connection.User, StringComparison.Ordinal)
            && string.Equals(document.RemotePath, remotePath, StringComparison.Ordinal));
    }

    public void Follow(RemoteDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        _documents[document.LocalPath] = document;
    }

    public void Forget(string localPath) => _documents.Remove(localPath);
}

public sealed class RemoteStorageProvider(IEnumerable<IRemoteStorage> storages) : IRemoteStorageProvider
{
    public IRemoteStorage For(RemoteProtocol protocol) =>
        storages.FirstOrDefault(storage => storage.Protocol == protocol)
        ?? throw new NotSupportedException($"No storage for {protocol}.");
}

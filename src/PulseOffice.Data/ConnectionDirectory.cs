using System.Text.Json;
using System.Text.Json.Serialization;
using PulseOffice.Platform;

namespace PulseOffice.Data;

public interface IConnectionDirectory
{
    IReadOnlyList<ConnectionDescriptor> All { get; }

    ConnectionDescriptor? Find(string id);

    Task SaveAsync(ConnectionDescriptor connection, string? secret, CancellationToken ct = default);

    Task RemoveAsync(string id, CancellationToken ct = default);
}

public sealed record StoredConnection
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("name")] public string? DisplayName { get; init; }
    [JsonPropertyName("provider")] public string? Provider { get; init; }
    [JsonPropertyName("connectionString")] public string? ConnectionString { get; init; }
    [JsonPropertyName("user")] public string? UserName { get; init; }
    [JsonPropertyName("credential")] public string? CredentialKey { get; init; }
    [JsonPropertyName("password")] public string? InsecureSecret { get; init; }

    [JsonPropertyName("nome")] public string? LegacyDisplayName { get; init; }
    [JsonPropertyName("stringa")] public string? LegacyConnectionString { get; init; }
    [JsonPropertyName("utente")] public string? LegacyUserName { get; init; }
    [JsonPropertyName("credenziale")] public string? LegacyCredentialKey { get; init; }

    public StoredConnection Migrated() => this with
    {
        DisplayName = DisplayName ?? LegacyDisplayName,
        ConnectionString = ConnectionString ?? LegacyConnectionString,
        UserName = UserName ?? LegacyUserName,
        CredentialKey = CredentialKey ?? LegacyCredentialKey,
        LegacyDisplayName = null,
        LegacyConnectionString = null,
        LegacyUserName = null,
        LegacyCredentialKey = null,
    };
}

public sealed class FileConnectionDirectory : IConnectionDirectory
{
    private readonly string _path;
    private readonly ICredentialStore _credentials;
    private readonly IConnectionSecretSplitter _splitter;
    private readonly IPrivateFiles _privateFiles;
    private Dictionary<string, ConnectionDescriptor> _connections = [];

    public FileConnectionDirectory(
        string path, ICredentialStore credentials, IConnectionSecretSplitter? splitter = null, IPrivateFiles? privateFiles = null)
    {
        _privateFiles = privateFiles ?? new PrivateFilesFactory().Create();
        _path = path;
        _credentials = credentials;
        _splitter = splitter ?? new ConnectionSecretSplitter();
        Reload();
    }

    public IReadOnlyList<ConnectionDescriptor> All => [.. _connections.Values];

    public async Task SaveAsync(
        ConnectionDescriptor connection, string? secret, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (!connection.SecretInFile)
        {
            var split = _splitter.Split(connection.Provider, connection.ConnectionString);
            connection = connection with { ConnectionString = split.ConnectionString };
            if (secret is not { Length: > 0 }) secret = split.Secret;
        }

        var key = connection.CredentialKey ?? connection.Id;
        var kept = secret is { Length: > 0 }
            ? secret
            : connection.SecretInFile ? _connections.GetValueOrDefault(connection.Id)?.InsecureSecret : null;

        var stored = connection.SecretInFile
            ? connection with { CredentialKey = null, InsecureSecret = kept }
            : connection with { CredentialKey = key, InsecureSecret = null };

        if (stored.SecretInFile) await _credentials.RemoveAsync(key, ct);
        else if (secret is { Length: > 0 }) await _credentials.SetSecretAsync(key, secret, ct);

        _connections[stored.Id] = stored;
        await WriteAsync(ct);
    }

    public async Task RemoveAsync(string id, CancellationToken ct = default)
    {
        if (!_connections.Remove(id, out var removed)) return;

        if (removed.CredentialKey is { Length: > 0 } key)
            await _credentials.RemoveAsync(key, ct);

        await WriteAsync(ct);
    }

    private async Task WriteAsync(CancellationToken ct)
    {
        var stored = _connections.Values
            .OrderBy(connection => connection.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(connection => new StoredConnection
            {
                Id = connection.Id,
                DisplayName = connection.DisplayName,
                Provider = connection.Provider.ToString(),
                ConnectionString = connection.ConnectionString,
                UserName = connection.UserName,
                CredentialKey = connection.CredentialKey,
                InsecureSecret = connection.InsecureSecret,
            })
            .ToList();

        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

        var temporary = _path + ".tmp";
        await using (var stream = _privateFiles.Open(temporary, FileMode.Create))
        {
            await JsonSerializer.SerializeAsync(stream, stored, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            }, ct);
        }

        File.Move(temporary, _path, overwrite: true);
    }

    public ConnectionDescriptor? Find(string id) =>
        _connections.TryGetValue(id, out var connection) ? connection : null;

    public void Reload()
    {
        _connections = [];

        if (!File.Exists(_path)) return;

        List<StoredConnection>? stored;

        try
        {
            stored = JsonSerializer.Deserialize<List<StoredConnection>>(File.ReadAllText(_path));
        }
        catch (JsonException)
        {
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var read in stored ?? [])
        {
            var entry = read.Migrated();

            if (entry.Id is not { Length: > 0 } id) continue;
            if (entry.ConnectionString is not { Length: > 0 } text) continue;

            if (!Enum.TryParse<DatabaseProviderKind>(entry.Provider, ignoreCase: true, out var kind))
                continue;

            _connections[id] = new ConnectionDescriptor
            {
                Id = id,
                DisplayName = entry.DisplayName ?? id,
                Provider = kind,
                ConnectionString = text,
                UserName = entry.UserName,
                CredentialKey = entry.CredentialKey,
                SecretInFile = entry.InsecureSecret is { Length: > 0 },
                InsecureSecret = entry.InsecureSecret,
            };
        }
    }
}

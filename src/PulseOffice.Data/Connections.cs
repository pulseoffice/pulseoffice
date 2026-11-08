namespace PulseOffice.Data;

public enum DatabaseProviderKind { Odbc, SqlServer, PostgreSql, MySql, Sqlite }

public sealed record ConnectionDescriptor
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required DatabaseProviderKind Provider { get; init; }

    public required string ConnectionString { get; init; }

    public string? UserName { get; init; }

    public string? CredentialKey { get; init; }

    public bool SecretInFile { get; init; }

    public string? InsecureSecret { get; init; }

    public TimeSpan CommandTimeout { get; init; } = TimeSpan.FromSeconds(30);
}

public sealed record ResultColumn(string Name, Type ClrType, bool AllowsNull);

public sealed record QueryResult(
    IReadOnlyList<ResultColumn> Columns,
    IReadOnlyList<object?[]> Rows,
    TimeSpan Elapsed,
    bool Truncated);

public sealed record QueryDefinition
{
    public required string Sql { get; init; }
    public IReadOnlyDictionary<string, object?> Parameters { get; init; } =
        new Dictionary<string, object?>();

    public int MaxRows { get; init; } = 100_000;
}

public interface IDatabaseProvider
{
    DatabaseProviderKind Kind { get; }
    string DisplayName { get; }

    Task<bool> TestConnectionAsync(ConnectionDescriptor connection, CancellationToken ct);

    Task<QueryResult> ExecuteAsync(ConnectionDescriptor connection, QueryDefinition query, CancellationToken ct);

    Task<IReadOnlyList<string>> ListTablesAsync(ConnectionDescriptor connection, CancellationToken ct);

    string QuoteIdentifier(string identifier);

    string QuoteListedTable(string listedTable);
}

public interface ICredentialStore
{
    Task<string?> GetSecretAsync(string credentialKey, CancellationToken ct);
    Task SetSecretAsync(string credentialKey, string secret, CancellationToken ct);
    Task RemoveAsync(string credentialKey, CancellationToken ct);
}

public sealed class DatabaseProviderRegistry
{
    private readonly Dictionary<DatabaseProviderKind, IDatabaseProvider> _providers = [];

    public void Register(IDatabaseProvider provider) => _providers[provider.Kind] = provider;

    public IDatabaseProvider Get(DatabaseProviderKind kind) =>
        _providers.TryGetValue(kind, out var p)
            ? p
            : throw new NotSupportedException($"Provider '{kind}' is not registered in this installation.");

    public bool IsAvailable(DatabaseProviderKind kind) => _providers.ContainsKey(kind);
    public IReadOnlyCollection<IDatabaseProvider> All => _providers.Values;
}

public interface IDatabaseProviderNames
{
    string Of(DatabaseProviderKind kind);
}

public sealed class DatabaseProviderNames : IDatabaseProviderNames
{
    private readonly Dictionary<DatabaseProviderKind, string> _names = new()
    {
        [DatabaseProviderKind.Odbc] = "ODBC",
        [DatabaseProviderKind.SqlServer] = "SQL Server",
        [DatabaseProviderKind.PostgreSql] = "PostgreSQL",
        [DatabaseProviderKind.MySql] = "MySQL",
        [DatabaseProviderKind.Sqlite] = "SQLite",
    };

    public string Of(DatabaseProviderKind kind) => _names.TryGetValue(kind, out var name) ? name : kind.ToString();
}

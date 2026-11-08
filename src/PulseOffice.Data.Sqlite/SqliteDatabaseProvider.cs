using System.Data.Common;
using Microsoft.Data.Sqlite;
using PulseOffice.Data;

namespace PulseOffice.Data.Sqlite;

public sealed class SqliteDatabaseProvider(ICredentialStore credentials) : AdoDatabaseProvider(credentials)
{
    public override DatabaseProviderKind Kind => DatabaseProviderKind.Sqlite;
    public override string DisplayName => "SQLite";

    protected override DbConnection CreateConnection(string connectionString) => new SqliteConnection(connectionString);

    protected override string ApplyCredentials(string connectionString, string? userName, string? secret)
    {
        _ = userName; _ = secret;
        return connectionString;
    }

    protected override string ListTablesSql => "SELECT name FROM sqlite_master WHERE type IN ('table','view') ORDER BY name";

    protected override bool ListsSchemaQualifiedTables => false;

    protected override string ReadOnlyConnectionString(string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (builder.Mode != SqliteOpenMode.Memory) builder.Mode = SqliteOpenMode.ReadOnly;
        return builder.ConnectionString;
    }

    protected override async Task<DbTransaction> BeginReadOnlyAsync(DbConnection connection, CancellationToken ct)
    {
        await using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA query_only = ON";
            await pragma.ExecuteNonQueryAsync(ct);
        }
        return ((SqliteConnection)connection).BeginTransaction(deferred: true);
    }

    public override string QuoteIdentifier(string identifier) =>
        "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}

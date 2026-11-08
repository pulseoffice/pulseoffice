using System.Data.Common;
using Npgsql;
using PulseOffice.Data;

namespace PulseOffice.Data.PostgreSql;

public sealed class PostgreSqlDatabaseProvider(ICredentialStore credentials) : AdoDatabaseProvider(credentials)
{
    public override DatabaseProviderKind Kind => DatabaseProviderKind.PostgreSql;
    public override string DisplayName => "PostgreSQL";

    protected override DbConnection CreateConnection(string connectionString) => new NpgsqlConnection(connectionString);

    protected override string ApplyCredentials(string connectionString, string? userName, string? secret)
    {
        var b = new NpgsqlConnectionStringBuilder(connectionString);
        if (userName is not null) b.Username = userName;
        if (secret is not null) b.Password = secret;
        return b.ConnectionString;
    }

    protected override async Task<DbTransaction> BeginReadOnlyAsync(DbConnection connection, CancellationToken ct)
    {
        var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SET TRANSACTION READ ONLY";
            await command.ExecuteNonQueryAsync(ct);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    protected override string ListTablesSql => "SELECT table_schema || '.' || table_name FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog','information_schema') ORDER BY 1";

    public override string QuoteIdentifier(string identifier) =>
        "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}

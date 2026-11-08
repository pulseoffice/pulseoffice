using System.Data.Common;
using System.Data.Odbc;
using PulseOffice.Data;

namespace PulseOffice.Data.Odbc;

public sealed class OdbcDatabaseProvider(ICredentialStore credentials) : AdoDatabaseProvider(credentials)
{
    public override DatabaseProviderKind Kind => DatabaseProviderKind.Odbc;
    public override string DisplayName => "ODBC (generico)";

    protected override DbConnection CreateConnection(string connectionString) => new OdbcConnection(connectionString);

    protected override string ApplyCredentials(string connectionString, string? userName, string? secret)
    {
        var b = new OdbcConnectionStringBuilder(connectionString);
        if (userName is not null) b["UID"] = userName;
        if (secret is not null) b["PWD"] = secret;
        return b.ConnectionString;
    }

    protected override async Task<DbTransaction> BeginReadOnlyAsync(DbConnection connection, CancellationToken ct) =>
        await connection.BeginTransactionAsync(ct);

    protected override string ListTablesSql => "SELECT 1";

    protected override bool ListsSchemaQualifiedTables => false;

    public override string QuoteIdentifier(string identifier) =>
        "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    public override async Task<IReadOnlyList<string>> ListTablesAsync(
    ConnectionDescriptor connection, CancellationToken ct)
    {
        var secret = await SecretAsync(connection, ct);

        await using var db = new OdbcConnection(
            ApplyCredentials(connection.ConnectionString, connection.UserName, secret));
        await db.OpenAsync(ct);

        var schema = await db.GetSchemaAsync("Tables", ct);
        return [.. schema.Rows.Cast<System.Data.DataRow>()
            .Select(r => r["TABLE_NAME"]?.ToString() ?? string.Empty)
            .Where(s => s.Length > 0)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)];
    }
}

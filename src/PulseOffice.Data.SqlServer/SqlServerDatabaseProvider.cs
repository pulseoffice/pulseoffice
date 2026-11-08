using System.Data.Common;
using Microsoft.Data.SqlClient;
using PulseOffice.Data;

namespace PulseOffice.Data.SqlServer;

public sealed class SqlServerDatabaseProvider(ICredentialStore credentials) : AdoDatabaseProvider(credentials)
{
    public override DatabaseProviderKind Kind => DatabaseProviderKind.SqlServer;
    public override string DisplayName => "Microsoft SQL Server";

    protected override DbConnection CreateConnection(string connectionString) => new SqlConnection(connectionString);

    protected override string ApplyCredentials(string connectionString, string? userName, string? secret)
    {
        var b = new SqlConnectionStringBuilder(connectionString);
        if (userName is null) return b.ConnectionString;
        b.UserID = userName;
        b.Password = secret ?? string.Empty;
        return b.ConnectionString;
    }

    protected override string ReadOnlyConnectionString(string connectionString) =>
        new SqlConnectionStringBuilder(connectionString) { ApplicationIntent = ApplicationIntent.ReadOnly }.ConnectionString;

    protected override async Task<DbTransaction> BeginReadOnlyAsync(DbConnection connection, CancellationToken ct) =>
        await connection.BeginTransactionAsync(ct);

    protected override string ListTablesSql => "SELECT TABLE_SCHEMA + '.' + TABLE_NAME FROM INFORMATION_SCHEMA.TABLES ORDER BY TABLE_SCHEMA, TABLE_NAME";

    public override string QuoteIdentifier(string identifier) =>
        "[" + identifier.Replace("]", "]]", StringComparison.Ordinal) + "]";
}

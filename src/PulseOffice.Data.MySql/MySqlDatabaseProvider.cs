using System.Data;
using System.Data.Common;
using MySqlConnector;
using PulseOffice.Data;

namespace PulseOffice.Data.MySql;

public sealed class MySqlDatabaseProvider(ICredentialStore credentials) : AdoDatabaseProvider(credentials)
{
    public override DatabaseProviderKind Kind => DatabaseProviderKind.MySql;
    public override string DisplayName => "MySQL / MariaDB";

    protected override DbConnection CreateConnection(string connectionString) => new MySqlConnection(connectionString);

    protected override string ApplyCredentials(string connectionString, string? userName, string? secret)
    {
        var b = new MySqlConnectionStringBuilder(connectionString);
        if (userName is not null) b.UserID = userName;
        if (secret is not null) b.Password = secret;
        return b.ConnectionString;
    }

    protected override async Task<DbTransaction> BeginReadOnlyAsync(DbConnection connection, CancellationToken ct) =>
        await ((MySqlConnection)connection).BeginTransactionAsync(IsolationLevel.RepeatableRead, isReadOnly: true, ct);

    protected override string ListTablesSql => "SELECT CONCAT(table_schema,'.',table_name) FROM information_schema.tables WHERE table_schema NOT IN ('mysql','performance_schema','information_schema','sys') ORDER BY 1";

    public override string QuoteIdentifier(string identifier) =>
        "`" + identifier.Replace("`", "``", StringComparison.Ordinal) + "`";
}

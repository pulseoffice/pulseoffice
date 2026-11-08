using Xunit;
using System.Net.Sockets;
using PulseOffice.Data;

namespace PulseOffice.Data.Tests;

public sealed class NoCredentialStore : ICredentialStore
{
    public Task<string?> GetSecretAsync(string credentialKey, CancellationToken ct) =>
        Task.FromResult<string?>(null);

    public Task SetSecretAsync(string credentialKey, string secret, CancellationToken ct) =>
        Task.CompletedTask;

    public Task RemoveAsync(string credentialKey, CancellationToken ct) => Task.CompletedTask;
}

public sealed record LiveServer(
    string Name,
    DatabaseProviderKind Kind,
    int Port,
    string ConnectionString,
    string ProbeSql,
    string SampleTable,
    string ParameterPrefix,
    string TopRowsSql);

public static class LiveServers
{
    private const string Password = "GoGoSql2026";
    private const string Host = "127.0.0.1";

    public static readonly LiveServer PostgreSql = new(
        "PostgreSQL", DatabaseProviderKind.PostgreSql, 5432,
        $"Host={Host};Port=5432;Database=shop;Username=gogosql;Password={Password}",
        "SELECT 1 AS uno", "customers", "@", "SELECT * FROM customers LIMIT 50");

    public static readonly LiveServer MySql = new(
        "MySQL", DatabaseProviderKind.MySql, 3306,
        $"Server={Host};Port=3306;Database=shop;User ID=gogosql;Password={Password}",
        "SELECT 1 AS uno", "customers", "@", "SELECT * FROM customers LIMIT 50");

    public static readonly LiveServer MariaDb = new(
        "MariaDB", DatabaseProviderKind.MySql, 3307,
        $"Server={Host};Port=3307;Database=shop;User ID=gogosql;Password={Password}",
        "SELECT 1 AS uno", "customers", "@", "SELECT * FROM customers LIMIT 50");

    public static readonly LiveServer SqlServer = new(
        "SQL Server", DatabaseProviderKind.SqlServer, 1433,
        $"Server={Host},1433;Database=shop;User ID=sa;Password={Password};TrustServerCertificate=True",
        "SELECT 1 AS uno", "customers", "@", "SELECT TOP 50 * FROM customers");

    public static readonly IReadOnlyList<LiveServer> All =
        [PostgreSql, MySql, MariaDb, SqlServer];

    public static bool IsReachable(LiveServer server)
    {
        ArgumentNullException.ThrowIfNull(server);

        try
        {
            using var socket = new TcpClient();

            return socket.ConnectAsync(Host, server.Port).Wait(TimeSpan.FromSeconds(1))
                && socket.Connected;
        }
        catch (Exception failure) when (
            failure is SocketException
            || failure is AggregateException { InnerException: SocketException })
        {
            return false;
        }
    }

    public static ConnectionDescriptor Descriptor(LiveServer server) => new()
    {
        Id = server.Name,
        DisplayName = server.Name,
        Provider = server.Kind,
        ConnectionString = server.ConnectionString,
        CommandTimeout = TimeSpan.FromSeconds(20),
    };

    public static IDatabaseProvider Provider(LiveServer server) => server.Kind switch
    {
        DatabaseProviderKind.PostgreSql => new PostgreSql.PostgreSqlDatabaseProvider(new NoCredentialStore()),
        DatabaseProviderKind.MySql => new MySql.MySqlDatabaseProvider(new NoCredentialStore()),
        DatabaseProviderKind.SqlServer => new SqlServer.SqlServerDatabaseProvider(new NoCredentialStore()),
        _ => new Sqlite.SqliteDatabaseProvider(new NoCredentialStore()),
    };
}

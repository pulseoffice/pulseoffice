using Microsoft.Data.Sqlite;
using PulseOffice.Data.Sqlite;
using Xunit;

namespace PulseOffice.Data.Tests;

public sealed class ReadOnlyExecutionTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pulseoffice-readonly-{Guid.NewGuid():N}.db");
    private readonly SqliteDatabaseProvider _provider = new(new NoCredentialStore());
    private readonly ConnectionDescriptor _connection;

    public ReadOnlyExecutionTests()
    {
        _connection = new ConnectionDescriptor
        {
            Id = "shop",
            DisplayName = "Shop",
            Provider = DatabaseProviderKind.Sqlite,
            ConnectionString = $"Data Source={_file}",
        };
        using var db = new SqliteConnection(_connection.ConnectionString);
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            CREATE TABLE items (name TEXT);
            CREATE TABLE audit (note TEXT);
            INSERT INTO items VALUES ('pen');
            CREATE TRIGGER items_audit AFTER INSERT ON items BEGIN INSERT INTO audit VALUES ('touched'); END;
            """;
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_file)) File.Delete(_file);
    }

    private long Count(string table)
    {
        using var db = new SqliteConnection(_connection.ConnectionString);
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table}";
        return (long)command.ExecuteScalar()!;
    }

    [Fact]
    public async Task Reading_still_works()
    {
        var result = await _provider.ExecuteAsync(
            _connection, new QueryDefinition { Sql = "SELECT name FROM items" }, CancellationToken.None);

        Assert.Equal("pen", result.Rows.Single()[0]);
    }

    [Theory]
    [InlineData("INSERT INTO items VALUES ('ink') RETURNING name")]
    [InlineData("UPDATE items SET name = 'x' RETURNING name")]
    [InlineData("DELETE FROM items RETURNING name")]
    [InlineData("WITH n AS (SELECT 1) INSERT INTO items SELECT 'ink' FROM n RETURNING name")]
    public async Task A_statement_that_writes_fails_and_leaves_the_database_untouched(string sql)
    {
        await Assert.ThrowsAsync<SqliteException>(() =>
            _provider.ExecuteAsync(_connection, new QueryDefinition { Sql = sql }, CancellationToken.None));

        Assert.Equal(1, Count("items"));
        Assert.Equal(0, Count("audit"));
    }
}

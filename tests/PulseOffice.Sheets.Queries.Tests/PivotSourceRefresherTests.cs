using Microsoft.Data.Sqlite;
using PulseOffice.Data;
using PulseOffice.Data.Sqlite;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.Queries;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Queries.Tests;

public class PivotSourceRefresherTests : IDisposable
{
    private sealed class NoCredentials : ICredentialStore
    {
        public Task<string?> GetSecretAsync(string credentialKey, CancellationToken ct) => Task.FromResult<string?>(null);

        public Task SetSecretAsync(string credentialKey, string secret, CancellationToken ct) => Task.CompletedTask;

        public Task RemoveAsync(string credentialKey, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class Directory(ConnectionDescriptor connection) : IConnectionDirectory
    {
        public IReadOnlyList<ConnectionDescriptor> All => [connection];

        public ConnectionDescriptor? Find(string id) => id == connection.Id ? connection : null;

        public Task SaveAsync(ConnectionDescriptor descriptor, string? secret, CancellationToken ct = default) => Task.CompletedTask;

        public Task RemoveAsync(string id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pulseoffice-pivot-{Guid.NewGuid():N}.db");
    private readonly ConnectionDescriptor _connection;
    private readonly DatabaseProviderRegistry _providers = new();

    public PivotSourceRefresherTests()
    {
        _connection = new ConnectionDescriptor
        {
            Id = "sales-db",
            DisplayName = "Sales",
            Provider = DatabaseProviderKind.Sqlite,
            ConnectionString = $"Data Source={_file}",
        };
        _providers.Register(new SqliteDatabaseProvider(new NoCredentials()));

        using var connection = new SqliteConnection(_connection.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE sales (region TEXT, amount REAL);
            INSERT INTO sales VALUES ('North', 4);
            INSERT INTO sales VALUES ('South', 6);
            INSERT INTO sales VALUES ('North', 5);
            """;
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_file)) File.Delete(_file);
        GC.SuppressFinalize(this);
    }

    private PivotSourceRefresher Refresher() => new(_providers, new Directory(_connection), new RecordingQueryGate(allow: true));

    private Workbook WorkbookWith(PivotExternalSource source)
    {
        var workbook = Workbook.CreateDefault();
        workbook.Pivots.Add(new PivotDefinition
        {
            Id = "p",
            Name = "Summary",
            SourceSheet = string.Empty,
            SourceRange = default,
            ExternalSource = source,
            DestinationSheet = workbook.Sheets[0].Name,
            Destination = CellAddress.First,
            Fields =
            [
                new PivotField { SourceName = "amount", SourceColumnIndex = 0, Area = PivotArea.Value },
                new PivotField { SourceName = "region", SourceColumnIndex = 1, Area = PivotArea.Row },
            ],
        });
        return workbook;
    }

    [Fact]
    public async Task ATableSourceIsReadAgainThroughTheConnectionNamedByTheDocument()
    {
        var workbook = WorkbookWith(new PivotExternalSource { Kind = PivotExternalKind.Table, DatabaseName = "Sales", Command = "sales" });
        var refresher = Refresher();

        var fetch = await refresher.FetchAsync(workbook, TestContext.Current.CancellationToken);
        Assert.Equal(1, refresher.Apply(workbook, fetch));

        var pivot = Assert.Single(workbook.Pivots);
        Assert.Equal(["region", "amount"], pivot.ExternalSource!.Cache.Headers);
        Assert.Equal(3, pivot.ExternalSource.Cache.Records.Count);
        Assert.Equal(CellValue.Text("South"), pivot.ExternalSource.Cache.Records[1][0]);
        Assert.Equal(1, pivot.Fields.Single(field => field.SourceName == "amount").SourceColumnIndex);
        Assert.Equal(0, pivot.Fields.Single(field => field.SourceName == "region").SourceColumnIndex);
    }

    [Fact]
    public async Task ASqlSourceFindsItsConnectionById()
    {
        var workbook = WorkbookWith(new PivotExternalSource { Kind = PivotExternalKind.Sql, ConnectionId = "sales-db", Command = "SELECT region, amount FROM sales WHERE amount > 4" });
        var refresher = Refresher();

        refresher.Apply(workbook, await refresher.FetchAsync(workbook, TestContext.Current.CancellationToken));

        Assert.Equal(2, workbook.Pivots[0].ExternalSource!.Cache.Records.Count);
    }

    [Fact]
    public async Task AnUnknownConnectionKeepsTheCachedRecords()
    {
        var cache = new PivotRecordCache { Headers = ["region", "amount"], Records = [[CellValue.Text("East"), CellValue.Number(1)]] };
        var workbook = WorkbookWith(new PivotExternalSource { Kind = PivotExternalKind.Query, DatabaseName = "Archive", Command = "Q", Cache = cache });
        var refresher = Refresher();

        var fetch = await refresher.FetchAsync(workbook, TestContext.Current.CancellationToken);

        Assert.Equal(0, refresher.Apply(workbook, fetch));
        Assert.Equal("PVT011", Assert.Single(fetch.Failures).Code);
        Assert.Same(cache, workbook.Pivots[0].ExternalSource!.Cache);
    }

    [Fact]
    public async Task AWritingStatementIsNeverRun()
    {
        var workbook = WorkbookWith(new PivotExternalSource { Kind = PivotExternalKind.Sql, DatabaseName = "Sales", Command = "DELETE FROM sales" });
        var refresher = Refresher();

        var fetch = await refresher.FetchAsync(workbook, TestContext.Current.CancellationToken);

        Assert.Equal("PVT014", Assert.Single(fetch.Failures).Code);
        using var connection = new SqliteConnection(_connection.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sales";
        Assert.Equal(3L, command.ExecuteScalar());
    }

    [Fact]
    public async Task ServicesAndOtherWorkbooksAreNotQueried()
    {
        var workbook = WorkbookWith(new PivotExternalSource { Kind = PivotExternalKind.Workbook, DatabaseName = "file:///data.xlsx" });

        var fetch = await Refresher().FetchAsync(workbook, TestContext.Current.CancellationToken);

        Assert.Equal("PVT010", Assert.Single(fetch.Failures).Code);
    }

    [Fact]
    public async Task TheDocumentsQueriesAreShownToTheGateWithTheirConnection()
    {
        var workbook = WorkbookWith(new PivotExternalSource { Kind = PivotExternalKind.Table, DatabaseName = "Sales", Command = "sales" });
        var gate = new RecordingQueryGate(allow: true);

        await new PivotSourceRefresher(_providers, new Directory(_connection), gate).FetchAsync(workbook, TestContext.Current.CancellationToken);

        var (document, requests) = Assert.Single(gate.Asked);
        Assert.Same(workbook, document);
        Assert.Equal(new ExternalQueryRequest("sales-db", "SELECT * FROM \"sales\""), Assert.Single(requests));
    }

    [Fact]
    public async Task ADeclinedQueryIsNotRunAndKeepsTheCachedRecords()
    {
        var cache = new PivotRecordCache { Headers = ["region", "amount"], Records = [[CellValue.Text("East"), CellValue.Number(1)]] };
        var workbook = WorkbookWith(new PivotExternalSource { Kind = PivotExternalKind.Sql, DatabaseName = "Sales", Command = "SELECT region, amount FROM sales", Cache = cache });
        var refresher = new PivotSourceRefresher(_providers, new Directory(_connection), new RecordingQueryGate(allow: false));

        var fetch = await refresher.FetchAsync(workbook, TestContext.Current.CancellationToken);

        Assert.Equal(0, refresher.Apply(workbook, fetch));
        Assert.Equal("PVT015", Assert.Single(fetch.Failures).Code);
        Assert.Same(cache, workbook.Pivots[0].ExternalSource!.Cache);
    }

    [Fact]
    public void WithoutAConsentGateThePivotRefresherRefusesToExist() =>
        Assert.Throws<ArgumentNullException>(() => new PivotSourceRefresher(_providers, new Directory(_connection), null!));
}

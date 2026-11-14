using PulseOffice.Data;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Queries;
using Xunit;

namespace PulseOffice.Data.Tests;

public sealed class QueryToSheetTests
{
    private sealed class OneConnection(ConnectionDescriptor connection) : IConnectionDirectory
    {
        public IReadOnlyList<ConnectionDescriptor> All => [connection];

        public ConnectionDescriptor? Find(string id) => id == connection.Id ? connection : null;

        public Task SaveAsync(ConnectionDescriptor descriptor, string? secret, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(string id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    [Fact]
    public async Task IDatiDiPostgresArrivanoNelFoglioConIlFormatoData()
    {
        var server = LiveServers.PostgreSql;
        Assert.SkipUnless(LiveServers.IsReachable(server), "PostgreSQL non è in ascolto.");

        var descriptor = LiveServers.Descriptor(server);
        var registry = new DatabaseProviderRegistry();
        registry.Register(LiveServers.Provider(server));

        var refresher = new QueryRefresher(registry, new OneConnection(descriptor), new ConsentingQueryGate());

        var book = new Workbook();
        var sheet = book.AddSheet("Clienti");

        var fetched = await refresher.FetchAsync(
            book,
            sheet,
            new QueryBinding
            {
                Id = "clienti",
                DisplayName = "Clienti",
                ConnectionId = descriptor.Id,
                Query = new QueryDefinition
                {
                    Sql = "SELECT first_name, birth_date FROM public.customers ORDER BY id LIMIT 5",
                },
                Anchor = A("A1"),
            },
            TestContext.Current.CancellationToken);
        var outcome = refresher.Write(book, fetched);

        Assert.True(outcome.Succeeded, string.Join("; ", outcome.Diagnostics.Select(d => d.Message)));

        Assert.Equal("first_name", sheet[A("A1")].Value.ToString());
        Assert.Equal(14, book.Styles.Get(sheet[A("B2")].StyleId).NumberFormatId);
        Assert.Equal(6, outcome.Value!.Range.RowCount);
    }
}

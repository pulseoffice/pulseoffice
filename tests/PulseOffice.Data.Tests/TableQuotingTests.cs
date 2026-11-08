using Xunit;
using PulseOffice.Data.PostgreSql;
using PulseOffice.Data.Sqlite;

namespace PulseOffice.Data.Tests;

public sealed class TableQuotingTests
{
    [Fact]
    public void UnaTabellaSqliteConIlPuntoNelNomeRestaUnIdentificatoreSolo() =>
        Assert.Equal("\"vendite.2024\"", new SqliteDatabaseProvider(new NoCredentialStore()).QuoteListedTable("vendite.2024"));

    [Fact]
    public void SchemaETabellaSiDividonoSoloAlPrimoPunto() =>
        Assert.Equal("\"shop\".\"ordini.vecchi\"", new PostgreSqlDatabaseProvider(new NoCredentialStore()).QuoteListedTable("shop.ordini.vecchi"));
}

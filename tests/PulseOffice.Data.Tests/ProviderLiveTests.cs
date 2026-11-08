using Xunit;
using PulseOffice.Data;

namespace PulseOffice.Data.Tests;

public sealed class ProviderLiveTests
{
    public static TheoryData<string> Servers()
    {
        var data = new TheoryData<string>();
        foreach (var server in LiveServers.All) data.Add(server.Name);
        return data;
    }

    private static LiveServer Server(string name) =>
        LiveServers.All.Single(s => s.Name == name);

    [Theory]
    [MemberData(nameof(Servers))]
    public async Task La_connessione_riesce(string name)
    {
        var server = Require(name);
        var provider = LiveServers.Provider(server);

        Assert.True(await provider.TestConnectionAsync(
            LiveServers.Descriptor(server), TestContext.Current.CancellationToken));
    }

    [Theory]
    [MemberData(nameof(Servers))]
    public async Task Le_credenziali_sbagliate_non_aprono_la_connessione(string name)
    {
        var server = Require(name);
        var provider = LiveServers.Provider(server);
        var descriptor = LiveServers.Descriptor(server) with
        {
            ConnectionString = server.ConnectionString.Replace(
                "GoGoSql2026", "NonSonoLaPassword", StringComparison.Ordinal),
        };

        Assert.False(await provider.TestConnectionAsync(descriptor, TestContext.Current.CancellationToken));
    }

    [Theory]
    [MemberData(nameof(Servers))]
    public async Task Le_tabelle_elencate_sono_quelle_dell_utente(string name)
    {
        var server = Require(name);
        var provider = LiveServers.Provider(server);

        var tables = await provider.ListTablesAsync(
            LiveServers.Descriptor(server), TestContext.Current.CancellationToken);

        Assert.NotEmpty(tables);
        Assert.InRange(tables.Count, 1, 200);
        Assert.Contains(tables, t => t.Contains("ustomer", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [MemberData(nameof(Servers))]
    public async Task Una_query_restituisce_colonne_tipizzate(string name)
    {
        var server = Require(name);
        var provider = LiveServers.Provider(server);

        var result = await provider.ExecuteAsync(
            LiveServers.Descriptor(server),
            new QueryDefinition { Sql = server.ProbeSql },
            TestContext.Current.CancellationToken);

        var row = Assert.Single(result.Rows);
        var column = Assert.Single(result.Columns);
        Assert.Equal("uno", column.Name, ignoreCase: true);
        Assert.Equal(1m, Convert.ToDecimal(row[0], System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [MemberData(nameof(Servers))]
    public async Task Un_parametro_viene_passato_e_non_interpolato(string name)
    {
        var server = Require(name);
        var provider = LiveServers.Provider(server);

        var result = await provider.ExecuteAsync(
            LiveServers.Descriptor(server),
            new QueryDefinition
            {
                Sql = $"SELECT {server.ParameterPrefix}valore AS eco",
                Parameters = new Dictionary<string, object?> { ["valore"] = "Rossi'; DROP TABLE customers--" },
            },
            TestContext.Current.CancellationToken);

        Assert.Equal("Rossi'; DROP TABLE customers--", Assert.Single(result.Rows)[0]);
    }

    [Theory]
    [MemberData(nameof(Servers))]
    public async Task Il_limite_di_righe_tronca_e_lo_dichiara(string name)
    {
        var server = Require(name);
        var provider = LiveServers.Provider(server);

        var result = await provider.ExecuteAsync(
            LiveServers.Descriptor(server),
            new QueryDefinition { Sql = server.TopRowsSql, MaxRows = 2 },
            TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Rows.Count);
        Assert.True(result.Truncated);
    }

    private static LiveServer Require(string name)
    {
        var server = Server(name);
        Assert.SkipUnless(LiveServers.IsReachable(server), $"{server.Name} non è in ascolto sulla porta {server.Port}.");
        return server;
    }
}

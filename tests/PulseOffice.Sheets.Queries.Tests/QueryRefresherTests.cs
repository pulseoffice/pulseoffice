using Microsoft.Data.Sqlite;
using PulseOffice.Data;
using PulseOffice.Data.Sqlite;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Queries;
using Xunit;

namespace PulseOffice.Sheets.Queries.Tests;

public class QueryRefresherTests : IDisposable
{
    private sealed class NoCredentials : ICredentialStore
    {
        public Task<string?> GetSecretAsync(string credentialKey, CancellationToken ct) =>
            Task.FromResult<string?>(null);

        public Task SetSecretAsync(string credentialKey, string secret, CancellationToken ct) =>
            Task.CompletedTask;

        public Task RemoveAsync(string credentialKey, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class Directory(ConnectionDescriptor connection) : IConnectionDirectory
    {
        public IReadOnlyList<ConnectionDescriptor> All => [connection];

        public ConnectionDescriptor? Find(string id) => id == connection.Id ? connection : null;

        public Task SaveAsync(ConnectionDescriptor descriptor, string? secret, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(string id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"pulseoffice-prova-{Guid.NewGuid():N}.db");

    private readonly ConnectionDescriptor _connection;
    private readonly DatabaseProviderRegistry _providers = new();

    public QueryRefresherTests()
    {
        _connection = new ConnectionDescriptor
        {
            Id = "prova",
            DisplayName = "Prova",
            Provider = DatabaseProviderKind.Sqlite,
            ConnectionString = $"Data Source={_file}",
        };

        _providers.Register(new SqliteDatabaseProvider(new NoCredentials()));

        using var connection = new SqliteConnection(_connection.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            CREATE TABLE vendite (zona TEXT, mese INTEGER, totale REAL, attiva INTEGER);
            INSERT INTO vendite VALUES ('Nord', 1, 1250.5, 1);
            INSERT INTO vendite VALUES ('Sud', 1, 890.0, 0);
            INSERT INTO vendite VALUES ('Nord', 2, 1310.25, 1);
            """;

        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_file)) File.Delete(_file);
        GC.SuppressFinalize(this);
    }

    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static async Task<PulseOffice.Foundation.Diagnostics.OperationResult<QueryBinding>> Refresh(
        IQueryRefresher refresher, Workbook book, Worksheet sheet, QueryBinding binding, CancellationToken ct) =>
        refresher.Write(book, await refresher.FetchAsync(book, sheet, binding, ct));

    [Fact]
    public async Task Fetching_reads_the_database_without_touching_the_sheet_and_only_writing_fills_it()
    {
        var book = new Workbook();
        var sheet = book.AddSheet("Dati");
        var refresher = Refresher();

        var fetched = await refresher.FetchAsync(book, sheet, Binding("SELECT zona FROM vendite"), TestContext.Current.CancellationToken);

        Assert.Empty(sheet.CellAddresses);
        Assert.NotNull(fetched.Result);
        Assert.True(refresher.Write(book, fetched).Succeeded);
        Assert.Equal("zona", sheet[A("A1")].Value.ToString());
    }

    [Fact]
    public async Task A_sheet_deleted_while_the_query_runs_is_not_written()
    {
        var book = new Workbook();
        var sheet = book.AddSheet("Dati");
        book.AddSheet("Altro");
        var refresher = Refresher();

        var fetched = await refresher.FetchAsync(book, sheet, Binding("SELECT zona FROM vendite"), TestContext.Current.CancellationToken);
        book.RemoveSheet(sheet);
        var outcome = refresher.Write(book, fetched);

        Assert.False(outcome.Succeeded);
        Assert.Equal("QRY007", outcome.Diagnostics[0].Code);
        Assert.Empty(sheet.CellAddresses);
    }

    private IQueryRefresher Refresher() => new QueryRefresher(_providers, new Directory(_connection), new RecordingQueryGate(allow: true));

    private QueryBinding Binding(string sql, bool headers = true) => new()
    {
        Id = "q1",
        DisplayName = "Vendite",
        ConnectionId = "prova",
        Query = new QueryDefinition { Sql = sql },
        Anchor = A("A1"),
        IncludeHeaders = headers,
    };

    [Theory]
    [InlineData("DELETE FROM vendite")]
    [InlineData("SELECT 1; DROP TABLE vendite")]
    [InlineData("ATTACH DATABASE 'altro.db' AS altro")]
    public async Task UnaQueryDelDocumentoCheScriveNonSiEsegue(string sql)
    {
        var book = new Workbook();
        var sheet = book.AddSheet("Dati");

        var outcome = await Refresh(Refresher(), book, sheet, Binding(sql), TestContext.Current.CancellationToken);

        Assert.False(outcome.Succeeded);
        Assert.Equal("QRY004", outcome.Diagnostics[0].Code);

        using var connection = new SqliteConnection(_connection.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM vendite";
        Assert.Equal(3L, (long)command.ExecuteScalar()!);
    }

    [Fact]
    public async Task ResultAtTheBottomOfTheSheetIsTruncatedWithAWarning()
    {
        var book = new Workbook();
        var sheet = book.AddSheet("Dati");
        var binding = Binding("SELECT zona, totale FROM vendite ORDER BY totale") with { Anchor = new CellAddress(CellAddress.MaxRows - 2, 0) };

        var outcome = await Refresh(Refresher(), book, sheet, binding, TestContext.Current.CancellationToken);

        Assert.True(outcome.Succeeded);
        Assert.Contains(outcome.Diagnostics, diagnostic => diagnostic.Code == "QRY005");
        Assert.True(outcome.Value!.Range.End.IsValid);
        Assert.All(sheet.CellAddresses, address => Assert.True(address.IsValid));
        Assert.Equal("Sud", sheet[new CellAddress(CellAddress.MaxRows - 1, 0)].Value.ToString());
    }

    [Fact]
    public async Task IlRisultatoFinisceNelFoglio()
    {
        var book = new Workbook();
        var sheet = book.AddSheet("Dati");

        var outcome = await Refresh(Refresher(), 
            book, sheet, Binding("SELECT zona, totale FROM vendite ORDER BY totale"),
            TestContext.Current.CancellationToken);

        Assert.True(outcome.Succeeded, string.Join("; ", outcome.Diagnostics.Select(d => d.Message)));

        Assert.Equal("zona", sheet[A("A1")].Value.ToString());
        Assert.Equal("totale", sheet[A("B1")].Value.ToString());
        Assert.Equal("Sud", sheet[A("A2")].Value.ToString());
        Assert.Equal(890, sheet[A("B2")].Value.AsNumber());
        Assert.Equal(1310.25, sheet[A("B4")].Value.AsNumber(), 2);
    }

    [Fact]
    public async Task SenzaIntestazioniLeRighePartonoDallAncora()
    {
        var book = new Workbook();
        var sheet = book.AddSheet("Dati");

        await Refresh(Refresher(), 
            book, sheet, Binding("SELECT zona FROM vendite ORDER BY zona", headers: false),
            TestContext.Current.CancellationToken);

        Assert.Equal("Nord", sheet[A("A1")].Value.ToString());
    }

    [Fact]
    public async Task LIntervalloScrittoTornaNelLegame()
    {
        var book = new Workbook();
        var sheet = book.AddSheet("Dati");

        var outcome = await Refresh(Refresher(), 
            book, sheet, Binding("SELECT zona, mese, totale FROM vendite"),
            TestContext.Current.CancellationToken);

        var binding = outcome.Value!;

        Assert.Equal(A("A1"), binding.Range.Start);
        Assert.Equal(A("C4"), binding.Range.End);
        Assert.Equal(3, binding.LastRowCount);
        Assert.NotNull(binding.LastRefresh);
    }

    [Fact]
    public async Task UnSecondoGiroPiuCortoPuliscLeRigheVecchie()
    {
        var book = new Workbook();
        var sheet = book.AddSheet("Dati");
        var refresher = Refresher();

        var first = await Refresh(refresher, 
            book, sheet, Binding("SELECT zona FROM vendite"), TestContext.Current.CancellationToken);

        var second = await Refresh(refresher, 
            book, sheet, first.Value! with { Query = new QueryDefinition { Sql = "SELECT zona FROM vendite WHERE zona = 'Sud'" } },
            TestContext.Current.CancellationToken);

        Assert.True(second.Succeeded);
        Assert.Equal("Sud", sheet[A("A2")].Value.ToString());
        Assert.True(sheet[A("A3")].Value.IsEmpty);
        Assert.True(sheet[A("A4")].Value.IsEmpty);
    }

    [Fact]
    public async Task IBooleaniEINumeriArrivanoTipizzati()
    {
        var book = new Workbook();
        var sheet = book.AddSheet("Dati");

        await Refresh(Refresher(), 
            book, sheet, Binding("SELECT attiva, mese FROM vendite ORDER BY mese, zona"),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, sheet[A("A2")].Value.AsNumber());
        Assert.Equal(1, sheet[A("B2")].Value.AsNumber());
    }

    [Fact]
    public async Task UnaConnessioneSconosciutaFallisceSenzaScrivere()
    {
        var book = new Workbook();
        var sheet = book.AddSheet("Dati");

        var outcome = await Refresh(Refresher(), 
            book, sheet, Binding("SELECT 1") with { ConnectionId = "assente" },
            TestContext.Current.CancellationToken);

        Assert.False(outcome.Succeeded);
        Assert.Equal(0, sheet.CellCount);
    }

    [Fact]
    public async Task UnaQueryRottaRiportaLErroreSenzaCadere()
    {
        var book = new Workbook();
        var sheet = book.AddSheet("Dati");

        var outcome = await Refresh(Refresher(), 
            book, sheet, Binding("SELECT * FROM tabella_che_non_esiste"),
            TestContext.Current.CancellationToken);

        Assert.False(outcome.Succeeded);
        Assert.NotEmpty(outcome.Diagnostics);
    }

    [Fact]
    public async Task IlGiroCompletoAggiornaTuttiILegamiDelDocumento()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");

        sheet.QueryBindings.AddOrUpdate(Binding("SELECT zona FROM vendite"));

        var refresher = Refresher();
        var outcome = refresher.Apply(workbook, await refresher.FetchAllAsync(workbook, TestContext.Current.CancellationToken));

        Assert.Equal(1, outcome.Value);
        Assert.Equal("zona", sheet[A("A1")].Value.ToString());
        Assert.NotNull(sheet.QueryBindings.Find("q1")!.LastRefresh);
    }

    [Fact]
    public async Task LeTabelleSiElencanoDavvero()
    {
        var provider = new SqliteDatabaseProvider(new NoCredentials());

        var tables = await provider.ListTablesAsync(_connection, TestContext.Current.CancellationToken);

        Assert.Contains("vendite", tables);
    }

    [Fact]
    public async Task ADeclinedDocumentQueryLeavesTheSheetAndTheBindingUntouched()
    {
        var book = new Workbook();
        var sheet = book.AddSheet("Dati");
        var binding = Binding("SELECT zona FROM vendite");
        sheet.QueryBindings.AddOrUpdate(binding);
        var gate = new RecordingQueryGate(allow: false);
        var refresher = new QueryRefresher(_providers, new Directory(_connection), gate);

        var outcome = refresher.Apply(book, await refresher.FetchAllAsync(book, TestContext.Current.CancellationToken));

        Assert.Equal(0, outcome.Value);
        Assert.Equal(QueryRefresher.DeclinedCode, Assert.Single(outcome.Diagnostics).Code);
        Assert.Empty(sheet.CellAddresses);
        Assert.Same(binding, sheet.QueryBindings.Find("q1"));
        Assert.Equal(new ExternalQueryRequest("prova", "SELECT zona FROM vendite"), Assert.Single(Assert.Single(gate.Asked).Requests));
    }
    [Fact]
    public void WithoutAConsentGateTheRefresherRefusesToExist() =>
        Assert.Throws<ArgumentNullException>(() => new QueryRefresher(_providers, new Directory(_connection), null!));
}

public class FileConnectionDirectoryTests : IDisposable
{
    private sealed class NoCredentials : ICredentialStore
    {
        public Task<string?> GetSecretAsync(string credentialKey, CancellationToken ct) =>
            Task.FromResult<string?>(null);

        public Task SetSecretAsync(string credentialKey, string secret, CancellationToken ct) =>
            Task.CompletedTask;

        public Task RemoveAsync(string credentialKey, CancellationToken ct) => Task.CompletedTask;
    }

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"connessioni-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void SenzaFileNonCiSonoConnessioni()
    {
        Assert.Empty(new FileConnectionDirectory(_path, new NoCredentials()).All);
    }

    [Fact]
    public void UnFileRottoNonFaCadereNulla()
    {
        File.WriteAllText(_path, "{non json");

        Assert.Empty(new FileConnectionDirectory(_path, new NoCredentials()).All);
    }

    [Fact]
    public void LeConnessioniSiLeggonoDalFile()
    {
        File.WriteAllText(_path, """
            [
              { "id": "locale", "nome": "Archivio", "provider": "Sqlite", "stringa": "Data Source=x.db" }
            ]
            """);

        var directory = new FileConnectionDirectory(_path, new NoCredentials());

        Assert.Single(directory.All);
        Assert.Equal(DatabaseProviderKind.Sqlite, directory.Find("locale")!.Provider);
    }

    [Fact]
    public void UnaVoceSenzaStringaDiConnessioneSiScarta()
    {
        File.WriteAllText(_path, """[{ "id": "vuota", "provider": "Sqlite" }]""");

        Assert.Empty(new FileConnectionDirectory(_path, new NoCredentials()).All);
    }

    [Fact]
    public void UnProviderSconosciutoSiScarta()
    {
        File.WriteAllText(_path, """[{ "id": "x", "provider": "Marte", "stringa": "y" }]""");

        Assert.Empty(new FileConnectionDirectory(_path, new NoCredentials()).All);
    }
}

public class ConnessioniScritteTests : IDisposable
{
    private sealed class MemoriaCredenziali : ICredentialStore
    {
        private readonly Dictionary<string, string> _secrets = [];

        public IReadOnlyDictionary<string, string> Secrets => _secrets;

        public Task<string?> GetSecretAsync(string credentialKey, CancellationToken ct) =>
            Task.FromResult(_secrets.GetValueOrDefault(credentialKey));

        public Task SetSecretAsync(string credentialKey, string secret, CancellationToken ct)
        {
            _secrets[credentialKey] = secret;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string credentialKey, CancellationToken ct)
        {
            _secrets.Remove(credentialKey);
            return Task.CompletedTask;
        }
    }

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"connessioni-{Guid.NewGuid():N}.json");

    private readonly MemoriaCredenziali _credentials = new();

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
        GC.SuppressFinalize(this);
    }

    private ConnectionDescriptor Descrittore() => new()
    {
        Id = "magazzino",
        DisplayName = "Magazzino",
        Provider = DatabaseProviderKind.PostgreSql,
        ConnectionString = "Host=127.0.0.1;Database=shop",
        UserName = "gogosql",
    };

    [Fact]
    public async Task UnaConnessioneSalvataSiRilegge()
    {
        var directory = new FileConnectionDirectory(_path, _credentials);
        await directory.SaveAsync(Descrittore(), "GoGoSql2026", TestContext.Current.CancellationToken);

        var riletta = new FileConnectionDirectory(_path, _credentials).Find("magazzino");

        Assert.NotNull(riletta);
        Assert.Equal("Magazzino", riletta.DisplayName);
        Assert.Equal(DatabaseProviderKind.PostgreSql, riletta.Provider);
    }

    [Fact]
    public async Task LaPasswordNonFinisceNelFile()
    {
        var directory = new FileConnectionDirectory(_path, _credentials);
        await directory.SaveAsync(Descrittore(), "GoGoSql2026", TestContext.Current.CancellationToken);

        var contenuto = await File.ReadAllTextAsync(_path, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("GoGoSql2026", contenuto, StringComparison.Ordinal);
        Assert.Equal("GoGoSql2026", _credentials.Secrets["magazzino"]);
    }

    [Fact]
    public async Task RimuovereUnaConnessioneRimuoveAncheIlSegreto()
    {
        var directory = new FileConnectionDirectory(_path, _credentials);
        await directory.SaveAsync(Descrittore(), "GoGoSql2026", TestContext.Current.CancellationToken);
        await directory.RemoveAsync("magazzino", TestContext.Current.CancellationToken);

        Assert.Null(directory.Find("magazzino"));
        Assert.Empty(_credentials.Secrets);
    }
}

public class ConnessioniInChiaroTests : IDisposable
{
    private sealed class MemoriaCredenziali : ICredentialStore
    {
        private readonly Dictionary<string, string> _secrets = [];

        public IReadOnlyDictionary<string, string> Secrets => _secrets;

        public Task<string?> GetSecretAsync(string credentialKey, CancellationToken ct) =>
            Task.FromResult(_secrets.GetValueOrDefault(credentialKey));

        public Task SetSecretAsync(string credentialKey, string secret, CancellationToken ct)
        {
            _secrets[credentialKey] = secret;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string credentialKey, CancellationToken ct)
        {
            _secrets.Remove(credentialKey);
            return Task.CompletedTask;
        }
    }

    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"connessioni-{Guid.NewGuid():N}.json");

    private readonly MemoriaCredenziali _credentials = new();

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
        GC.SuppressFinalize(this);
    }

    private static ConnectionDescriptor Descrittore(bool inFile) => new()
    {
        Id = "magazzino",
        DisplayName = "Magazzino",
        Provider = DatabaseProviderKind.PostgreSql,
        ConnectionString = "Host=127.0.0.1;Database=shop",
        UserName = "gogosql",
        SecretInFile = inFile,
    };

    [Fact]
    public async Task ScegliendoIlFileLaPasswordCiFinisceDavvero()
    {
        var directory = new FileConnectionDirectory(_path, _credentials);
        await directory.SaveAsync(Descrittore(inFile: true), "GoGoSql2026", TestContext.Current.CancellationToken);

        var contenuto = await File.ReadAllTextAsync(_path, TestContext.Current.CancellationToken);

        Assert.Contains("GoGoSql2026", contenuto, StringComparison.Ordinal);
        Assert.Empty(_credentials.Secrets);

        var riletta = new FileConnectionDirectory(_path, _credentials).Find("magazzino");

        Assert.True(riletta!.SecretInFile);
        Assert.Equal("GoGoSql2026", riletta.InsecureSecret);
    }

    [Fact]
    public async Task SalvandoDiNuovoSenzaRiscrivereLaPasswordNonLaPerde()
    {
        var directory = new FileConnectionDirectory(_path, _credentials);
        await directory.SaveAsync(Descrittore(inFile: true), "GoGoSql2026", TestContext.Current.CancellationToken);
        await directory.SaveAsync(Descrittore(inFile: true), null, TestContext.Current.CancellationToken);

        Assert.Equal("GoGoSql2026", directory.Find("magazzino")!.InsecureSecret);
    }

    [Fact]
    public async Task TogliendoLaSceltaLaPasswordTornaNelPortachiavi()
    {
        var directory = new FileConnectionDirectory(_path, _credentials);
        await directory.SaveAsync(Descrittore(inFile: true), "GoGoSql2026", TestContext.Current.CancellationToken);
        await directory.SaveAsync(Descrittore(inFile: false), "GoGoSql2026", TestContext.Current.CancellationToken);

        var contenuto = await File.ReadAllTextAsync(_path, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("GoGoSql2026", contenuto, StringComparison.Ordinal);
        Assert.Equal("GoGoSql2026", _credentials.Secrets["magazzino"]);
        Assert.Null(directory.Find("magazzino")!.InsecureSecret);
    }

}

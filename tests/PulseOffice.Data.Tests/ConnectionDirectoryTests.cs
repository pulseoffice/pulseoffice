using PulseOffice.Data;
using Xunit;

namespace PulseOffice.Data.Tests;

public class ConnectionDirectoryTests : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), "pulseoffice-connections-" + Guid.NewGuid().ToString("N") + ".json");

    private sealed class MemoryCredentials : ICredentialStore
    {
        private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);

        public Task<string?> GetSecretAsync(string key, CancellationToken ct = default) =>
            Task.FromResult(_secrets.GetValueOrDefault(key));

        public Task SetSecretAsync(string key, string secret, CancellationToken ct = default)
        {
            _secrets[key] = secret;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken ct = default)
        {
            _secrets.Remove(key);
            return Task.CompletedTask;
        }
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void UnNomeCheDaLoStessoIdentificativoNonSovrascriveUnaConnessioneEsistente()
    {
        var ids = new ConnectionIds();

        Assert.Equal("clienti-1", ids.NewId("Clienti 1", []));
        Assert.Equal("clienti-1-2", ids.NewId("clienti-1", ["clienti-1"]));
        Assert.Equal("clienti-1-3", ids.NewId("Clienti?1", ["clienti-1", "clienti-1-2"]));
        Assert.Equal("connection", ids.NewId("!!!", []));
    }

    [Fact]
    public async Task IlFileSalvatoUsaNomiInglesi()
    {
        var directory = new FileConnectionDirectory(_path, new MemoryCredentials());

        await directory.SaveAsync(
            new ConnectionDescriptor
            {
                Id = "negozio",
                DisplayName = "Negozio",
                Provider = DatabaseProviderKind.Sqlite,
                ConnectionString = "Data Source=negozio.db",
                UserName = "ada",
            },
            secret: null,
            TestContext.Current.CancellationToken);

        var written = await File.ReadAllTextAsync(_path, TestContext.Current.CancellationToken);

        Assert.Contains("\"name\"", written, StringComparison.Ordinal);
        Assert.Contains("\"connectionString\"", written, StringComparison.Ordinal);
        Assert.Contains("\"user\"", written, StringComparison.Ordinal);
        Assert.DoesNotContain("\"nome\"", written, StringComparison.Ordinal);
        Assert.DoesNotContain("\"stringa\"", written, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IlFileDelleConnessioniLoLeggeSoloLUtente()
    {
        if (OperatingSystem.IsWindows()) return;
        await File.WriteAllTextAsync(_path, "[]", TestContext.Current.CancellationToken);
        File.SetUnixFileMode(_path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        var directory = new FileConnectionDirectory(_path, new MemoryCredentials());

        await directory.SaveAsync(
            new ConnectionDescriptor
            {
                Id = "negozio",
                DisplayName = "Negozio",
                Provider = DatabaseProviderKind.PostgreSql,
                ConnectionString = "Host=db",
                SecretInFile = true,
            },
            secret: "segreto",
            TestContext.Current.CancellationToken);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(_path));
    }

    [Fact]
    public async Task The_connections_file_is_written_through_the_private_file_store()
    {
        var files = new RecordingPrivateFiles();
        var directory = new FileConnectionDirectory(_path, new MemoryCredentials(), privateFiles: files);

        await directory.SaveAsync(
            new ConnectionDescriptor
            {
                Id = "negozio",
                DisplayName = "Negozio",
                Provider = DatabaseProviderKind.PostgreSql,
                ConnectionString = "Host=db",
            },
            secret: null,
            TestContext.Current.CancellationToken);

        Assert.Equal([_path + ".tmp"], files.Opened);
        Assert.True(File.Exists(_path));
    }

    private sealed class RecordingPrivateFiles : PulseOffice.Platform.IPrivateFiles
    {
        public List<string> Opened { get; } = [];

        public void CreateFolder(string folder) => Directory.CreateDirectory(folder);

        public FileStream Open(string path, FileMode mode, FileShare share = FileShare.None)
        {
            Opened.Add(path);
            return new FileStream(path, mode, FileAccess.Write, share);
        }

        public void Restrict(string path)
        {
        }
    }

    [Fact]
    public async Task UnFileConIVecchiNomiItalianiSiLeggeAncora()
    {
        await File.WriteAllTextAsync(
            _path,
            """
            [{"id":"vecchia","nome":"Vecchia","provider":"Sqlite",
              "stringa":"Data Source=vecchia.db","utente":"ada","credenziale":"chiave"}]
            """,
            TestContext.Current.CancellationToken);

        var directory = new FileConnectionDirectory(_path, new MemoryCredentials());

        var connection = Assert.Single(directory.All);

        Assert.Equal("Vecchia", connection.DisplayName);
        Assert.Equal("Data Source=vecchia.db", connection.ConnectionString);
        Assert.Equal("ada", connection.UserName);
        Assert.Equal("chiave", connection.CredentialKey);
    }

    [Fact]
    public async Task LaPasswordNonEntraNelFileSeNonSiSceglie()
    {
        var directory = new FileConnectionDirectory(_path, new MemoryCredentials());

        await directory.SaveAsync(
            new ConnectionDescriptor
            {
                Id = "sicura",
                DisplayName = "Sicura",
                Provider = DatabaseProviderKind.PostgreSql,
                ConnectionString = "Host=localhost",
            },
            "segretissima",
            TestContext.Current.CancellationToken);

        var written = await File.ReadAllTextAsync(_path, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("segretissima", written, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LaPasswordScrittaNellaStringaVaNelPortachiavi()
    {
        var credentials = new MemoryCredentials();
        var directory = new FileConnectionDirectory(_path, credentials);

        await directory.SaveAsync(new ConnectionDescriptor
        {
            Id = "pg",
            DisplayName = "Pg",
            Provider = DatabaseProviderKind.PostgreSql,
            ConnectionString = "Host=db;Username=ada;Password=segreta;Database=shop",
        }, null, TestContext.Current.CancellationToken);

        var file = await File.ReadAllTextAsync(_path, TestContext.Current.CancellationToken);
        Assert.DoesNotContain("segreta", file, StringComparison.Ordinal);
        Assert.Equal("segreta", await credentials.GetSecretAsync("pg", TestContext.Current.CancellationToken));
        Assert.DoesNotContain("Password", directory.Find("pg")!.ConnectionString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnFileVecchioConLaPasswordNellaStringaSiMigraAlSalvataggio()
    {
        await File.WriteAllTextAsync(_path,
            """[{"id":"o","name":"O","provider":"Odbc","connectionString":"DSN=x;UID=ada;PWD={s;e}"}]""",
            TestContext.Current.CancellationToken);

        var credentials = new MemoryCredentials();
        var directory = new FileConnectionDirectory(_path, credentials);

        await directory.SaveAsync(directory.Find("o")!, null, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("s;e", await File.ReadAllTextAsync(_path, TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal("s;e", await credentials.GetSecretAsync("o", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConLaSceltaInChiaroLaStringaNonSiTocca()
    {
        var directory = new FileConnectionDirectory(_path, new MemoryCredentials());

        await directory.SaveAsync(new ConnectionDescriptor
        {
            Id = "c",
            DisplayName = "C",
            Provider = DatabaseProviderKind.SqlServer,
            ConnectionString = "Server=x;Password=chiara",
            SecretInFile = true,
        }, null, TestContext.Current.CancellationToken);

        Assert.Contains("chiara", directory.Find("c")!.ConnectionString, StringComparison.Ordinal);
    }
}

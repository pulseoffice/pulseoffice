using PulseOffice.Data;
using PulseOffice.Data.Sqlite;
using PulseOffice.Document.Data;
using PulseOffice.Document.Embedding;
using Xunit;

namespace PulseOffice.Data.Tests;

public sealed class DataTableLoaderTests
{
    private sealed class NoCredentials : ICredentialStore
    {
        public Task<string?> GetSecretAsync(string credentialKey, CancellationToken ct) => Task.FromResult<string?>(null);

        public Task SetSecretAsync(string credentialKey, string secret, CancellationToken ct) => Task.CompletedTask;

        public Task RemoveAsync(string credentialKey, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class OneConnection(ConnectionDescriptor connection) : IConnectionDirectory
    {
        public IReadOnlyList<ConnectionDescriptor> All => [connection];

        public ConnectionDescriptor? Find(string id) => id == connection.Id ? connection : null;

        public Task SaveAsync(ConnectionDescriptor descriptor, string? secret, CancellationToken ct = default) => Task.CompletedTask;

        public Task RemoveAsync(string id, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task AConnectionTheDriverRejectsIsReportedInsteadOfThrown()
    {
        var providers = new DatabaseProviderRegistry();
        providers.Register(new SqliteDatabaseProvider(new NoCredentials()));
        var connection = new ConnectionDescriptor
        {
            Id = "broken",
            DisplayName = "Broken",
            Provider = DatabaseProviderKind.Sqlite,
            ConnectionString = "Data Source=:memory:;Unknown Keyword=1",
        };
        var loader = new DataTableLoader(providers, new OneConnection(connection));

        var loaded = await loader.LoadAsync(new DataQuerySource { ConnectionId = "broken", Sql = "SELECT 1" }, TestContext.Current.CancellationToken);

        Assert.False(loaded.Succeeded);
        Assert.Equal("data.query.failed", Assert.Single(loaded.Diagnostics).Code);
    }
}

using PulseOffice.Sheets.UI.ExternalData;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class ExternalDataFetcherTests
{
    [Fact]
    public async Task UnPercorsoRelativoSiRisolveDallaCartellaDelDocumento()
    {
        var folder = Directory.CreateTempSubdirectory("qo-ext").FullName;
        var data = Directory.CreateDirectory(Path.Combine(folder, "dati")).FullName;
        await File.WriteAllTextAsync(Path.Combine(data, "a b.csv"), "x;y", TestContext.Current.CancellationToken);
        using var fetcher = new ExternalDataFetcher(new PulseOffice.Foundation.Network.OutboundHttpClients(new PulseOffice.Foundation.Network.AddressClassifier()), new PulseOffice.Foundation.Network.NetworkSharePaths());

        Assert.Equal("x;y", await fetcher.FetchAsync("dati/a%20b.csv", folder, TestContext.Current.CancellationToken));
        Assert.Equal("x;y", await fetcher.FetchAsync("../dati/a b.csv", data, TestContext.Current.CancellationToken));
        Assert.Equal("https://example.org/a.csv", fetcher.Resolve("https://example.org/a.csv", folder));
        Directory.Delete(folder, true);
    }
}

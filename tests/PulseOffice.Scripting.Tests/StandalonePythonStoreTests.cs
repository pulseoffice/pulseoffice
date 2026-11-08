using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Scripting.CPython;
using Xunit;

namespace PulseOffice.Scripting.Tests;

public sealed class StandalonePythonStoreTests
{
    private sealed class Server(string release, string sums) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            HttpContent content = path.EndsWith("/latest", StringComparison.Ordinal) ? new StringContent(release)
                : path.EndsWith("/SHA256SUMS", StringComparison.Ordinal) ? new StringContent(sums)
                : new ByteArrayContent([1, 2, 3, 4]);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    private sealed class Consent : IPythonDownloadConsent, IProgress<ProvisioningProgress>
    {
        public List<ProvisioningProgress> Reports { get; } = [];

        public IProgress<ProvisioningProgress> Progress => this;

        public Task<bool> AllowAsync(PythonDownloadOffer offer, CancellationToken ct = default) => Task.FromResult(true);

        public void Report(ProvisioningProgress value) => Reports.Add(value);
    }

    [Fact]
    public async Task Una_somma_sbagliata_non_installa_nulla_chiude_l_avanzamento_e_non_lascia_residui()
    {
        var catalog = new StandalonePythonCatalog();
        var name = $"cpython-3.13.9+20260101-{catalog.Triple}-install_only.tar.gz";
        var release = "{\"assets\":[" +
            $"{{\"name\":\"{name}\",\"size\":4,\"browser_download_url\":\"https://github.com/astral-sh/python-build-standalone/releases/download/20260101/{name}\"}}," +
            "{\"name\":\"SHA256SUMS\",\"browser_download_url\":\"https://github.com/astral-sh/python-build-standalone/releases/download/20260101/SHA256SUMS\"}]}";
        var root = Directory.CreateTempSubdirectory("pulse-standalone-").FullName;
        var consent = new Consent();
        var store = new StandalonePythonStore(root, new HttpClient(new Server(release, $"{new string('0', 64)}  {name}\n")),
            catalog, new PythonDiscovery(), NullLogger<StandalonePythonStore>.Instance);

        var installed = await store.InstallAsync(consent, TestContext.Current.CancellationToken);

        Assert.Null(installed);
        Assert.Equal(1, consent.Reports[^1].Fraction);
        Assert.Empty(Directory.EnumerateFiles(root));
    }
}

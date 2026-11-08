using PulseOffice.Scripting.CPython;
using Xunit;

namespace PulseOffice.Scripting.Tests;

public sealed class StandalonePythonCatalogTests
{
    private readonly StandalonePythonCatalog _catalog = new();

    private string Release(params string[] names) =>
        "{\"assets\":[" + string.Join(",", names.Select(name =>
            $"{{\"name\":\"{name}\",\"size\":1234,\"browser_download_url\":\"https://github.com/astral-sh/python-build-standalone/releases/download/20260101/{name}\"}}")) + "]}";

    [Fact]
    public void Sceglie_la_versione_preferita_per_questa_macchina_e_ne_legge_la_somma_di_controllo()
    {
        var triple = _catalog.Triple;
        var preferred = $"cpython-3.13.9+20260101-{triple}-install_only.tar.gz";
        var json = Release(
            $"cpython-3.14.2+20260101-{triple}-install_only.tar.gz",
            preferred,
            $"cpython-3.13.9+20260101-{triple}-freethreaded-install_only.tar.gz",
            $"cpython-3.13.9+20260101-{triple}-install_only_stripped.tar.gz",
            "cpython-3.13.9+20260101-riscv64-unknown-linux-gnu-install_only.tar.gz",
            "SHA256SUMS");

        var asset = _catalog.Pick(json, $"ABCDEF  {preferred}\n0123  other.tar.gz\n");

        Assert.NotNull(asset);
        Assert.Equal(preferred, asset.Name);
        Assert.Equal(new Version(3, 13, 9), asset.Version);
        Assert.Equal("abcdef", asset.Sha256);
        Assert.EndsWith("/SHA256SUMS", _catalog.ChecksumsAsset(json), StringComparison.Ordinal);
    }

    [Fact]
    public void Senza_una_build_per_questa_macchina_non_propone_nulla()
    {
        Assert.Null(_catalog.Pick(Release("cpython-3.13.9+20260101-riscv64-unknown-linux-gnu-install_only.tar.gz"), null));
    }

    [Fact]
    public void Senza_somma_di_controllo_non_propone_nulla_da_eseguire()
    {
        var name = $"cpython-3.13.9+20260101-{_catalog.Triple}-install_only.tar.gz";

        Assert.Null(_catalog.Pick(Release(name), null));
        Assert.Null(_catalog.Pick(Release(name), "0123  other.tar.gz\n"));
    }

    [Fact]
    public void Nomi_strani_e_indirizzi_non_https_si_scartano_senza_eccezioni()
    {
        var triple = _catalog.Triple;
        var plain = $"cpython-3.13.9+20260101-{triple}-install_only.tar.gz";
        var json = "{\"assets\":[" +
            $"{{\"name\":\"cpython-3.13.9-{triple}-install_only.tar.gz\",\"browser_download_url\":\"https://github.com/x\"}}," +
            $"{{\"name\":\"{plain}\",\"browser_download_url\":\"http://example.com/{plain}\"}}," +
            $"{{\"name\":\"{plain}\"}}]}}";

        Assert.Null(_catalog.Pick(json, $"abcdef  {plain}\ndef0  cpython-3.13.9-{triple}-install_only.tar.gz\n"));
    }

    [Fact]
    public void Downloads_from_any_origin_other_than_the_project_releases_are_refused()
    {
        var name = $"cpython-3.13.9+20260101-{_catalog.Triple}-install_only.tar.gz";
        var json = "{\"assets\":[" +
            $"{{\"name\":\"{name}\",\"browser_download_url\":\"https://evil.example/astral-sh/python-build-standalone/releases/download/1/{name}\"}}," +
            "{\"name\":\"SHA256SUMS\",\"browser_download_url\":\"https://github.com/someone/else/releases/download/1/SHA256SUMS\"}]}";

        Assert.Null(_catalog.Pick(json, $"abcdef  {name}\n"));
        Assert.Null(_catalog.ChecksumsAsset(json));
    }
}

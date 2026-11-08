using PulseOffice.Drawing.Assets;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public class AssetLibraryTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "pulseoffice-raccolte-" + Guid.NewGuid().ToString("N"));

    private const string Svg =
        """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><path d="M2 2 L22 22"/></svg>""";

    private void Pack(string id, string? manifest, params string[] names)
    {
        var directory = Path.Combine(_root, id);
        Directory.CreateDirectory(directory);

        if (manifest is not null) File.WriteAllText(Path.Combine(directory, "pack.json"), manifest);

        foreach (var name in names)
            File.WriteAllText(Path.Combine(directory, name + ".svg"), Svg);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private IAssetLibrary Library() => new AssetLibrary([new FolderAssetSource(_root)]);

    [Fact]
    public void UnaCartellaInesistenteNonHaRaccolte()
    {
        Assert.Empty(new FolderAssetSource(Path.Combine(_root, "assente")).Packs);
    }

    [Fact]
    public void UnaCartellaDiventaUnaRaccolta()
    {
        Pack("mie-icone", null, "casa", "cuore");

        var pack = Assert.Single(Library().Packs);

        Assert.Equal("mie-icone", pack.Id);
        Assert.Equal(2, pack.ItemCount);
    }

    [Fact]
    public void IlManifestoDaNomeLicenzaETipo()
    {
        Pack("pacchetto", """{"id":"tabler","name":"Tabler","kind":"Stickers","license":"MIT"}""", "casa");

        var pack = Assert.Single(Library().Packs);

        Assert.Equal("tabler", pack.Id);
        Assert.Equal("Tabler", pack.DisplayName);
        Assert.Equal(AssetPackKind.Stickers, pack.Kind);
        Assert.Equal("MIT", pack.License.SpdxId);
    }

    [Fact]
    public void UnManifestoRottoNonFaCadereLaRaccolta()
    {
        Pack("rotto", "{non json", "casa");

        Assert.Single(Library().Packs);
    }

    [Fact]
    public async Task LaRicercaSenzaTestoTrovaTutto()
    {
        Pack("mie", null, "casa", "cuore", "stella");

        Assert.Equal(3, (await Library().SearchAsync("", ct: TestContext.Current.CancellationToken)).Count);
    }

    [Fact]
    public async Task LaRicercaFiltraPerNomeEParoleChiave()
    {
        Pack("mie", null, "casa-grande", "cuore");

        var found = await Library().SearchAsync("grande", ct: TestContext.Current.CancellationToken);

        Assert.Equal("casa grande", Assert.Single(found).Name);
    }

    [Fact]
    public async Task ITerminiSiSommano()
    {
        Pack("mie", null, "freccia-su", "freccia-giu");

        Assert.Single(await Library().SearchAsync("freccia giu", ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LaRicercaSiPuoLimitareAUnaRaccolta()
    {
        Pack("prima", null, "casa");
        Pack("seconda", null, "casa");

        Assert.Single(await Library().SearchAsync("casa", packId: "seconda", ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OgniElementoPortaIlProprioSvg()
    {
        Pack("mie", null, "casa");

        var asset = Assert.Single(await Library().SearchAsync("", ct: TestContext.Current.CancellationToken));

        Assert.Contains("<path", asset.SvgContent);
        Assert.Equal("mie", asset.PackId);
        Assert.Equal("casa", asset.ItemId);
    }

    [Fact]
    public async Task UnElementoSiRitrovaPerIdentificativo()
    {
        Pack("mie", null, "casa");

        Assert.NotNull(await Library().GetAsync("mie", "casa", TestContext.Current.CancellationToken));
        Assert.Null(await Library().GetAsync("mie", "assente", TestContext.Current.CancellationToken));
    }
}

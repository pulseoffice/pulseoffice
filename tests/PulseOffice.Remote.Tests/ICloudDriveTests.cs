using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Platform;
using PulseOffice.Remote;
using PulseOffice.Remote.Cloud;
using Xunit;

namespace PulseOffice.Remote.Tests;

public class ICloudDriveTests : IDisposable
{
    private readonly string _folder =
        Path.Combine(Path.GetTempPath(), "pulseoffice-icloud-" + Guid.NewGuid().ToString("N"));

    private sealed class Folder(string? path) : ISyncedFolderLocator
    {
        public OperationResult<string> ICloudDrive() =>
            path is null
                ? OperationResult<string>.Fail(Diagnostic.Error("CLOUD020", "Solo su macOS."))
                : OperationResult<string>.Ok(path);
    }

    private static RemoteConnection Connection() =>
        new() { Name = "iCloud", Protocol = RemoteProtocol.ICloudDrive };

    public ICloudDriveTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task LaCartellaSincronizzataSiLeggeESiScrive()
    {
        var storage = new ICloudDriveStorage(new Folder(_folder), new PulseOffice.Platform.RuntimeHostSystem());

        var written = await storage.WriteAsync(
            Connection(), "/Conti/bilancio.ods", [1, 2, 3], null, TestContext.Current.CancellationToken);

        Assert.True(written.Succeeded);

        var listing = await storage.ListAsync(
            Connection(), "/Conti", TestContext.Current.CancellationToken);

        Assert.Equal("bilancio.ods", Assert.Single(listing.Value!).Name);

        var read = await storage.ReadAsync(
            Connection(), "/Conti/bilancio.ods", TestContext.Current.CancellationToken);

        Assert.Equal([1, 2, 3], read.Value!.Bytes);
    }

    [Fact]
    public async Task LeCartelleVengonoPrimaDeiFile()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "zeta"));
        await File.WriteAllTextAsync(
            Path.Combine(_folder, "alfa.ods"), "x", TestContext.Current.CancellationToken);

        var storage = new ICloudDriveStorage(new Folder(_folder), new PulseOffice.Platform.RuntimeHostSystem());

        var listing = await storage.ListAsync(Connection(), "/", TestContext.Current.CancellationToken);

        Assert.True(listing.Value![0].IsFolder);
        Assert.Equal("zeta", listing.Value![0].Name);
    }

    [Fact]
    public async Task FuoriDaMacOsLoDiceInveceDiFallireInSilenzio()
    {
        var storage = new ICloudDriveStorage(new Folder(null), new PulseOffice.Platform.RuntimeHostSystem());

        var listing = await storage.ListAsync(Connection(), "/", TestContext.Current.CancellationToken);

        Assert.False(listing.Succeeded);
        Assert.Equal("CLOUD020", listing.Diagnostics[0].Code);
    }

    [Fact]
    public void UnPercorsoNonPuoUscireDallaCartella()
    {
        Assert.Throws<ArgumentException>(() => new ICloudDriveStorage(new Folder(_folder), new PulseOffice.Platform.RuntimeHostSystem()).Combined(_folder, "/../altrove/segreto.ods"));
        Assert.Throws<ArgumentException>(() => new ICloudDriveStorage(new Folder(_folder), new PulseOffice.Platform.RuntimeHostSystem()).Combined(_folder, OperatingSystem.IsWindows() ? "/a/..\\..\\x.ods" : "/a/../../x.ods"));
        Assert.StartsWith(Path.GetFullPath(_folder), new ICloudDriveStorage(new Folder(_folder), new PulseOffice.Platform.RuntimeHostSystem()).Combined(_folder, "/Documenti/conti.ods"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnDocumentoNonAncoraScaricatoLoDice()
    {
        var storage = new ICloudDriveStorage(new Folder(_folder), new PulseOffice.Platform.RuntimeHostSystem());

        var read = await storage.ReadAsync(
            Connection(), "/mai-visto.ods", TestContext.Current.CancellationToken);

        Assert.False(read.Succeeded);
        Assert.Equal("CLOUD023", read.Diagnostics[0].Code);
    }
}

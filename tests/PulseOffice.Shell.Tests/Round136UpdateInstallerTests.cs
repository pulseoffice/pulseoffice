using System.Runtime.Versioning;
using System.Security.Cryptography;
using PulseOffice.Platform;
using PulseOffice.Platform.Linux;
using PulseOffice.Platform.Windows;
using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class Round136UpdateInstallerTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("pulse-r136-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private sealed class Location(string path) : IAppImageLocation
    {
        public string? Current { get; private set; } = path;

        public void MovedTo(string moved) => Current = moved;
    }

    [Fact]
    public async Task The_windows_installer_is_staged_with_the_hash_its_helper_checks_before_elevating()
    {
        var package = Path.Combine(_folder, "PulseOffice-9.0.0-windows-x64-setup.exe");
        await File.WriteAllBytesAsync(package, [1, 2, 3, 4], TestContext.Current.CancellationToken);
        var installer = new WindowsUpdateInstaller(_folder, "PulseOffice.exe", 1);

        var staged = await installer.StageAsync(package, TestContext.Current.CancellationToken);

        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(package))), staged.Sha256);
        var arguments = installer.HelperArguments(staged, "helper.ps1").ToList();
        Assert.Equal(staged.Sha256, arguments[arguments.IndexOf("-Sha256") + 1]);
    }

    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task Staging_an_appimage_does_not_write_through_a_planted_link()
    {
        var current = Path.Combine(_folder, "PulseOffice.AppImage");
        await File.WriteAllTextAsync(current, "old", TestContext.Current.CancellationToken);
        var victim = Path.Combine(_folder, "victim");
        await File.WriteAllTextAsync(victim, "precious", TestContext.Current.CancellationToken);
        File.CreateSymbolicLink(Path.Combine(_folder, ".PulseOffice.AppImage.update"), victim);
        var package = Path.Combine(_folder, "package");
        await File.WriteAllTextAsync(package, "new", TestContext.Current.CancellationToken);

        var staged = await new AppImageUpdateInstaller(new Location(current), 1).StageAsync(package, TestContext.Current.CancellationToken);

        Assert.Equal("precious", await File.ReadAllTextAsync(victim, TestContext.Current.CancellationToken));
        Assert.Equal("new", await File.ReadAllTextAsync(staged.StagedPath, TestContext.Current.CancellationToken));
        Assert.Null(new FileInfo(staged.StagedPath).LinkTarget);
    }
}

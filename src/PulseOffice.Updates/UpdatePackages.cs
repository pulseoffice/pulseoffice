using System.Runtime.InteropServices;
using PulseOffice.Platform;

namespace PulseOffice.Updates;

public sealed record UpdatePackage(ReleaseAsset Asset, ReleaseAsset Checksums, ReleaseAsset? Signature);

public interface IUpdatePackageChoice
{
    string? PackageName(ReleaseVersion version);

    UpdatePackage? Choose(ReleaseInfo release);
}

public sealed class UpdatePackageChoice(IHostSystem host) : IUpdatePackageChoice
{
    public const string ChecksumsName = "SHA256SUMS";
    public const string SignatureName = "SHA256SUMS.sig";
    private const string Product = "PulseOffice";

    public string? PackageName(ReleaseVersion version)
    {
        var architecture = host.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X64 => "x64",
            _ => null,
        };
        if (architecture is null) return null;

        return host.Kind switch
        {
            HostSystemKind.MacOS => $"{Product}-{version}-macos-{architecture}.dmg",
            HostSystemKind.Windows => $"{Product}-{version}-windows-{architecture}-setup.exe",
            HostSystemKind.Linux => $"{Product}-{version}-linux-{(architecture == "arm64" ? "aarch64" : "x86_64")}.AppImage",
            _ => null,
        };
    }

    public UpdatePackage? Choose(ReleaseInfo release)
    {
        ArgumentNullException.ThrowIfNull(release);
        if (PackageName(release.Version) is not { } name) return null;
        if (release.Asset(name) is not { } asset || release.Asset(ChecksumsName) is not { } checksums) return null;
        return new UpdatePackage(asset, checksums, release.Asset(SignatureName));
    }
}

namespace PulseOffice.Updates;

public sealed record ReleaseAsset(string Name, Uri Address, long Size);

public sealed record ReleaseInfo(ReleaseVersion Version, string Tag, string Notes, Uri Page, IReadOnlyList<ReleaseAsset> Assets)
{
    public ReleaseAsset? Asset(string name) =>
        Assets.FirstOrDefault(asset => string.Equals(asset.Name, name, StringComparison.Ordinal));
}

public interface IUpdateEndpoints
{
    Uri LatestRelease { get; }

    Uri ReleasesPage { get; }

    IReadOnlyList<string> TrustedDownloadHosts { get; }
}

public sealed class GitHubUpdateEndpoints : IUpdateEndpoints
{
    public Uri LatestRelease { get; } = new("https://api.github.com/repos/pulseoffice/pulseoffice/releases/latest");

    public Uri ReleasesPage { get; } = new("https://github.com/pulseoffice/pulseoffice/releases/latest");

    public IReadOnlyList<string> TrustedDownloadHosts { get; } =
        ["github.com", "objects.githubusercontent.com", "release-assets.githubusercontent.com"];
}

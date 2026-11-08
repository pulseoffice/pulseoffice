namespace PulseOffice.Presentations.UI;

public abstract record DeckMediaSource;

public sealed record EmbeddedMediaSource(MediaElement Media) : DeckMediaSource;

public sealed record LinkedMediaFile(string Path, bool Exists) : DeckMediaSource;

public sealed record BlockedMediaFile(string Path) : DeckMediaSource;

public sealed record LinkedMediaAddress(string Address) : DeckMediaSource;

public interface IDeckMediaFiles
{
    DeckMediaSource Source(MediaElement media, string? documentPath);
}

public sealed class DeckMediaFiles(PulseOffice.Platform.IExecutableFileTypes executables, PulseOffice.Foundation.Network.INetworkSharePaths shares) : IDeckMediaFiles
{
    public DeckMediaSource Source(MediaElement media, string? documentPath)
    {
        ArgumentNullException.ThrowIfNull(media);
        if (!media.IsLinked) return new EmbeddedMediaSource(media);

        var link = media.LinkedPath!;
        if (shares.IsNetworkShare(link)) return new BlockedMediaFile(link);
        if (Uri.TryCreate(link, UriKind.Absolute, out var absolute) && !Path.IsPathRooted(link))
            return absolute.IsFile ? File(absolute.LocalPath) : new LinkedMediaAddress(absolute.AbsoluteUri);
        if (Path.IsPathRooted(link)) return File(link);

        var relative = Uri.UnescapeDataString(link.StartsWith("./", StringComparison.Ordinal) ? link[2..] : link)
            .Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var folder = documentPath is { Length: > 0 } ? Path.GetDirectoryName(Path.GetFullPath(documentPath)) : null;
        return File(Path.GetFullPath(folder is null ? relative : Path.Combine(folder, relative)));
    }

    private DeckMediaSource File(string path) =>
        executables.CanRunCode(path) || shares.IsNetworkShare(path) ? new BlockedMediaFile(path) : new LinkedMediaFile(path, System.IO.File.Exists(path));
}

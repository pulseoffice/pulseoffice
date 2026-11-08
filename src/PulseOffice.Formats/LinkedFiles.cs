namespace PulseOffice.Formats;

public interface ILinkedFiles
{
    string ToHref(string path);

    string? ToPath(string href);

    bool IsExternal(string href);

    string ToPackageHref(string link);

    string FromPackageHref(string href);

    byte[]? Load(string? path);
}

public sealed class LinkedFiles : ILinkedFiles
{
    public string ToHref(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri)) return uri.IsFile ? new Uri(Path.GetFullPath(uri.LocalPath)).AbsoluteUri : uri.AbsoluteUri;
        return Path.IsPathRooted(path) ? new Uri(Path.GetFullPath(path)).AbsoluteUri : path.Replace('\\', '/');
    }

    public bool IsExternal(string href) =>
        !string.IsNullOrEmpty(href)
        && (href.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
            || href.StartsWith("http:", StringComparison.OrdinalIgnoreCase)
            || href.StartsWith("https:", StringComparison.OrdinalIgnoreCase)
            || href.StartsWith("../", StringComparison.Ordinal)
            || Path.IsPathRooted(href));

    public string ToPackageHref(string link) =>
        Uri.TryCreate(link, UriKind.Absolute, out _) || Path.IsPathRooted(link) || link.StartsWith('#') ? link : "../" + link.Replace('\\', '/');

    public string FromPackageHref(string href) =>
        href.StartsWith("../", StringComparison.Ordinal) ? href[3..] : href;

    public string? ToPath(string href)
    {
        if (string.IsNullOrWhiteSpace(href)) return null;
        if (Uri.TryCreate(href, UriKind.Absolute, out var uri))
        {
            if (uri.IsFile) return uri.LocalPath;
            if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) return uri.AbsoluteUri;
            return Path.IsPathRooted(href) ? href : null;
        }
        return href;
    }

    public byte[]? Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

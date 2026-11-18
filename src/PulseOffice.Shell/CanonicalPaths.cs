namespace PulseOffice.Shell;

public sealed class CanonicalPaths
{
    public string Of(string path) => Canonical(path, depth: 0);

    private string Canonical(string path, int depth)
    {
        if (depth > 16) return path;
        try
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetPathRoot(full) ?? string.Empty;
            var resolved = root;
            foreach (var part in full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            {
                resolved = Path.Combine(resolved, part);
                var info = Directory.Exists(resolved) ? (FileSystemInfo)new DirectoryInfo(resolved) : new FileInfo(resolved);
                if (info.Exists && info.LinkTarget is not null && info.ResolveLinkTarget(returnFinalTarget: true) is { } target)
                    resolved = Canonical(target.FullName, depth + 1);
            }
            return resolved;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return path;
        }
    }
}

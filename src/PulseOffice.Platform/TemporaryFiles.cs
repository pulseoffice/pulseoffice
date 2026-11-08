namespace PulseOffice.Platform;

public interface ITemporaryFiles
{
    string NewFolder(string prefix);
}

public sealed class TemporaryFiles : ITemporaryFiles, IDisposable
{
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private static readonly TimeSpan Stale = TimeSpan.FromDays(1);

    private readonly string _root;
    private readonly string _session;

    public TemporaryFiles() : this(Path.Combine(Path.GetTempPath(), $"pulseoffice-{Environment.UserName}"))
    {
    }

    public TemporaryFiles(string root)
    {
        _root = Private(root);
        _session = Path.Combine(_root, $"session-{Environment.ProcessId}-{Guid.NewGuid():N}");
        Sweep();
    }

    public string NewFolder(string prefix)
    {
        CreatePrivate(_session);
        var folder = Path.Combine(_session, $"{prefix}{Guid.NewGuid():N}");
        CreatePrivate(folder);
        return folder;
    }

    public void Dispose() => Delete(_session);

    private string Private(string root)
    {
        if (Usable(root)) return root;
        var fresh = $"{root}-{Guid.NewGuid():N}";
        CreatePrivate(fresh);
        return fresh;
    }

    private bool Usable(string root)
    {
        try
        {
            if (new DirectoryInfo(root).LinkTarget is not null) return false;
            CreatePrivate(root);
            if (new DirectoryInfo(root).LinkTarget is not null) return false;
            if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(root) & ~OwnerOnly) != 0) return false;
            _ = Directory.EnumerateFileSystemEntries(root).Any();
            var probe = Path.Combine(root, $"probe-{Guid.NewGuid():N}");
            Directory.CreateDirectory(probe);
            Directory.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void CreatePrivate(string folder)
    {
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(folder);
        else Directory.CreateDirectory(folder, OwnerOnly);
    }

    private void Sweep()
    {
        if (!Directory.Exists(_root)) return;

        foreach (var folder in Directory.EnumerateDirectories(_root, "session-*"))
            if (DateTime.UtcNow - Directory.GetLastWriteTimeUtc(folder) > Stale && !OwnerAlive(folder))
                Delete(folder);
    }

    private bool OwnerAlive(string folder)
    {
        var parts = Path.GetFileName(folder).Split('-');
        if (parts.Length < 2 || !int.TryParse(parts[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id))
            return false;

        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(id);
            return !process.HasExited;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private void Delete(string folder)
    {
        try
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}

namespace PulseOffice.Settings;

public sealed record SettingsReloadedEventArgs(IReadOnlyList<string> Keys);

public interface IReloadingSettingsStore : ISettingsStore
{
    event EventHandler<SettingsReloadedEventArgs>? Reloaded;

    IReadOnlyList<string> Reload();
}

public interface ISettingsFileMonitor
{
    IDisposable Watch(string path, Func<IReadOnlyList<string>> changed);
}

public sealed class NoSettingsFileMonitor : ISettingsFileMonitor
{
    public IDisposable Watch(string path, Func<IReadOnlyList<string>> changed) => new Unwatched();

    private sealed class Unwatched : IDisposable
    {
        public void Dispose()
        {
        }
    }
}

public sealed class FileSystemSettingsFileMonitor : ISettingsFileMonitor
{
    public IDisposable Watch(string path, Func<IReadOnlyList<string>> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        var folder = Path.GetDirectoryName(Path.GetFullPath(path));
        if (string.IsNullOrEmpty(folder)) return new NoSettingsFileMonitor().Watch(path, changed);
        Directory.CreateDirectory(folder);

        var watcher = new FileSystemWatcher(folder, Path.GetFileName(path))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        watcher.Changed += (_, _) => changed();
        watcher.Created += (_, _) => changed();
        watcher.Renamed += (_, _) => changed();
        watcher.EnableRaisingEvents = true;
        return watcher;
    }
}

public interface ISettingsChangeDispatch
{
    void Post(Action notify);
}

public sealed class InlineSettingsChangeDispatch : ISettingsChangeDispatch
{
    public void Post(Action notify) => notify();
}

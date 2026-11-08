namespace PulseOffice.UI.Controls.Objects;

public interface IExternalEditWatcher
{
    void Watch(string file, Action<byte[]> onChanged);
}

public sealed class ExternalEditWatcher : IExternalEditWatcher, IDisposable
{
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly Lock _gate = new();

    public void Watch(string file, Action<byte[]> onChanged)
    {
        ArgumentException.ThrowIfNullOrEmpty(file);
        ArgumentNullException.ThrowIfNull(onChanged);

        var stamp = File.Exists(file) ? File.GetLastWriteTimeUtc(file) : DateTime.MinValue;

        var watcher = new FileSystemWatcher(Path.GetDirectoryName(file)!, Path.GetFileName(file))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };

        watcher.Changed += (_, _) =>
        {
            if (File.GetLastWriteTimeUtc(file) <= stamp) return;

            watcher.EnableRaisingEvents = false;

            byte[] bytes;

            try
            {
                bytes = File.ReadAllBytes(file);
            }
            catch (IOException)
            {
                return;
            }

            onChanged(bytes);

            Forget(watcher);
        };

        lock (_gate) _watchers.Add(watcher);
    }

    private void Forget(FileSystemWatcher watcher)
    {
        lock (_gate) _watchers.Remove(watcher);

        watcher.Dispose();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var watcher in _watchers) watcher.Dispose();

            _watchers.Clear();
        }
    }
}

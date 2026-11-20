namespace PulseOffice.Shell;

public interface IRecentAvailability
{
    Task<IReadOnlySet<string>> MissingAsync(IReadOnlyList<string> paths, CancellationToken ct = default);
}

public sealed class FileRecentAvailability : IRecentAvailability
{
    public Task<IReadOnlySet<string>> MissingAsync(IReadOnlyList<string> paths, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var copy = paths.ToList();
        return Task.Run<IReadOnlySet<string>>(() =>
        {
            var missing = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in copy)
            {
                ct.ThrowIfCancellationRequested();
                if (!Exists(path)) missing.Add(path);
            }

            return missing;
        }, ct);
    }

    private bool Exists(string path)
    {
        try
        {
            return File.Exists(path);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return true;
        }
    }
}

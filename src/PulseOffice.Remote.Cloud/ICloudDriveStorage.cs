using System.Globalization;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Platform;

namespace PulseOffice.Remote.Cloud;

public sealed class ICloudDriveStorage(ISyncedFolderLocator folders, IHostSystem host) : IRemoteStorage
{
    public RemoteProtocol Protocol => RemoteProtocol.ICloudDrive;

    public bool SupportsCheckOut => false;

    public Task<OperationResult<IReadOnlyList<RemoteEntry>>> ListAsync(
        RemoteConnection connection, string path, CancellationToken ct = default)
    {
        var root = Root();

        if (!root.Succeeded)
            return Task.FromResult(OperationResult<IReadOnlyList<RemoteEntry>>.Fail(root.Diagnostics[0]));

        try
        {
            var folder = Combined(root.Value!, path);

            if (!Directory.Exists(folder))
                return Task.FromResult(OperationResult<IReadOnlyList<RemoteEntry>>.Fail(
                    Diagnostic.Error("CLOUD022", "The folder does not exist in iCloud Drive.")));

            var entries = new List<RemoteEntry>();

            foreach (var directory in Directory.EnumerateDirectories(folder).Order(StringComparer.CurrentCulture))
            {
                var name = Path.GetFileName(directory);

                entries.Add(new RemoteEntry(
                    name, CloudPaths.Join(path, name), true, 0, Directory.GetLastWriteTimeUtc(directory)));
            }

            foreach (var file in Directory.EnumerateFiles(folder).Order(StringComparer.CurrentCulture))
            {
                var info = new FileInfo(file);

                entries.Add(new RemoteEntry(
                    info.Name, CloudPaths.Join(path, info.Name), false, info.Length, info.LastWriteTimeUtc));
            }

            return Task.FromResult(OperationResult<IReadOnlyList<RemoteEntry>>.Ok(entries));
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(OperationResult<IReadOnlyList<RemoteEntry>>.Fail(Diagnostic.Error("CLOUD025", ex.Message).With(ex.Message)));
        }
    }

    public async Task<OperationResult<RemoteContent>> ReadAsync(
        RemoteConnection connection, string path, CancellationToken ct = default)
    {
        var file = FileFor(path);

        if (!file.Succeeded) return OperationResult<RemoteContent>.Fail(file.Diagnostics[0]);

        if (!File.Exists(file.Value!))
            return OperationResult<RemoteContent>.Fail(Diagnostic.Error("CLOUD023", "The document is not in the synced folder yet."));

        try
        {
            var version = VersionOf(file.Value!);
            var bytes = await File.ReadAllBytesAsync(file.Value!, ct).ConfigureAwait(false);

            return OperationResult<RemoteContent>.Ok(new RemoteContent(bytes, version));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return OperationResult<RemoteContent>.Fail(Diagnostic.Error("CLOUD024", ex.Message).With(ex.Message));
        }
    }

    public async Task<OperationResult<RemoteWriteOutcome>> WriteAsync(
        RemoteConnection connection, string path, byte[] content, RemoteWriteConditions? conditions,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var file = FileFor(path);

        if (!file.Succeeded) return OperationResult<RemoteWriteOutcome>.Fail(file.Diagnostics[0]);

        try
        {
            if (conditions?.ExpectedVersion is { } expected && File.Exists(file.Value!)
                && !string.Equals(VersionOf(file.Value!), expected, StringComparison.Ordinal))
                return OperationResult<RemoteWriteOutcome>.Ok(new RemoteWriteOutcome(RemoteWriteStatus.Conflict, null));

            Directory.CreateDirectory(Path.GetDirectoryName(file.Value!)!);

            await File.WriteAllBytesAsync(file.Value!, content, ct).ConfigureAwait(false);

            return OperationResult<RemoteWriteOutcome>.Ok(new RemoteWriteOutcome(RemoteWriteStatus.Written, VersionOf(file.Value!)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return OperationResult<RemoteWriteOutcome>.Fail(Diagnostic.Error("CLOUD024", ex.Message).With(ex.Message));
        }
    }

    private string VersionOf(string file)
    {
        var info = new FileInfo(file);

        return string.Create(CultureInfo.InvariantCulture, $"{info.LastWriteTimeUtc.Ticks}-{info.Length}");
    }

    public Task<OperationResult<RemoteLock>> LockAsync(
        RemoteConnection connection, string path, CancellationToken ct = default) =>
        Task.FromResult(OperationResult<RemoteLock>.Fail(Diagnostic.Error("CLOUD031", "iCloud Drive has no check-out: it is a synced folder, not a server.")));

    public Task<OperationResult<bool>> UnlockAsync(
        RemoteConnection connection, RemoteLock held, CancellationToken ct = default) =>
        Task.FromResult(OperationResult<bool>.Fail(Diagnostic.Error("CLOUD031", "iCloud Drive has no check-out: it is a synced folder, not a server.")));

    public OperationResult<string> Root() => folders.ICloudDrive();

    private OperationResult<string> FileFor(string path)
    {
        var root = Root();

        if (!root.Succeeded) return OperationResult<string>.Fail(root.Diagnostics[0]);

        try
        {
            return OperationResult<string>.Ok(Combined(root.Value!, path));
        }
        catch (ArgumentException ex)
        {
            return OperationResult<string>.Fail(Diagnostic.Error("CLOUD026", ex.Message).With(ex.Message));
        }
    }

    public string Combined(string root, string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);

        var parts = (path ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
            if (part is ".." or ".")
                throw new ArgumentException("A remote path cannot climb out of the folder.", nameof(path));

        if (parts.Length == 0) return root;

        var top = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        var combined = Path.GetFullPath(Path.Combine([root, .. parts]));
        if (parts.Any(Path.IsPathRooted) || !combined.StartsWith(top, host.PathComparison))
            throw new ArgumentException("A remote path cannot climb out of the folder.", nameof(path));

        return combined;
    }
}

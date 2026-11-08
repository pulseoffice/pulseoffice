namespace PulseOffice.Platform;

public sealed record FileFilter(string Name, IReadOnlyList<string> Extensions);

public sealed record ChosenFile(string Path, string Name) : IAsyncDisposable
{
    public required Func<CancellationToken, Task<Stream>> OpenRead { get; init; }
    public required Func<CancellationToken, Task<Stream>> OpenWrite { get; init; }

    public IAsyncDisposable? Handle { get; init; }

    public ValueTask DisposeAsync() => Handle?.DisposeAsync() ?? ValueTask.CompletedTask;
}

public interface IFileDialogService
{
    Task<ChosenFile?> OpenFileAsync(
        string title, IReadOnlyList<FileFilter> filters, CancellationToken ct = default);

    Task<ChosenFile?> OpenFileInAsync(
        string title, IReadOnlyList<FileFilter> filters, string startFolder, CancellationToken ct = default) =>
        OpenFileAsync(title, filters, ct);

    Task<IReadOnlyList<ChosenFile>> OpenFilesAsync(
        string title, IReadOnlyList<FileFilter> filters, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ChosenFile>>([]);

    Task<ChosenFile?> SaveFileAsync(
        string title, string suggestedName, IReadOnlyList<FileFilter> filters,
        CancellationToken ct = default);
}

public interface ISaveNameCorrection
{
    string Corrected(string path, IReadOnlyList<FileFilter> filters);
}

public sealed class SaveNameCorrection : ISaveNameCorrection
{
    public string Corrected(string path, IReadOnlyList<FileFilter> filters)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(filters);

        var appended = Path.GetExtension(path);
        var stem = path[..^appended.Length];
        var typed = Path.GetExtension(stem);
        if (appended.Length == 0 || typed.Length == 0 || string.Equals(typed, appended, StringComparison.OrdinalIgnoreCase)) return path;

        var known = filters.SelectMany(filter => filter.Extensions).Select(extension => extension.TrimStart('*')).ToList();
        return Known(known, typed) && Known(known, appended) ? stem : path;
    }

    private bool Known(IReadOnlyList<string> extensions, string extension) =>
        extensions.Any(known => string.Equals(known, extension, StringComparison.OrdinalIgnoreCase));
}

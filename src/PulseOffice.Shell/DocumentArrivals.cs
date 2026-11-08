using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.Logging;

namespace PulseOffice.Shell;

public interface IDocumentArrivals
{
    IReadOnlyList<string> Openable(IEnumerable<string?> candidates);

    Task OpenAsync(IEnumerable<string?> candidates, CancellationToken ct = default);

    void ExpectStartup(IEnumerable<string?> candidates);

    Task OpenStartupAsync(CancellationToken ct = default);

    void AcceptDrops(Window window);
}

public sealed class DocumentArrivals(IDocumentOpenRouter router, ILogger<DocumentArrivals> logger) : IDocumentArrivals, IDisposable
{
    private readonly SemaphoreSlim _sequence = new(1, 1);

    private readonly HashSet<string> _startup = new(StringComparer.Ordinal);

    private readonly HashSet<string> _deliveredAtStartup = new(StringComparer.Ordinal);

    public void ExpectStartup(IEnumerable<string?> candidates)
    {
        foreach (var path in Openable(candidates)) _startup.Add(path);
    }

    public Task OpenStartupAsync(CancellationToken ct = default)
    {
        var pending = _startup.ToList();
        _startup.Clear();
        foreach (var path in pending) _deliveredAtStartup.Add(path);
        return OpenPathsAsync(pending, ct);
    }

    public void Dispose() => _sequence.Dispose();

    public IReadOnlyList<string> Openable(IEnumerable<string?> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        return [.. candidates
            .OfType<string>()
            .Where(candidate => candidate.Length > 0 && !candidate.StartsWith('-'))
            .Select(Path.GetFullPath)
            .Where(File.Exists)
            .Where(router.CanOpen)
            .Distinct(StringComparer.Ordinal)];
    }

    public async Task OpenAsync(IEnumerable<string?> candidates, CancellationToken ct = default)
    {
        var paths = Openable(candidates).Where(path => !_deliveredAtStartup.Remove(path)).ToList();
        foreach (var path in paths) _startup.Remove(path);
        await OpenPathsAsync(paths, ct).ConfigureAwait(true);
    }

    private async Task OpenPathsAsync(IReadOnlyList<string> paths, CancellationToken ct)
    {
        if (paths.Count == 0) return;
        await _sequence.WaitAsync(ct).ConfigureAwait(true);
        try
        {
            foreach (var path in paths)
            {
                try
                {
                    logger.LogInformation("Opening arrived document {Path}", path);
                    await router.OpenAsync(path, ct).ConfigureAwait(true);
                }
                catch (Exception failure) when (failure is not OutOfMemoryException and not OperationCanceledException)
                {
                    logger.LogError(failure, "Opening arrived document {Path} failed", path);
                }
            }
        }
        finally
        {
            _sequence.Release();
        }
    }

    public void AcceptDrops(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        DragDrop.SetAllowDrop(window, true);
        window.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            if (e.Handled) return;
            e.DragEffects = Openable(Paths(e)).Count > 0 ? DragDropEffects.Copy : DragDropEffects.None;
        });
        window.AddHandler(DragDrop.DropEvent, async (_, e) =>
        {
            if (e.Handled) return;
            var paths = Openable(Paths(e));
            if (paths.Count == 0) return;
            e.Handled = true;
            await OpenAsync(paths).ConfigureAwait(true);
        });
    }

    private IEnumerable<string?> Paths(DragEventArgs e) =>
        e.DataTransfer.TryGetFiles() is { } files ? files.Select(file => file.TryGetLocalPath()) : [];
}

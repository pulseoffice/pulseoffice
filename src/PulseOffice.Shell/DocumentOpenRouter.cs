using PulseOffice.Localization;
using PulseOffice.Platform;

namespace PulseOffice.Shell;

public interface IWorkspaceOpener
{
    IReadOnlyList<FileFilter> ReadableFilters { get; }

    bool Handles(string path);

    Task OpenAsync(string path, CancellationToken ct = default);

    Task<bool> OpenAsNewAsync(string path, CancellationToken ct = default);
}

public interface IDocumentOpenRouter
{
    bool CanOpen(string path);

    Task OpenAsync(string path, CancellationToken ct = default);

    Task OpenDialogAsync(CancellationToken ct = default);

    Task<bool> OpenAsNewAsync(string path, CancellationToken ct = default);

    Task EditAsync(string path, CancellationToken ct = default);

    OpenDocument? FindOpen(string path);
}

public sealed class DocumentOpenRouter(
    IEnumerable<IWorkspaceOpener> openers,
    IFileDialogService dialogs,
    ITextCatalog texts,
    IDocumentWorkspace workspace,
    ITemplateLibrary templates,
    IHostSystem host) : IDocumentOpenRouter
{
    private readonly IReadOnlyList<IWorkspaceOpener> _openers = [.. openers];

    private readonly List<string> _opening = [];

    public bool CanOpen(string path) => !string.IsNullOrEmpty(path) && _openers.Any(o => o.Handles(path));

    public async Task<bool> OpenAsNewAsync(string path, CancellationToken ct = default) =>
        !string.IsNullOrEmpty(path) && _openers.FirstOrDefault(o => o.Handles(path)) is { } opener
        && await opener.OpenAsNewAsync(path, ct).ConfigureAwait(true);

    public async Task OpenAsync(string path, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        await RouteAsync(path, ct).ConfigureAwait(true);
    }

    public async Task EditAsync(string path, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (AlreadyOpen(path)) return;

        var opener = _openers.FirstOrDefault(o => o.Handles(path));
        if (opener is null) return;

        var full = Path.GetFullPath(path);
        if (_opening.Any(pending => string.Equals(pending, full, PathComparison))) return;
        _opening.Add(full);
        try
        {
            await opener.OpenAsync(path, ct).ConfigureAwait(true);
        }
        finally
        {
            _opening.Remove(full);
        }
    }

    private async Task RouteAsync(string path, CancellationToken ct)
    {
        if (templates.IsTemplate(path))
        {
            await OpenAsNewAsync(path, ct).ConfigureAwait(true);
            return;
        }

        await EditAsync(path, ct).ConfigureAwait(true);
    }

    public async Task OpenDialogAsync(CancellationToken ct = default)
    {
        var filters = _openers.SelectMany(o => o.ReadableFilters).ToList();

        var file = await dialogs.OpenFileAsync(texts["dialog.open.title"], filters, ct).ConfigureAwait(true);
        if (file is null) return;

        var path = file.Path;

        await using (file.ConfigureAwait(true))
            await RouteAsync(path, ct).ConfigureAwait(true);
    }

    private StringComparison PathComparison => host.PathComparison;

    public OpenDocument? FindOpen(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var full = Path.GetFullPath(path);
        return workspace.Documents.FirstOrDefault(document => document.Path is { } open && string.Equals(Path.GetFullPath(open), full, PathComparison));
    }

    private bool AlreadyOpen(string path)
    {
        if (FindOpen(path) is not { } existing)
            return false;
        workspace.Activate(existing);
        return true;
    }
}

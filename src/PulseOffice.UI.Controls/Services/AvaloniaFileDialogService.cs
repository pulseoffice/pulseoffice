using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PulseOffice.Platform;

namespace PulseOffice.UI.Controls.Services;

public sealed class AvaloniaFileDialogService(Func<TopLevel?> topLevel, ISaveNameCorrection names, Func<string?> startFolder) : IFileDialogService
{
    public Task<ChosenFile?> OpenFileAsync(
        string title, IReadOnlyList<FileFilter> filters, CancellationToken ct = default) =>
        OpenSingleAsync(title, filters, startFolder(), ct);

    public Task<ChosenFile?> OpenFileInAsync(
        string title, IReadOnlyList<FileFilter> filters, string startFolder, CancellationToken ct = default) =>
        OpenSingleAsync(title, filters, startFolder, ct);

    private async Task<ChosenFile?> OpenSingleAsync(
        string title, IReadOnlyList<FileFilter> filters, string? folder, CancellationToken ct)
    {
        var provider = Provider();
        if (provider is null) return null;

        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = await StartLocation(provider, folder).ConfigureAwait(true),
            FileTypeFilter = [.. filters.Select(ToFileType)],
        }).ConfigureAwait(true);

        return files.Count == 0 ? null : Wrap(files[0]);
    }

    public async Task<IReadOnlyList<ChosenFile>> OpenFilesAsync(
        string title, IReadOnlyList<FileFilter> filters, CancellationToken ct = default)
    {
        var provider = Provider();
        if (provider is null) return [];

        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = true,
            SuggestedStartLocation = await StartLocation(provider).ConfigureAwait(true),
            FileTypeFilter = [.. filters.Select(ToFileType)],
        }).ConfigureAwait(true);

        return [.. files.Select(Wrap)];
    }

    public async Task<ChosenFile?> SaveFileAsync(
        string title, string suggestedName, IReadOnlyList<FileFilter> filters,
        CancellationToken ct = default)
    {
        var provider = Provider();
        if (provider is null) return null;

        var file = await provider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            SuggestedStartLocation = await StartLocation(provider).ConfigureAwait(true),
            DefaultExtension = filters.FirstOrDefault()?.Extensions.FirstOrDefault()?.TrimStart('*', '.'),
            FileTypeChoices = [.. filters.Select(ToFileType)],
        }).ConfigureAwait(true);

        if (file is null) return null;
        var chosen = Wrap(file);
        var corrected = names.Corrected(chosen.Path, filters);
        if (corrected == chosen.Path || !Path.IsPathRooted(corrected)) return chosen;

        return new ChosenFile(corrected, Path.GetFileName(corrected))
        {
            OpenRead = _ => Task.FromResult<Stream>(File.OpenRead(corrected)),
            OpenWrite = _ => Task.FromResult<Stream>(File.Create(corrected)),
            Handle = chosen.Handle,
        };
    }

    private IStorageProvider? Provider() => topLevel()?.StorageProvider;

    private Task<IStorageFolder?> StartLocation(IStorageProvider provider) => StartLocation(provider, startFolder());

    private static async Task<IStorageFolder?> StartLocation(IStorageProvider provider, string? folder) =>
        folder is { Length: > 0 } && Directory.Exists(folder)
            ? await provider.TryGetFolderFromPathAsync(folder).ConfigureAwait(true)
            : null;

    private static FilePickerFileType ToFileType(FileFilter filter) =>
        new(filter.Name) { Patterns = [.. filter.Extensions.Select(e => "*" + e)] };

    private static ChosenFile Wrap(IStorageFile file) =>
        new(file.TryGetLocalPath() ?? file.Name, file.Name)
        {
            OpenRead = async _ => await file.OpenReadAsync().ConfigureAwait(true),
            OpenWrite = async _ => await file.OpenWriteAsync().ConfigureAwait(true),

            Handle = file as IAsyncDisposable,
        };
}

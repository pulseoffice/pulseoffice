using PulseOffice.Localization;
using PulseOffice.Platform;

namespace PulseOffice.UI.Controls.Services;

public interface IFileTypeNames
{
    FileFilter Localize(FileFilter filter);
}

public sealed class FileTypeNames(ITextCatalog texts) : IFileTypeNames
{
    private static readonly string[][] Families =
    [
        ["odt", "fodt", "ott", "sxw", "stw"],
        ["ods", "fods", "ots", "sxc", "stc"],
        ["odp", "fodp", "otp", "sxi", "sti"],
        ["html", "htm", "xhtml"],
        ["tsv", "tab", "txt", "text"],
        ["jpg", "jpeg"],
        ["tif", "tiff"],
    ];

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["jpeg"] = "jpg",
        ["tiff"] = "tif",
        ["htm"] = "html",
        ["xhtml"] = "html",
        ["tab"] = "tsv",
        ["text"] = "txt",
    };

    public FileFilter Localize(FileFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var extensions = filter.Extensions.Select(Bare).Where(extension => extension.Length > 0 && extension != "*").ToList();
        if (extensions.Count == 0) return filter;

        var first = extensions[0];
        if (extensions.Select(Family).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1) return filter;

        var key = "fileType." + (Aliases.TryGetValue(first, out var alias) ? alias : first);
        if (!texts.Contains(key)) return filter;

        var shown = string.Join(", ", extensions.Distinct(StringComparer.OrdinalIgnoreCase).Select(extension => "." + extension));
        return filter with { Name = $"{texts[key]} ({shown})" };
    }

    private string Bare(string extension) => extension.TrimStart('*', '.').ToLowerInvariant();

    private string Family(string extension) =>
        Families.FirstOrDefault(family => family.Contains(extension, StringComparer.OrdinalIgnoreCase))?[0] ?? extension;
}

public sealed class LocalizedFileDialogService(IFileDialogService inner, IFileTypeNames names) : IFileDialogService
{
    public Task<ChosenFile?> OpenFileAsync(string title, IReadOnlyList<FileFilter> filters, CancellationToken ct = default) =>
        inner.OpenFileAsync(title, Localized(filters), ct);

    public Task<ChosenFile?> OpenFileInAsync(string title, IReadOnlyList<FileFilter> filters, string startFolder, CancellationToken ct = default) =>
        inner.OpenFileInAsync(title, Localized(filters), startFolder, ct);

    public Task<IReadOnlyList<ChosenFile>> OpenFilesAsync(string title, IReadOnlyList<FileFilter> filters, CancellationToken ct = default) =>
        inner.OpenFilesAsync(title, Localized(filters), ct);

    public Task<ChosenFile?> SaveFileAsync(string title, string suggestedName, IReadOnlyList<FileFilter> filters, CancellationToken ct = default) =>
        inner.SaveFileAsync(title, suggestedName, Localized(filters), ct);

    private FileFilter[] Localized(IReadOnlyList<FileFilter> filters) => [.. filters.Select(names.Localize)];
}

using Microsoft.Extensions.Logging;
using PulseOffice.Foundation.Modules;
using PulseOffice.Printing;

namespace PulseOffice.Shell;

public sealed record TemplateEntry(string Path, string Name, ModuleKind Module, DateTime Modified);

public interface ITemplateLibrary
{
    string Folder { get; }

    IReadOnlyList<string> Extensions { get; }

    IReadOnlyList<TemplateEntry> Entries();

    IDocumentOperations? ModuleFor(string path);

    bool IsTemplate(string path);

    IPrintableDocument? FirstPages(TemplateEntry entry);
}

public sealed class TemplateLibrary(
    IDocumentOperationsCatalog operations,
    Settings.IUserDataPaths paths,
    ILogger<TemplateLibrary> logger) : ITemplateLibrary
{
    public const int PreviewCapacity = 64;

    private readonly Dictionary<string, (DateTime Modified, IPrintableDocument? Preview)> _previews = new(StringComparer.Ordinal);

    private readonly LinkedList<string> _previewOrder = [];

    public string Folder => paths.Configuration("templates");

    public IReadOnlyList<string> Extensions =>
        [.. operations.All.SelectMany(ExtensionsOf).Select(extension => "." + extension)];

    public IReadOnlyList<TemplateEntry> Entries()
    {
        try
        {
            if (!Directory.Exists(Folder)) return [];

            return [.. Directory.EnumerateFiles(Folder)
                .Select(path => (Path: path, Module: ModuleFor(path)))
                .Where(found => found.Module is not null)
                .Select(found => new TemplateEntry(
                    found.Path, System.IO.Path.GetFileNameWithoutExtension(found.Path), found.Module!.Module, File.GetLastWriteTime(found.Path)))
                .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not list the templates folder");
            return [];
        }
    }

    public IDocumentOperations? ModuleFor(string path) =>
        operations.All.FirstOrDefault(module => ExtensionsOf(module).Any(extension =>
            string.Equals(System.IO.Path.GetExtension(path), "." + extension, StringComparison.OrdinalIgnoreCase)));

    public bool IsTemplate(string path) => !string.IsNullOrEmpty(path) && ModuleFor(path) is not null;

    public IPrintableDocument? FirstPages(TemplateEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (_previews.TryGetValue(entry.Path, out var cached) && cached.Modified == entry.Modified) return cached.Preview;

        IPrintableDocument? preview = null;
        try
        {
            preview = ModuleFor(entry.Path)?.PreviewFile(entry.Path) is { PageCount: > 0 } built ? built : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or FormatException or System.Xml.XmlException)
        {
            logger.LogWarning(ex, "Could not render a preview of template {Template}", entry.Name);
        }

        Remember(entry, preview);
        return preview;
    }

    private void Remember(TemplateEntry entry, IPrintableDocument? preview)
    {
        if (_previews.Remove(entry.Path)) _previewOrder.Remove(entry.Path);

        while (_previews.Count >= PreviewCapacity && _previewOrder.First is { } oldest)
        {
            _previews.Remove(oldest.Value);
            _previewOrder.RemoveFirst();
        }

        _previews[entry.Path] = (entry.Modified, preview);
        _previewOrder.AddLast(entry.Path);
    }

    private IEnumerable<string> ExtensionsOf(IDocumentOperations module) =>
        module.OfficeTemplateExtension is { } office ? [module.TemplateExtension, office] : [module.TemplateExtension];
}

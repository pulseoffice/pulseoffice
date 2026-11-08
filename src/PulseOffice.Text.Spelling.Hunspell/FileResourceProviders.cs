using Microsoft.Extensions.Logging;
using PulseOffice.Text.Spelling.Dictionaries;

namespace PulseOffice.Text.Spelling.Hunspell;

public abstract class FileResourceProvider<T> where T : class
{
    private readonly IDictionaryLocations _locations;
    private readonly ILogger _logger;
    private readonly Dictionary<string, T?> _loaded = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _gate = new();

    protected FileResourceProvider(IDictionaryLocations locations, ILogger logger, IDictionaryChanges changes)
    {
        _locations = locations;
        _logger = logger;
        changes.Changed += (_, _) => Forget();
    }

    protected abstract string Prefix { get; }

    protected abstract string Extension { get; }

    protected abstract T Load(LanguageTag language, string file);

    protected T? Find(LanguageTag language)
    {
        ArgumentNullException.ThrowIfNull(language);

        if (language == LanguageTag.None) return null;

        lock (_gate)
        {
            if (_loaded.TryGetValue(language.Canonical, out var cached)) return cached;

            T? resource = null;

            if (ResourceFiles.Find(_locations, Prefix, Extension, language) is { } file)
            {
                try
                {
                    resource = Load(language, file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning(ex, "Unreadable {Kind} file for {Language}", Prefix, language.Canonical);
                }
            }

            _loaded[language.Canonical] = resource;
            return resource;
        }
    }

    private void Forget()
    {
        lock (_gate) _loaded.Clear();
    }
}

public sealed class FileHyphenationProvider(
    IDictionaryLocations locations, ILogger<FileHyphenationProvider> logger, IDeclaredEncodingText text, IDictionaryChanges changes)
    : FileResourceProvider<IHyphenator>(locations, logger, changes), IHyphenationProvider
{
    protected override string Prefix => "hyph";

    protected override string Extension => ".dic";

    public IHyphenator? For(LanguageTag language) => Find(language);

    protected override IHyphenator Load(LanguageTag language, string file) => HyphenationPatterns.Parse(language, text.ReadLines(file));
}

public sealed class FileThesaurusProvider(
    IDictionaryLocations locations, ILogger<FileThesaurusProvider> logger, IDeclaredEncodingText text, IDictionaryChanges changes)
    : FileResourceProvider<IThesaurus>(locations, logger, changes), IThesaurusProvider
{
    protected override string Prefix => "th";

    protected override string Extension => ".dat";

    public IThesaurus? For(LanguageTag language) => Find(language);

    protected override IThesaurus Load(LanguageTag language, string file) => MyThesThesaurus.Parse(language, text.ReadLines(file));
}

public static class ResourceFiles
{
    public static string? Find(
        IDictionaryLocations locations, string prefix, string extension, LanguageTag language)
    {
        ArgumentNullException.ThrowIfNull(locations);
        ArgumentNullException.ThrowIfNull(language);

        foreach (var folder in locations.Folders)
        {
            if (!Directory.Exists(folder)) continue;

            string[] files;

            try
            {
                files = Directory.GetFiles(folder, $"{prefix}_*{extension}", SearchOption.AllDirectories);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            var exact = files.FirstOrDefault(file => Tag(file, prefix).Canonical == language.Canonical);
            if (exact is not null) return exact;

            var loose = files.FirstOrDefault(file => Tag(file, prefix).Matches(language));
            if (loose is not null) return loose;
        }

        return null;
    }

    private static LanguageTag Tag(string file, string prefix)
    {
        var name = Path.GetFileNameWithoutExtension(file)[(prefix.Length + 1)..];

        var version = name.LastIndexOf("_v", StringComparison.OrdinalIgnoreCase);
        if (version > 0) name = name[..version];

        return LanguageTag.Parse(name);
    }
}

using Microsoft.Extensions.Logging;
using WeCantSpell.Hunspell;
using PulseOffice.Text.Spelling.Dictionaries;

namespace PulseOffice.Text.Spelling.Hunspell;

public sealed class HunspellDictionary(LanguageTag language, WordList words) : ISpellDictionary
{
    public LanguageTag Language { get; } = language;

    public bool Check(string word) => !string.IsNullOrWhiteSpace(word) && words.Check(word);

    public IReadOnlyList<string> Suggest(string word) =>
        string.IsNullOrWhiteSpace(word) ? [] : [.. words.Suggest(word)];
}

public sealed class HunspellDictionaryProvider : ISpellDictionaryProvider
{
    private readonly IDictionaryLocations _locations;
    private readonly ILogger<HunspellDictionaryProvider> _logger;
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ISpellDictionary?> _loaded = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _gate = new();

    public HunspellDictionaryProvider(
        IDictionaryLocations locations, ILogger<HunspellDictionaryProvider> logger, IDictionaryChanges changes)
    {
        _locations = locations;
        _logger = logger;
        changes.Changed += (_, _) => Refresh();

        Refresh();
    }

    public IReadOnlyList<LanguageTag> Available
    {
        get { lock (_gate) return [.. _files.Keys.Select(LanguageTag.Parse).OrderBy(tag => tag.Canonical)]; }
    }

    public void Refresh()
    {
        lock (_gate)
        {
            _files.Clear();
            _loaded.Clear();

            foreach (var folder in _locations.Folders)
                foreach (var file in Affixes(folder))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (!File.Exists(Path.ChangeExtension(file, ".dic"))) continue;

                    _files.TryAdd(LanguageTag.Parse(name).Canonical, file);
                }

            _logger.LogInformation("Spelling dictionaries found: {Count}", _files.Count);
        }
    }

    public ISpellDictionary? For(LanguageTag language)
    {
        ArgumentNullException.ThrowIfNull(language);

        if (language == LanguageTag.None) return null;

        lock (_gate)
        {
            if (Resolve(language) is not { } key) return null;

            if (_loaded.TryGetValue(key, out var cached)) return cached;

            ISpellDictionary? dictionary = null;

            try
            {
                var affix = _files[key];
                dictionary = new HunspellDictionary(
                    LanguageTag.Parse(key),
                    WordList.CreateFromFiles(Path.ChangeExtension(affix, ".dic"), affix));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException
                or InvalidDataException or ArgumentException or InvalidOperationException or IndexOutOfRangeException)
            {
                _logger.LogWarning(ex, "Unreadable dictionary for {Language}", key);
            }

            _loaded[key] = dictionary;
            return dictionary;
        }
    }

    private string? Resolve(LanguageTag language)
    {
        if (_files.ContainsKey(language.Canonical)) return language.Canonical;

        return _files.Keys.FirstOrDefault(key => LanguageTag.Parse(key).Matches(language));
    }

    private IEnumerable<string> Affixes(string folder)
    {
        if (!Directory.Exists(folder)) return [];

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            MaxRecursionDepth = 8,
        };

        try
        {
            return [.. Directory.EnumerateFiles(folder, "*.aff", options)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            _logger.LogWarning(ex, "Could not scan the dictionary folder {Folder}", folder);
            return [];
        }
    }
}

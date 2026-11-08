using System.Text.Json;

namespace PulseOffice.Text.Spelling;

public sealed record AutoCorrectLists(
    IReadOnlyDictionary<string, string> Replacements,
    IReadOnlyCollection<string> Abbreviations,
    IReadOnlyCollection<string> TwoCapitalWords)
{
    public static readonly AutoCorrectLists Empty = new(new Dictionary<string, string>(), [], []);
}

public interface IAutoCorrectSource
{
    EntryOrigin Origin { get; }

    int Priority { get; }

    IReadOnlyCollection<string> Languages { get; }

    AutoCorrectLists? Lists(string language);
}

public sealed class AutoCorrectEntryNormalizer
{
    private const string Wildcard = ".*";
    private const int MaximumKeyLength = 256;
    private const int MaximumValueLength = 4096;

    public (string From, string To)? Replacement(string? shortcut, string? value)
    {
        if (shortcut is null || value is null) return null;

        var key = shortcut;
        if (key.StartsWith(Wildcard, StringComparison.Ordinal)) key = key[Wildcard.Length..];
        if (key.EndsWith(Wildcard, StringComparison.Ordinal)) key = key[..^Wildcard.Length];

        if (key.Length is 0 or > MaximumKeyLength || value.Length > MaximumValueLength) return null;
        if (key.Contains(Wildcard, StringComparison.Ordinal) || key.Any(char.IsWhiteSpace)) return null;
        if (string.Equals(key, value, StringComparison.Ordinal)) return null;

        return (key, value);
    }

    public string? Word(string? word)
    {
        var trimmed = word?.Trim();
        return trimmed is { Length: > 0 and <= MaximumKeyLength } && !trimmed.Any(char.IsWhiteSpace) ? trimmed : null;
    }

    public AutoCorrectLists Lists(
        IEnumerable<(string? Shortcut, string? Value)> replacements,
        IEnumerable<string?> sentenceExceptions,
        IEnumerable<string?> wordExceptions)
    {
        var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (shortcut, value) in replacements)
            if (Replacement(shortcut, value) is { } entry)
                entries.TryAdd(entry.From, entry.To);

        return new AutoCorrectLists(entries, Words(sentenceExceptions), Words(wordExceptions));
    }

    private IReadOnlyCollection<string> Words(IEnumerable<string?> words) =>
        [.. words.Select(Word).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase)];
}

public interface IAutoCorrectJsonReader
{
    AutoCorrectLists? Read(string path);
}

public sealed class AutoCorrectJsonReader(AutoCorrectEntryNormalizer normalizer) : IAutoCorrectJsonReader
{
    private const string Format = "pulseoffice.autocorrect";
    private const long MaximumFileBytes = 16L * 1024 * 1024;

    public AutoCorrectLists? Read(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaximumFileBytes) return null;

            using var stream = info.OpenRead();
            using var document = JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 8 });
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("format", out var format)
                || format.ValueKind != JsonValueKind.String
                || format.GetString() != Format)
                return null;

            return normalizer.Lists(Pairs(root, "replacements"), Strings(root, "sentenceExceptions"), Strings(root, "wordExceptions"));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private IEnumerable<(string?, string?)> Pairs(JsonElement root, string name) =>
        root.TryGetProperty(name, out var pairs) && pairs.ValueKind == JsonValueKind.Object
            ? [.. pairs.EnumerateObject().Select(pair => ((string?)pair.Name, pair.Value.ValueKind == JsonValueKind.String ? pair.Value.GetString() : null))]
            : [];

    private IEnumerable<string?> Strings(JsonElement root, string name) =>
        root.TryGetProperty(name, out var items) && items.ValueKind == JsonValueKind.Array
            ? [.. items.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : null)]
            : [];
}

public sealed class BundledAutoCorrectSource : IAutoCorrectSource
{
    private const string Extension = ".json";
    private const string Common = "common";

    private readonly string _folder;
    private readonly IAutoCorrectJsonReader _reader;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, AutoCorrectLists?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyCollection<string>? _languages;

    public BundledAutoCorrectSource(string folder, IAutoCorrectJsonReader reader, EntryOrigin origin, int priority)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        _folder = folder;
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        Origin = origin;
        Priority = priority;
    }

    public EntryOrigin Origin { get; }

    public int Priority { get; }

    public IReadOnlyCollection<string> Languages
    {
        get
        {
            lock (_gate) return _languages ??= Scan();
        }
    }

    public AutoCorrectLists? Lists(string language)
    {
        var own = Languages.FirstOrDefault(tag => string.Equals(tag, language, StringComparison.OrdinalIgnoreCase));
        var parts = new[] { own is null ? null : Load(own), Load(Common) }.OfType<AutoCorrectLists>().ToList();

        return parts.Count switch
        {
            0 => null,
            1 => parts[0],
            _ => new AutoCorrectLists(
                parts.SelectMany(part => part.Replacements)
                    .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.OrdinalIgnoreCase),
                [.. parts.SelectMany(part => part.Abbreviations).Distinct(StringComparer.OrdinalIgnoreCase)],
                [.. parts.SelectMany(part => part.TwoCapitalWords).Distinct(StringComparer.OrdinalIgnoreCase)]),
        };
    }

    private AutoCorrectLists? Load(string file)
    {
        lock (_gate)
        {
            if (_cache.TryGetValue(file, out var cached)) return cached;

            var path = Path.Combine(_folder, file + Extension);
            var lists = File.Exists(path) ? _reader.Read(path) : null;
            _cache[file] = lists;
            return lists;
        }
    }

    private IReadOnlyCollection<string> Scan()
    {
        try
        {
            return Directory.Exists(_folder)
                ? [.. Directory.EnumerateFiles(_folder, "*" + Extension)
                    .Select(Path.GetFileNameWithoutExtension)
                    .OfType<string>()
                    .Where(name => name != Common)
                    .Order(StringComparer.Ordinal)]
                : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}

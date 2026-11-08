namespace PulseOffice.Documents.Indexes;

public enum IndexKind
{
    Contents,
    Alphabetical,
    Illustrations,
    Tables,
    Bibliography,
    User,
    Objects,
}

[Flags]
public enum IndexObjectSources
{
    None = 0,
    Charts = 1,
    Formulas = 2,
    Drawings = 4,
    Spreadsheets = 8,
    Other = 16,
    All = Charts | Formulas | Drawings | Spreadsheets | Other,
}

public enum CaptionDisplay
{
    Caption,
    CategoryAndNumber,
    Text,
}

public sealed record IndexDefinition(IndexKind Kind)
{
    public string Title { get; init; } = string.Empty;

    public int Levels { get; init; } = 10;

    public bool FromOutline { get; init; } = true;

    public bool FromMarks { get; init; } = true;

    public string? Sequence { get; init; }

    public CaptionDisplay Captions { get; init; } = CaptionDisplay.Caption;

    public bool Hyperlinks { get; init; } = true;

    public bool PageNumbers { get; init; } = true;

    public bool CombineEntries { get; init; } = true;

    public bool LetterSeparators { get; init; }

    public bool CaseSensitive { get; init; }

    public string? UserName { get; init; }

    public bool SortByPosition { get; init; } = true;

    public IndexObjectSources ObjectSources { get; init; } = IndexObjectSources.All;

    public IndexLevelStyles LevelStyles { get; init; } = IndexLevelStyles.Defaults;
}

public sealed class IndexLevelStyles : IEquatable<IndexLevelStyles>
{
    private readonly SortedDictionary<int, string> _entries;

    private IndexLevelStyles(string? heading, SortedDictionary<int, string> entries)
    {
        Heading = heading;
        _entries = entries;
    }

    public static IndexLevelStyles Defaults { get; } = new(null, []);

    public string? Heading { get; }

    public IReadOnlyDictionary<int, string> Entries => _entries;

    public bool IsDefault => Heading is null && _entries.Count == 0;

    public string? Entry(int level) => _entries.TryGetValue(level, out var name) ? name : null;

    public IndexLevelStyles WithHeading(string? name) => new(name, _entries);

    public IndexLevelStyles WithEntry(int level, string? name)
    {
        var entries = new SortedDictionary<int, string>(_entries);
        if (name is null) entries.Remove(level);
        else entries[level] = name;
        return new IndexLevelStyles(Heading, entries);
    }

    public IndexLevelStyles Map(Func<string, string?> rename)
    {
        var entries = new SortedDictionary<int, string>();
        foreach (var (level, name) in _entries)
        {
            if (rename(name) is { } mapped) entries[level] = mapped;
        }
        return new IndexLevelStyles(Heading is null ? null : rename(Heading), entries);
    }

    public bool Mentions(string name) => Heading == name || _entries.Values.Contains(name, StringComparer.Ordinal);

    public bool Equals(IndexLevelStyles? other) =>
        other is not null && Heading == other.Heading && _entries.Count == other._entries.Count && _entries.All(pair => other._entries.TryGetValue(pair.Key, out var name) && name == pair.Value);

    public override bool Equals(object? obj) => Equals(obj as IndexLevelStyles);

    public override int GetHashCode() => _entries.Aggregate(Heading?.GetHashCode(StringComparison.Ordinal) ?? 0, (hash, pair) => HashCode.Combine(hash, pair.Key, pair.Value));
}

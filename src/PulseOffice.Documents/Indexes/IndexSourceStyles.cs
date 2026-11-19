namespace PulseOffice.Documents.Indexes;

public sealed class IndexSourceStyles : IEquatable<IndexSourceStyles>
{
    private readonly SortedDictionary<string, int> _levels;

    private IndexSourceStyles(SortedDictionary<string, int> levels) => _levels = levels;

    public static IndexSourceStyles None { get; } = new(new SortedDictionary<string, int>(StringComparer.Ordinal));

    public IReadOnlyDictionary<string, int> Levels => _levels;

    public bool IsEmpty => _levels.Count == 0;

    public int? LevelOf(string? style) => style is not null && _levels.TryGetValue(style, out var level) ? level : null;

    public IndexSourceStyles With(string style, int? level)
    {
        var levels = new SortedDictionary<string, int>(_levels, StringComparer.Ordinal);
        if (level is { } value) levels[style] = Math.Clamp(value, 1, 10);
        else levels.Remove(style);
        return new IndexSourceStyles(levels);
    }

    public IndexSourceStyles Map(Func<string, string?> rename)
    {
        var levels = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (name, level) in _levels)
        {
            if (rename(name) is { } mapped) levels[mapped] = level;
        }
        return new IndexSourceStyles(levels);
    }

    public bool Equals(IndexSourceStyles? other) =>
        other is not null && _levels.Count == other._levels.Count && _levels.All(pair => other._levels.TryGetValue(pair.Key, out var level) && level == pair.Value);

    public override bool Equals(object? obj) => Equals(obj as IndexSourceStyles);

    public override int GetHashCode() => _levels.Aggregate(0, (hash, pair) => HashCode.Combine(hash, pair.Key.GetHashCode(StringComparison.Ordinal), pair.Value));
}

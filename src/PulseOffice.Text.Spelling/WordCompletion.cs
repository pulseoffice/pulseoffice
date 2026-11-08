namespace PulseOffice.Text.Spelling;

public interface IWordCompletion
{
    int MinimumLength { get; }

    IReadOnlyCollection<string> Words { get; }

    void Collect(string text);

    string? Suggest(string prefix);

    void Clear();
}

public sealed class WordCompletion : IWordCompletion
{
    private const int Capacity = 1000;
    private const int MinimumPrefix = 3;
    private const int MaximumLength = 64;

    private readonly Lock _gate = new();
    private readonly Dictionary<string, Usage> _counts = new(StringComparer.CurrentCultureIgnoreCase);
    private long _clock;

    public int MinimumLength => 8;

    public IReadOnlyCollection<string> Words
    {
        get
        {
            lock (_gate) return [.. _counts.Keys.Order(StringComparer.CurrentCultureIgnoreCase)];
        }
    }

    public void Collect(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        lock (_gate)
        {
            foreach (var word in Split(text).Where(word => word.Length >= MinimumLength && word.Length <= MaximumLength))
            {
                _counts[word] = new Usage(_counts.TryGetValue(word, out var usage) ? usage.Count + 1 : 1, ++_clock);
                if (_counts.Count > Capacity) _counts.Remove(Weakest(word));
            }
        }
    }

    public string? Suggest(string prefix)
    {
        if (string.IsNullOrEmpty(prefix) || prefix.Length < MinimumPrefix) return null;
        lock (_gate)
            return _counts
                .Where(pair => pair.Key.Length > prefix.Length && pair.Key.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase))
                .OrderByDescending(pair => pair.Value.Count)
                .ThenBy(pair => pair.Key, StringComparer.CurrentCultureIgnoreCase)
                .Select(pair => pair.Key)
                .FirstOrDefault();
    }

    public void Clear()
    {
        lock (_gate) _counts.Clear();
    }

    private string Weakest(string kept) =>
        _counts.Where(pair => !string.Equals(pair.Key, kept, StringComparison.CurrentCultureIgnoreCase))
            .MinBy(pair => (pair.Value.Count, pair.Value.Stamp)).Key;

    private readonly record struct Usage(int Count, long Stamp);

    private IEnumerable<string> Split(string text)
    {
        var start = -1;
        for (var index = 0; index <= text.Length; index++)
        {
            var letter = index < text.Length && (char.IsLetter(text[index]) || text[index] is '\'' or '’' && start >= 0);
            if (letter && start < 0) start = index;
            else if (!letter && start >= 0)
            {
                yield return text[start..index].TrimEnd('\'', '’');
                start = -1;
            }
        }
    }
}

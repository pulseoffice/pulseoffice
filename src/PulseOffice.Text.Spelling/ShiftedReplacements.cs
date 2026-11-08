namespace PulseOffice.Text.Spelling;

public sealed class ShiftedReplacements<TKey> where TKey : notnull
{
    private readonly Dictionary<TKey, List<(int At, int Delta)>> _shifts = [];

    public string? Replace(TKey place, string text, WordSpan word, string replacement)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(replacement);

        var shifts = _shifts.TryGetValue(place, out var known) ? known : [];
        var start = word.Start + shifts.Where(shift => shift.At <= word.Start).Sum(shift => shift.Delta);

        if (start < 0 || start + word.Length > text.Length
            || string.CompareOrdinal(text, start, word.Text, 0, word.Length) != 0)
            return null;

        shifts.Add((word.Start, replacement.Length - word.Length));
        _shifts[place] = shifts;

        return string.Concat(text.AsSpan(0, start), replacement, text.AsSpan(start + word.Length));
    }
}

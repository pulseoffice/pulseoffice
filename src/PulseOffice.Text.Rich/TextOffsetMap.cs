namespace PulseOffice.Text.Rich;

public sealed class TextOffsetMap
{
    private readonly int _prefix;
    private readonly int _beforeChangedEnd;
    private readonly int _delta;
    private readonly int _afterLength;

    public static readonly TextOffsetMap Identity = new(string.Empty, string.Empty);

    public TextOffsetMap(string before, string after)
    {
        var prefix = 0;
        var limit = Math.Min(before.Length, after.Length);
        while (prefix < limit && before[prefix] == after[prefix]) prefix++;

        var suffix = 0;
        while (suffix < limit - prefix && before[before.Length - 1 - suffix] == after[after.Length - 1 - suffix]) suffix++;

        _prefix = prefix;
        _beforeChangedEnd = before.Length - suffix;
        _delta = after.Length - before.Length;
        _afterLength = after.Length;
    }

    public int Map(int offset)
    {
        if (offset <= _prefix) return Math.Min(offset, _afterLength);
        if (offset >= _beforeChangedEnd) return Math.Clamp(offset + _delta, 0, _afterLength);
        return Math.Clamp(_beforeChangedEnd + _delta, 0, _afterLength);
    }
}

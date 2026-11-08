namespace PulseOffice.Sheets.Engine.Evaluation.Functions;

public abstract class TextUnits
{
    protected abstract int WidthOf(string text, int index, out int span);

    public int Length(string text)
    {
        var total = 0;
        for (var index = 0; index < text.Length;)
        {
            total += WidthOf(text, index, out var span);
            index += span;
        }
        return total;
    }

    public string Left(string text, int units) => Slice(text, 0, units);

    public string Right(string text, int units)
    {
        var length = Length(text);
        var from = Math.Max(0, length - units);
        return Slice(text, from, length - from);
    }

    public string Middle(string text, int start, int length) => Slice(text, start - 1, length);

    public string Replace(string text, int start, int length, string replacement)
    {
        var from = start - 1;
        var resume = from + length;
        return Slice(text, 0, from) + replacement + Slice(text, resume, Math.Max(0, Length(text) - resume));
    }

    private string Slice(string text, int from, int count)
    {
        var builder = new System.Text.StringBuilder();
        var until = (long)from + count;
        var offset = 0;
        for (var index = 0; index < text.Length && offset < until;)
        {
            var width = WidthOf(text, index, out var span);
            var overlap = (int)Math.Min(offset + width, until) - Math.Max(offset, from);
            if (overlap == width) builder.Append(text, index, span);
            else if (overlap > 0) builder.Append(' ', overlap);
            offset += width;
            index += span;
        }
        return builder.ToString();
    }

    public int? Find(string haystack, int start, Func<string, int, int> search)
    {
        if (start < 1 || start > Length(haystack) + 1) return null;
        var found = search(haystack, CharIndexAt(haystack, start - 1));
        return found < 0 ? null : UnitsBefore(haystack, found) + 1;
    }

    private int CharIndexAt(string text, int units)
    {
        var taken = 0;
        var index = 0;
        while (index < text.Length && taken < units)
        {
            taken += WidthOf(text, index, out var span);
            index += span;
        }
        return index;
    }

    private int UnitsBefore(string text, int charIndex)
    {
        var taken = 0;
        for (var index = 0; index < charIndex && index < text.Length;)
        {
            taken += WidthOf(text, index, out var span);
            index += span;
        }
        return taken;
    }
}

public sealed class CharacterUnits : TextUnits
{
    protected override int WidthOf(string text, int index, out int span)
    {
        span = char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]) ? 2 : 1;
        return 1;
    }
}

public sealed class ByteUnits : TextUnits
{
    protected override int WidthOf(string text, int index, out int span)
    {
        if (char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
        {
            span = 2;
            return 2;
        }
        span = 1;
        return IsWide(text[index]) ? 2 : 1;
    }

    private bool IsWide(char character) => character switch
    {
        >= 'ᄀ' and <= 'ᅟ' => true,
        >= '⺀' and <= '꓏' => true,
        >= '가' and <= '힣' => true,
        >= '豈' and <= '﫿' => true,
        >= '︰' and <= '﹏' => true,
        >= '＀' and <= '｠' => true,
        >= '￠' and <= '￦' => true,
        _ => false,
    };
}

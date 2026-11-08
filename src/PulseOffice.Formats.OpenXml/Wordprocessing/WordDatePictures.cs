using System.Globalization;
using System.Text;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public interface IWordDatePictures
{
    string ToWord(string format);

    string FromWord(string picture);
}

public sealed class WordDatePictures : IWordDatePictures
{
    private readonly DateTimeFormatInfo _invariant = CultureInfo.InvariantCulture.DateTimeFormat;

    public string ToWord(string format)
    {
        var pattern = format.Length == 1 ? Standard(format[0]) ?? format : format;
        var builder = new StringBuilder(pattern.Length + 8);
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c is '\'' or '"')
            {
                var end = pattern.IndexOf(c, i + 1);
                var literal = end < 0 ? pattern[(i + 1)..] : pattern[(i + 1)..end];
                builder.Append('\'').Append(literal).Append('\'');
                i = end < 0 ? pattern.Length : end;
                continue;
            }
            if (c == '\\' && i + 1 < pattern.Length)
            {
                builder.Append('\'').Append(pattern[++i]).Append('\'');
                continue;
            }
            if (c == '%') continue;
            if (c == 't')
            {
                var run = Run(pattern, i, c);
                builder.Append(run >= 2 ? "AM/PM" : "A/P");
                i += run - 1;
                continue;
            }
            if (c is 'f' or 'F' or 'K' or 'z' or 'g')
            {
                i += Run(pattern, i, c) - 1;
                continue;
            }
            builder.Append(c);
        }
        return builder.ToString().Trim();
    }

    public string FromWord(string picture)
    {
        var builder = new StringBuilder(picture.Length + 4);
        for (var i = 0; i < picture.Length; i++)
        {
            var c = picture[i];
            if (c == '\'')
            {
                var end = picture.IndexOf('\'', i + 1);
                var literal = end < 0 ? picture[(i + 1)..] : picture[(i + 1)..end];
                builder.Append('\'').Append(literal).Append('\'');
                i = end < 0 ? picture.Length : end;
                continue;
            }
            if (Designator(picture, i) is { } designator)
            {
                builder.Append(designator.Format);
                i += designator.Length - 1;
                continue;
            }
            if (c is 'D') builder.Append('d');
            else if (c is 'Y') builder.Append('y');
            else if (c is '%' or '\\') builder.Append('\\').Append(c);
            else if (char.IsLetter(c) && c is not ('d' or 'M' or 'y' or 'h' or 'H' or 'm' or 's' or 'S'))
                builder.Append('\\').Append(c);
            else builder.Append(c == 'S' ? 's' : c);
        }
        return builder.Length == 1 ? "%" + builder : builder.ToString();
    }

    private (string Format, int Length)? Designator(string picture, int at)
    {
        foreach (var (word, format) in new[] { ("AM/PM", "tt"), ("A/P", "t") })
            if (string.Compare(picture, at, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) == 0) return (format, word.Length);
        return null;
    }

    private int Run(string pattern, int at, char c)
    {
        var end = at;
        while (end < pattern.Length && pattern[end] == c) end++;
        return end - at;
    }

    private string? Standard(char format) => format switch
    {
        'd' => _invariant.ShortDatePattern,
        'D' => _invariant.LongDatePattern,
        't' => _invariant.ShortTimePattern,
        'T' => _invariant.LongTimePattern,
        'f' => _invariant.LongDatePattern + " " + _invariant.ShortTimePattern,
        'F' => _invariant.FullDateTimePattern,
        'g' => _invariant.ShortDatePattern + " " + _invariant.ShortTimePattern,
        'G' => _invariant.ShortDatePattern + " " + _invariant.LongTimePattern,
        'M' or 'm' => _invariant.MonthDayPattern,
        'Y' or 'y' => _invariant.YearMonthPattern,
        _ => null,
    };
}

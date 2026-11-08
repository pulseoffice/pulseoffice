using System.Globalization;
using System.Text;

namespace PulseOffice.Sheets.Formatting;

internal sealed record DateTimeMoment(DateTime Date, double TotalSeconds, bool TwelveHour, bool Negative, CultureInfo Culture)
{
    public double SecondsOfDay => TotalSeconds - Math.Floor(TotalSeconds / 86400) * 86400;

    public int Hour => (int)Math.Floor(SecondsOfDay / 3600);

    public int Minute => (int)Math.Floor(SecondsOfDay / 60) % 60;

    public int Second => (int)Math.Floor(SecondsOfDay) % 60;
}

internal abstract class DateTimeToken
{
    public abstract void Render(DateTimeMoment moment, StringBuilder text);
}

internal sealed class LiteralDateToken(string text) : DateTimeToken
{
    public override void Render(DateTimeMoment moment, StringBuilder output) => output.Append(text);
}

internal sealed class YearToken(int length) : DateTimeToken
{
    public override void Render(DateTimeMoment moment, StringBuilder text) =>
        text.Append(length <= 2
            ? (moment.Date.Year % 100).ToString("00", CultureInfo.InvariantCulture)
            : moment.Date.Year.ToString("0000", CultureInfo.InvariantCulture));
}

internal sealed class MonthToken(int length) : DateTimeToken
{
    public int Length => length;

    public override void Render(DateTimeMoment moment, StringBuilder text)
    {
        var format = moment.Culture.DateTimeFormat;
        var month = moment.Date.Month;
        text.Append(length switch
        {
            1 => month.ToString(CultureInfo.InvariantCulture),
            2 => month.ToString("00", CultureInfo.InvariantCulture),
            3 => format.GetAbbreviatedMonthName(month),
            4 => format.GetMonthName(month),
            _ => format.GetMonthName(month)[..1],
        });
    }
}

internal sealed class DayToken(int length) : DateTimeToken
{
    public override void Render(DateTimeMoment moment, StringBuilder text)
    {
        var format = moment.Culture.DateTimeFormat;
        text.Append(length switch
        {
            1 => moment.Date.Day.ToString(CultureInfo.InvariantCulture),
            2 => moment.Date.Day.ToString("00", CultureInfo.InvariantCulture),
            3 => format.GetAbbreviatedDayName(moment.Date.DayOfWeek),
            _ => format.GetDayName(moment.Date.DayOfWeek),
        });
    }
}

internal sealed class HourToken(int length) : DateTimeToken
{
    public override void Render(DateTimeMoment moment, StringBuilder text)
    {
        var hour = moment.Hour;
        if (moment.TwelveHour) hour = hour % 12 == 0 ? 12 : hour % 12;
        text.Append(hour.ToString(length >= 2 ? "00" : "0", CultureInfo.InvariantCulture));
    }
}

internal sealed class MinuteToken(int length) : DateTimeToken
{
    public override void Render(DateTimeMoment moment, StringBuilder text) =>
        text.Append(moment.Minute.ToString(length >= 2 ? "00" : "0", CultureInfo.InvariantCulture));
}

internal sealed class SecondToken(int length) : DateTimeToken
{
    public override void Render(DateTimeMoment moment, StringBuilder text) =>
        text.Append(moment.Second.ToString(length >= 2 ? "00" : "0", CultureInfo.InvariantCulture));
}

internal sealed class FractionalSecondToken(int digits) : DateTimeToken
{
    public int Digits => digits;

    public override void Render(DateTimeMoment moment, StringBuilder text)
    {
        var fraction = moment.SecondsOfDay - Math.Floor(moment.SecondsOfDay);
        var value = (long)Math.Round(fraction * Math.Pow(10, digits));
        text.Append(moment.Culture.NumberFormat.NumberDecimalSeparator)
            .Append(value.ToString(new string('0', digits), CultureInfo.InvariantCulture));
    }
}

internal sealed class AmPmToken(bool shortForm, bool lower) : DateTimeToken
{
    public override void Render(DateTimeMoment moment, StringBuilder text)
    {
        var marker = moment.Hour < 12 ? "AM" : "PM";
        if (shortForm) marker = marker[..1];
        text.Append(lower ? marker.ToLowerInvariant() : marker);
    }
}

internal sealed class ElapsedToken(char unit, int length) : DateTimeToken
{
    public char Unit => unit;

    public override void Render(DateTimeMoment moment, StringBuilder text)
    {
        var divisor = unit switch { 'h' => 3600d, 'm' => 60d, _ => 1d };
        var value = (long)Math.Floor(moment.TotalSeconds / divisor);
        if (moment.Negative) text.Append('-');
        text.Append(value.ToString(new string('0', length), CultureInfo.InvariantCulture));
    }
}

internal abstract class DateTimeTokenRule
{
    public abstract DateTimeToken? Match(string code, int index, out int length);
}

internal sealed class KeywordRule(string keyword, Func<string, DateTimeToken> create) : DateTimeTokenRule
{
    public override DateTimeToken? Match(string code, int index, out int length)
    {
        length = keyword.Length;
        return index + keyword.Length <= code.Length &&
               string.Compare(code, index, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase) == 0
            ? create(code.Substring(index, keyword.Length))
            : null;
    }
}

internal sealed class ElapsedRule : DateTimeTokenRule
{
    public override DateTimeToken? Match(string code, int index, out int length)
    {
        length = 0;
        if (code[index] != '[') return null;

        var close = code.IndexOf(']', index);
        if (close < 0) return null;

        var content = code[(index + 1)..close];
        if (content.Length == 0 || content.Any(c => c != content[0]) || content[0] is not ('h' or 'm' or 's')) return null;

        length = close - index + 1;
        return new ElapsedToken(content[0], content.Length);
    }
}

internal sealed class LetterRunRule(char letter, Func<int, DateTimeToken> create) : DateTimeTokenRule
{
    public override DateTimeToken? Match(string code, int index, out int length)
    {
        length = 0;
        while (index + length < code.Length && char.ToLowerInvariant(code[index + length]) == letter) length++;
        return length == 0 ? null : create(length);
    }
}

internal sealed class FractionalSecondRule : DateTimeTokenRule
{
    public override DateTimeToken? Match(string code, int index, out int length)
    {
        length = 0;
        if (code[index] != '.') return null;

        var zeros = 0;
        while (index + 1 + zeros < code.Length && code[index + 1 + zeros] == '0') zeros++;
        if (zeros == 0) return null;

        length = zeros + 1;
        return new FractionalSecondToken(zeros);
    }
}

internal sealed class DateTimeTokenizer
{
    private readonly IReadOnlyList<DateTimeTokenRule> _rules =
    [
        new KeywordRule("AM/PM", text => new AmPmToken(false, char.IsLower(text[0]))),
        new KeywordRule("A/P", text => new AmPmToken(true, char.IsLower(text[0]))),
        new ElapsedRule(),
        new FractionalSecondRule(),
        new LetterRunRule('y', length => new YearToken(length)),
        new LetterRunRule('m', length => new MonthToken(length)),
        new LetterRunRule('d', length => new DayToken(length)),
        new LetterRunRule('h', length => new HourToken(length)),
        new LetterRunRule('s', length => new SecondToken(length)),
    ];

    public IReadOnlyList<DateTimeToken> Tokenize(IReadOnlyList<FormatToken> source)
    {
        var tokens = new List<DateTimeToken>();

        foreach (var token in Grouped(source))
        {
            if (token.IsLiteral)
            {
                tokens.Add(new LiteralDateToken(token.Text));
                continue;
            }

            var code = token.Text;
            for (var index = 0; index < code.Length;)
            {
                var matched = MatchAt(code, index, out var length);
                if (matched is null)
                {
                    tokens.Add(new LiteralDateToken(code[index].ToString()));
                    index++;
                    continue;
                }

                tokens.Add(matched);
                index += length;
            }
        }

        return ResolveMinutes(tokens);
    }

    private DateTimeToken? MatchAt(string code, int index, out int length)
    {
        foreach (var rule in _rules)
            if (rule.Match(code, index, out length) is { } token)
                return token;

        length = 0;
        return null;
    }

    private IEnumerable<FormatToken> Grouped(IReadOnlyList<FormatToken> source)
    {
        var code = new StringBuilder();
        foreach (var token in source)
        {
            if (!token.IsLiteral)
            {
                code.Append(token.Text);
                continue;
            }

            if (code.Length > 0) yield return new FormatToken(code.ToString(), false);
            code.Clear();
            yield return token;
        }

        if (code.Length > 0) yield return new FormatToken(code.ToString(), false);
    }

    private List<DateTimeToken> ResolveMinutes(List<DateTimeToken> tokens)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i] is not MonthToken { Length: <= 2 } month) continue;

            var previous = tokens.Take(i).LastOrDefault(t => t is not LiteralDateToken);
            var next = tokens.Skip(i + 1).FirstOrDefault(t => t is not LiteralDateToken);

            if (previous is HourToken or ElapsedToken { Unit: 'h' } || next is SecondToken or ElapsedToken { Unit: 's' })
                tokens[i] = new MinuteToken(month.Length);
        }

        return tokens;
    }
}

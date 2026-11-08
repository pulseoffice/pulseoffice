using System.Text;
using System.Globalization;
using PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;
using PulseOffice.Sheets.Values;
using M = System.Math;
using F = PulseOffice.Sheets.Engine.Evaluation.Functions.FunctionBuilder;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class TextFunctions(NumericText numbers, IDateSystem dates) : IFunctionProvider
{
    private readonly CharacterUnits _characters = new();

    public string Category => "Testo";

    public IEnumerable<FunctionDefinition> GetFunctions() => new FunctionBuilder(Category)
        .Text1("LEN", t => CellValue.Number(_characters.Length(t)))
        .Text1("UPPER", t => CellValue.Text(t.ToUpperInvariant()))
        .Text1("LOWER", t => CellValue.Text(t.ToLowerInvariant()))
        .Text1("PROPER", t => CellValue.Text(Proper(t)))

        .Text1("TRIM", t => CellValue.Text(string.Join(' ', t.Split(' ', StringSplitOptions.RemoveEmptyEntries))))

        .Text1("CLEAN", t => CellValue.Text(string.Concat(t.EnumerateRunes().Where(rune => !Rune.IsControl(rune) && (rune.Value & 0xFFFE) != 0xFFFE && rune.Value is not (>= 0xFDD0 and <= 0xFDEF)).Select(rune => rune.ToString()))))

        .Text1("CODE", t => t.Length == 0 ? F.Invalid() : CellValue.Number(t[0]))
        .Numeric1("CHAR", x => x is < 1 or >= 256 ? F.Invalid() : CellValue.Text(((char)(int)x).ToString()))
        .Numeric1("UNICHAR", x => x is < 1 or >= 0x110000 ? F.Invalid() : x is >= 0xD800 and < 0xE000 ? CellValue.FromError(CellError.NotAvailable) : CellValue.Text(char.ConvertFromUtf32((int)x)))
        .Text1("UNICODE", t => t.Length == 0 ? F.Invalid() : CellValue.Number(Rune.TryGetRuneAt(t, 0, out var rune) ? rune.Value : t[0]))

        .Raw("LEFT", 1, 2, r => Slice(r, fromEnd: false))
        .Raw("RIGHT", 1, 2, r => Slice(r, fromEnd: true))

        .Raw("MID", 3, 3, r =>
        {
            var text = r.Text(0);
            var start = r.Integer(1);
            var length = r.Integer(2);
            if (r.HasError) return r.Error;
            if (start < 1 || length < 0) return F.Invalid();
            return CellValue.Text(_characters.Middle(text, start, length));
        })

        .Raw("CONCAT", 1, int.MaxValue, r => Join(r, separator: null, skipEmpty: false))
        .Lifted("CONCATENATE", 1, int.MaxValue, _ => true, r =>
        {
            var parts = Enumerable.Range(0, r.Count).Select(index => r.Text(index)).ToList();
            if (r.HasError) return r.Error;
            return ResultLimits.TextFits(parts.Sum(part => (long)part.Length)) ? CellValue.Text(string.Concat(parts)) : F.Invalid();
        })

        .Raw("TEXTJOIN", 3, int.MaxValue, r =>
        {
            var separators = r.Values(0).Select(r.TextOf).ToList();
            var skipEmpty = r.Boolean(1, true);
            if (r.HasError) return r.Error;
            if (separators.Count == 0) separators.Add(string.Empty);

            var joined = new System.Text.StringBuilder();
            var count = 0;
            for (var i = 2; i < r.Count; i++)
                foreach (var value in r.Values(i))
                {
                    if (value.IsError) return value;
                    var text = r.TextOf(value);
                    if (skipEmpty && text.Length == 0) continue;
                    if (count > 0) joined.Append(separators[(count - 1) % separators.Count]);
                    joined.Append(text);
                    if (!ResultLimits.TextFits(joined.Length)) return F.Invalid();
                    count++;
                }
            return CellValue.Text(joined.ToString());
        })

        .Raw("REPT", 2, 2, r =>
        {
            var text = r.Text(0);
            var times = r.Integer(1);
            if (r.HasError) return r.Error;
            if (times < 0) return F.Invalid();

            if (!ResultLimits.TextFits((long)text.Length * times)) return F.Invalid();
            return CellValue.Text(string.Concat(Enumerable.Repeat(text, times)));
        })

        .Raw("FIND", 2, 3, r => IndexOf(r, StringComparison.Ordinal))
        .Raw("SEARCH", 2, 3, Search)

        .Raw("SUBSTITUTE", 3, 4, r =>
        {
            var text = r.Text(0);
            var search = r.Text(1);
            var replacement = r.Text(2);
            if (r.HasError) return r.Error;
            if (search.Length == 0) return CellValue.Text(text);

            if (!r.Has(3))
            {
                var occurrences = Occurrences(text, search);
                return ResultLimits.TextFits(text.Length + occurrences * ((long)replacement.Length - search.Length))
                    ? CellValue.Text(text.Replace(search, replacement, StringComparison.Ordinal))
                    : F.Invalid();
            }

            var occurrence = r.Integer(3);
            if (r.HasError) return r.Error;
            if (occurrence < 1) return F.Invalid();

            return CellValue.Text(ReplaceOccurrence(text, search, replacement, occurrence));
        })

        .Raw("REPLACE", 4, 4, r =>
        {
            var text = r.Text(0);
            var start = r.Integer(1);
            var length = r.Integer(2);
            var replacement = r.Text(3);
            if (r.HasError) return r.Error;
            if (start < 1 || length < 0) return F.Invalid();
            return CellValue.Text(_characters.Replace(text, start, length, replacement));
        })

        .Raw("EXACT", 2, 2, r =>
        {
            var a = r.Text(0);
            var b = r.Text(1);
            return r.HasError ? r.Error : CellValue.Boolean(string.Equals(a, b, StringComparison.Ordinal));
        })

        .RawHandlingErrors("T", 1, 1, r =>
        {
            var value = r.Value(0);
            return value.Kind == ValueKind.Text || value.IsError ? value : CellValue.Text(string.Empty);
        })

        .Raw("VALUE", 1, 1, r =>
        {
            if (r.Value(0).IsEmpty) return CellValue.Number(0);
            var text = r.Text(0);
            if (r.HasError) return r.Error;
            if (numbers.TryReadGrouped(text, out var n)) return CellValue.Number(n);
            var trimmed = text.Trim();
            if (trimmed is ['(', .. var inner, ')'] && numbers.TryReadGrouped(inner, out var negated) && negated >= 0) return CellValue.Number(-negated);
            if (trimmed.Contains(':') && TimeSpan.TryParse(trimmed, numbers.Culture, out var time)) return CellValue.Number(time.TotalDays);
            return TryReadExplicitDate(trimmed, out var date) ? CellValue.Number(dates.ToSerial(date)) : F.Invalid();
        })

        .Raw("FIXED", 1, 3, r =>
        {
            var value = r.Number(0);
            var decimals = r.Integer(1, 2);
            var noThousands = r.Boolean(2, false);
            if (r.HasError) return r.Error;

            if (decimals > 127) return F.Invalid();
            var rounded = decimals >= 0
                ? M.Round(value, M.Min(decimals, 15), MidpointRounding.AwayFromZero)
                : M.Round(value / M.Pow(10, -decimals), MidpointRounding.AwayFromZero) * M.Pow(10, -decimals);
            var format = (noThousands ? "F" : "N") + M.Max(decimals, 0).ToString(CultureInfo.InvariantCulture);
            return CellValue.Text(rounded.ToString(format, numbers.Culture));
        })

        .Build();

    private static string Proper(string text)
    {
        var builder = new System.Text.StringBuilder(text.Length);
        var afterLetter = false;
        foreach (var current in text)
        {
            builder.Append(afterLetter ? char.ToLowerInvariant(current) : char.ToUpperInvariant(current));
            afterLetter = char.IsLetter(current);
        }
        return builder.ToString();
    }

    private CellValue Search(ArgumentReader r)
    {
        var needle = r.Text(0);
        var haystack = r.Text(1);
        var start = r.Integer(2, 1);
        if (r.HasError) return r.Error;
        if (needle.Length == 0) return F.Invalid();

        var pattern = r.Context.TextRules.SearchPattern(needle);
        var position = _characters.Find(haystack, start, (text, from) => pattern is not null
            ? pattern.IndexOf(text, from)
            : text.IndexOf(needle, from, StringComparison.OrdinalIgnoreCase));
        return position is { } found ? CellValue.Number(found) : F.Invalid();
    }

    private CellValue Slice(ArgumentReader r, bool fromEnd)
    {
        var text = r.Text(0);
        var count = r.Integer(1, 1);
        if (r.HasError) return r.Error;
        if (count < 0) return F.Invalid();

        return CellValue.Text(fromEnd ? _characters.Right(text, count) : _characters.Left(text, count));
    }

    private static CellValue Join(ArgumentReader r, string? separator, bool skipEmpty)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var value in r.AllValues())
        {
            if (value.IsError) return value;
            if (skipEmpty && value.IsEmpty) continue;
            if (separator is not null && builder.Length > 0) builder.Append(separator);
            builder.Append(r.TextOf(value));
            if (!ResultLimits.TextFits(builder.Length)) return F.Invalid();
        }
        return CellValue.Text(builder.ToString());
    }

    private CellValue IndexOf(ArgumentReader r, StringComparison comparison)
    {
        var needle = r.Text(0);
        var haystack = r.Text(1);
        var start = r.Integer(2, 1);
        if (r.HasError) return r.Error;
        if (needle.Length == 0) return F.Invalid();

        return _characters.Find(haystack, start, (text, from) => text.IndexOf(needle, from, comparison)) is { } found
            ? CellValue.Number(found) : F.Invalid();
    }

    private static long Occurrences(string text, string search)
    {
        long count = 0;
        for (var index = text.IndexOf(search, StringComparison.Ordinal); index >= 0; index = text.IndexOf(search, index + search.Length, StringComparison.Ordinal))
            count++;
        return count;
    }

    private static string ReplaceOccurrence(string text, string search, string replacement, int occurrence)
    {
        var index = -1;
        for (var found = 0; found < occurrence; found++)
        {
            index = text.IndexOf(search, index + 1, StringComparison.Ordinal);
            if (index < 0) return text;
        }
        return text.Remove(index, search.Length).Insert(index, replacement);
    }

    private bool TryReadExplicitDate(string text, out DateTime date)
    {
        var format = numbers.Culture.DateTimeFormat;
        string[] patterns =
        [
            "yyyy-MM-dd", "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss",
            format.ShortDatePattern, format.LongDatePattern,
            format.ShortDatePattern + " " + format.ShortTimePattern,
            format.ShortDatePattern + " " + format.LongTimePattern,
        ];
        return DateTime.TryParseExact(text, patterns, numbers.Culture, DateTimeStyles.AllowWhiteSpaces, out date);
    }
}

using System.Globalization;
using System.Text;

namespace PulseOffice.Sheets.Formatting;

internal abstract class SectionRenderer
{
    public abstract FormatValueKind Kind { get; }

    public abstract string Render(double number, FormatSection section);
}

internal sealed class GeneralNumberRenderer(CultureInfo culture)
{
    public string Render(double number, double widthInCharacters)
    {
        if (double.IsNaN(number) || double.IsInfinity(number)) return "#NUM!";
        if (number == 0) return "0";

        var magnitude = Math.Abs(number);
        var available = (int)Math.Clamp(widthInCharacters, 5, 15);

        if (magnitude >= Math.Pow(10, available) || magnitude < Math.Pow(10, -(available - 3)))
            return number.ToString("0.#####E+00", culture);

        var integerDigits = magnitude < 1 ? 1 : (int)Math.Floor(Math.Log10(magnitude)) + 1;
        var decimals = Math.Max(available - integerDigits - 1, 0);

        return Math.Round(number, Math.Min(decimals, 15)).ToString("0.###############", culture);
    }
}

internal sealed class DigitPattern
{
    public DigitPattern(string body)
    {
        var separator = body.IndexOf('.');
        var integer = separator < 0 ? body : body[..separator];
        var fraction = separator < 0 ? string.Empty : body[(separator + 1)..];

        IntegerZeros = integer.Count(c => c == '0');
        RequiredDecimals = fraction.Count(c => c == '0');
        OptionalDecimals = fraction.Count(c => c is '#');
        PaddedDecimals = fraction.Count(c => c is '?');
    }

    public int IntegerZeros { get; }

    public int RequiredDecimals { get; }

    public int OptionalDecimals { get; }

    public int PaddedDecimals { get; }

    public string DotNet(bool thousands)
    {
        var integer = (thousands ? "#,##" : "#") + new string('0', IntegerZeros);
        var decimals = RequiredDecimals + OptionalDecimals + PaddedDecimals;
        return decimals == 0
            ? integer
            : integer + "." + new string('0', RequiredDecimals) + new string('#', OptionalDecimals + PaddedDecimals);
    }

    public string Pad(string text, CultureInfo culture)
    {
        if (PaddedDecimals == 0) return text;

        var separator = culture.NumberFormat.NumberDecimalSeparator;
        var at = text.IndexOf(separator, StringComparison.Ordinal);
        var shown = at < 0 ? 0 : text.Length - at - separator.Length;
        var wanted = RequiredDecimals + OptionalDecimals + PaddedDecimals;
        var missing = wanted - shown;
        if (missing <= 0) return text;

        return (at < 0 ? text + " " : text) + new string(' ', at < 0 ? missing - 1 : missing);
    }
}

internal sealed class PlainNumberRenderer(CultureInfo culture) : SectionRenderer
{
    public override FormatValueKind Kind => FormatValueKind.Number;

    public override string Render(double number, FormatSection section)
    {
        if (section.Body.Length == 0) return string.Empty;

        var value = number;
        if (section.IsPercentage) value *= 100;
        for (var i = 0; i < section.ThousandScale; i++) value /= 1000;

        var digits = new DigitPattern(section.Body.TrimEnd('%', ',').Replace(",", string.Empty, StringComparison.Ordinal));
        var text = digits.Pad(value.ToString(digits.DotNet(section.UseThousandSeparator), culture), culture);
        return section.IsPercentage ? text + "%" : text;
    }
}

internal sealed class ScientificRenderer(CultureInfo culture) : SectionRenderer
{
    public override FormatValueKind Kind => FormatValueKind.Scientific;

    public override string Render(double number, FormatSection section)
    {
        var body = section.Body;
        var marker = body.IndexOfAny(['E', 'e']);
        var mantissa = new DigitPattern(body[..marker].Replace(",", string.Empty, StringComparison.Ordinal));
        var exponent = body[(marker + 1)..];
        var sign = exponent.StartsWith('+') ? "+" : string.Empty;
        var exponentDigits = Math.Max(1, exponent.Count(c => c is '0' or '#' or '?'));

        var pattern = new string('0', Math.Max(1, mantissa.IntegerZeros));
        if (mantissa.RequiredDecimals + mantissa.OptionalDecimals + mantissa.PaddedDecimals > 0)
            pattern += "." + new string('0', mantissa.RequiredDecimals) + new string('#', mantissa.OptionalDecimals + mantissa.PaddedDecimals);

        var interval = body[..marker].Split('.')[0].Count(c => c is '0' or '#' or '?');
        if (interval > 1 && number != 0) return Engineering(number, interval, pattern[Math.Max(1, mantissa.IntegerZeros)..], sign, exponentDigits);

        return number.ToString(pattern + "E" + (sign.Length > 0 ? "+" : "-") + new string('0', exponentDigits), culture);
    }

    private string Engineering(double number, int interval, string decimals, string sign, int exponentDigits)
    {
        var exponent = (int)Math.Floor(Math.Floor(Math.Log10(Math.Abs(number))) / interval) * interval;
        var scaled = number / Math.Pow(10, exponent);
        var digits = exponent < 0 ? "-" : sign;
        return scaled.ToString("0" + decimals, culture) + "E" + digits
            + Math.Abs(exponent).ToString(new string('0', exponentDigits), culture);
    }
}

internal sealed class FractionRenderer(CultureInfo culture) : SectionRenderer
{
    public override FormatValueKind Kind => FormatValueKind.Fraction;

    public override string Render(double number, FormatSection section)
    {
        var body = section.Body;
        var slash = body.IndexOf('/');
        var left = body[..slash].TrimEnd();
        var right = body[(slash + 1)..].Trim();

        var gap = left.LastIndexOf(' ');
        var hasWhole = gap >= 0;
        var numeratorCode = hasWhole ? left[(gap + 1)..] : left;

        var sign = number < 0 ? "-" : string.Empty;
        var magnitude = Math.Abs(number);
        var whole = hasWhole ? Math.Floor(magnitude) : 0;
        var remainder = magnitude - whole;

        var (numerator, denominator) = int.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out var fixedDenominator) && fixedDenominator > 0
            ? ((long)Math.Round(remainder * fixedDenominator), (long)fixedDenominator)
            : Approximate(remainder, (int)Math.Pow(10, Math.Clamp(right.Length, 1, 6)) - 1);

        if (hasWhole && numerator >= denominator)
        {
            whole += numerator / denominator;
            numerator %= denominator;
        }

        var wholeText = whole.ToString("0", culture);

        if (numerator == 0)
            return sign + (hasWhole || whole != 0 ? wholeText : "0");

        var fraction = numerator.ToString(culture).PadLeft(Math.Max(numeratorCode.Length, 1)) + "/" +
                       denominator.ToString(culture).PadRight(Math.Max(right.Length, 1));

        if (!hasWhole) return sign + fraction.TrimStart();
        var shownWhole = whole != 0 || left[..gap].Contains('0') ? wholeText : string.Empty;
        return sign + shownWhole + " " + fraction;
    }

    private static (long Numerator, long Denominator) Approximate(double value, int maximumDenominator)
    {
        long bestNumerator = (long)Math.Round(value);
        long bestDenominator = 1;
        var bestError = Math.Abs(value - bestNumerator);

        for (var denominator = 2; denominator <= maximumDenominator && bestError > 1e-12; denominator++)
        {
            var numerator = Math.Round(value * denominator);
            var error = Math.Abs(value - numerator / denominator);
            if (error >= bestError - 1e-12) continue;

            bestError = error;
            bestNumerator = (long)numerator;
            bestDenominator = denominator;
        }

        return (bestNumerator, bestDenominator);
    }
}

internal sealed class DateTimeRenderer(CultureInfo culture, Func<double, DateTime> serialToDate) : SectionRenderer
{
    private const double LastSerial = 2958466;

    private readonly DateTimeTokenizer _tokenizer = new();

    public override FormatValueKind Kind => FormatValueKind.DateTime;

    public override string Render(double number, FormatSection section)
    {
        var tokens = _tokenizer.Tokenize(section.Tokens);
        var hasElapsed = tokens.OfType<ElapsedToken>().Any();
        if (number < 0 && !hasElapsed) return "#####";
        if (Math.Abs(number) >= LastSerial) return "#####";

        var precision = tokens.OfType<FractionalSecondToken>().Select(t => t.Digits).DefaultIfEmpty(0).Max();
        var scale = Math.Pow(10, precision);
        var totalSeconds = Math.Round(Math.Abs(number) * 86400 * scale) / scale;
        var day = Math.Floor(totalSeconds / 86400);

        var moment = new DateTimeMoment(
            serialToDate(number < 0 ? number : day),
            totalSeconds,
            tokens.OfType<AmPmToken>().Any(),
            number < 0,
            culture);

        var text = new StringBuilder();
        foreach (var token in tokens) token.Render(moment, text);
        return text.ToString();
    }
}

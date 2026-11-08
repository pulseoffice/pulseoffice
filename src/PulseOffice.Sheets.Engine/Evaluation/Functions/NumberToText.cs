using System.Globalization;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions;

public sealed class NumberToText(CultureInfo culture)
{
    private const int SignificantDigits = 15;
    private const double ExactIntegerLimit = 9007199254740992;

    public string Of(CellValue value) => value.Kind switch
    {
        ValueKind.Number => Of(value.AsNumber()),
        ValueKind.Boolean => value.AsBoolean() ? "1" : "0",
        _ => value.ToString(),
    };

    public string Of(double number)
    {
        if (!double.IsFinite(number)) return number.ToString(CultureInfo.InvariantCulture);
        if (number == 0) return "0";

        var exponent = (int)Math.Floor(Math.Log10(Math.Abs(number)));
        if (exponent >= 16 || exponent <= -15) return Scientific(number);
        if (exponent == 15)
            return Math.Abs(number) < ExactIntegerLimit && number == Math.Floor(number)
                ? number.ToString("F0", culture)
                : Scientific(number);

        var rounded = double.Parse(number.ToString("E" + (SignificantDigits - 1), CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        var decimals = Math.Clamp(SignificantDigits - 1 - exponent, 0, 30);
        var text = rounded.ToString("F" + decimals, culture);
        return Trim(text);
    }

    private string Scientific(double number)
    {
        var text = number.ToString("E" + (SignificantDigits - 1), CultureInfo.InvariantCulture);
        var split = text.IndexOf('E');
        var mantissa = text[..split].Contains('.') ? text[..split].TrimEnd('0').TrimEnd('.') : text[..split];
        var power = int.Parse(text[(split + 1)..], CultureInfo.InvariantCulture);
        var sign = power < 0 ? "-" : "+";
        return mantissa.Replace(".", culture.NumberFormat.NumberDecimalSeparator, StringComparison.Ordinal)
            + "E" + sign + Math.Abs(power).ToString("000", CultureInfo.InvariantCulture);
    }

    private string Trim(string text)
    {
        var separator = culture.NumberFormat.NumberDecimalSeparator;
        if (!text.Contains(separator, StringComparison.Ordinal)) return text;
        return text.TrimEnd('0') is var trimmed && trimmed.EndsWith(separator, StringComparison.Ordinal)
            ? trimmed[..^separator.Length]
            : trimmed;
    }
}

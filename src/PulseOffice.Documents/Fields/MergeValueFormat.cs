using System.Globalization;
using System.Text;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Fields;

public static class MergeCases
{
    public const string Upper = "upper";
    public const string Lower = "lower";
    public const string Caps = "caps";
    public const string FirstCap = "firstcap";

    public static readonly IReadOnlyList<string> All = [Upper, Lower, Caps, FirstCap];
}

public interface IMergeValueFormat
{
    string Shown(string? value, FieldDetail? detail, CultureInfo culture);
}

public sealed class MergeValueFormat : IMergeValueFormat
{
    public string Shown(string? value, FieldDetail? detail, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var shaped = Cased(Numbered(value, detail?.Value, culture), detail?.Format, culture);
        return (detail?.WhenTrue ?? string.Empty) + shaped + (detail?.WhenFalse ?? string.Empty);
    }

    private string Numbered(string value, string? picture, CultureInfo culture)
    {
        if (string.IsNullOrEmpty(picture)) return value;
        if (!decimal.TryParse(value, NumberStyles.Number, culture, out var number) && !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out number))
            return value;
        try
        {
            return number.ToString(picture.Replace('x', '#'), culture);
        }
        catch (FormatException)
        {
            return value;
        }
    }

    private string Cased(string value, string? casing, CultureInfo culture) => casing switch
    {
        MergeCases.Upper => value.ToUpper(culture),
        MergeCases.Lower => value.ToLower(culture),
        MergeCases.Caps => Words(value, culture),
        MergeCases.FirstCap => value.Length == 0 ? value : char.ToUpper(value[0], culture) + value[1..],
        _ => value,
    };

    private string Words(string value, CultureInfo culture)
    {
        var builder = new StringBuilder(value.Length);
        var start = true;
        foreach (var character in value)
        {
            builder.Append(start ? char.ToUpper(character, culture) : character);
            start = char.IsWhiteSpace(character) || character == '-';
        }
        return builder.ToString();
    }
}

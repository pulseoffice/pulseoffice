using System.Globalization;
using System.Text;

namespace PulseOffice.Text.Rich;

public enum ListNumberStyle
{
    Arabic,
    LowerLetter,
    UpperLetter,
    LowerRoman,
    UpperRoman,
}

public sealed record ListNumberFormat
{
    public const int LargestStart = 32767;

    public const int LongestAffix = 16;

    public ListNumberStyle Style { get; init; }

    public string Prefix { get; init; } = string.Empty;

    public string Suffix { get; init; } = ".";

    public int Start { get; init; } = 1;

    public string? DrawingScheme { get; init; }

    public string? OdfFormat { get; init; }

    public bool IsPlain => this == new ListNumberFormat();
}

public interface IListNumberFormats
{
    ListNumberFormat? Normalized(ListNumberFormat? format);

    string Label(ListNumberFormat? format, int ordinal);

    string Number(ListNumberStyle style, int value);
}

public sealed class ListNumberFormats : IListNumberFormats
{
    private const int LargestRoman = 3999;

    private readonly (int Value, string Digits)[] _romans =
    [
        (1000, "m"), (900, "cm"), (500, "d"), (400, "cd"), (100, "c"), (90, "xc"),
        (50, "l"), (40, "xl"), (10, "x"), (9, "ix"), (5, "v"), (4, "iv"), (1, "i"),
    ];

    public ListNumberFormat? Normalized(ListNumberFormat? format)
    {
        if (format is null) return null;
        var normal = format with
        {
            Style = Enum.IsDefined(format.Style) ? format.Style : ListNumberStyle.Arabic,
            Prefix = Affix(format.Prefix),
            Suffix = Affix(format.Suffix),
            Start = Math.Clamp(format.Start, 1, ListNumberFormat.LargestStart),
            DrawingScheme = Token(format.DrawingScheme),
            OdfFormat = Token(format.OdfFormat),
        };
        return normal.IsPlain ? null : normal;
    }

    public string Label(ListNumberFormat? format, int ordinal)
    {
        var used = format ?? new ListNumberFormat();
        var value = Math.Clamp((long)used.Start - 1 + Math.Max(1, ordinal), 1, int.MaxValue);
        return used.Prefix + Number(used.Style, (int)value) + used.Suffix;
    }

    public string Number(ListNumberStyle style, int value) => style switch
    {
        ListNumberStyle.LowerLetter => Letters(value),
        ListNumberStyle.UpperLetter => Letters(value).ToUpperInvariant(),
        ListNumberStyle.LowerRoman => Roman(value),
        ListNumberStyle.UpperRoman => Roman(value).ToUpperInvariant(),
        _ => value.ToString(CultureInfo.InvariantCulture),
    };

    private string Affix(string? affix) =>
        string.IsNullOrEmpty(affix) ? string.Empty
        : new string([.. affix.Where(character => !char.IsControl(character)).Take(ListNumberFormat.LongestAffix)]);

    private string? Token(string? token) =>
        token is { Length: > 0 and <= 64 } && token.All(character => !char.IsControl(character)) ? token : null;

    private string Letters(int value)
    {
        var builder = new StringBuilder();
        for (var remaining = Math.Max(1, value); remaining > 0; remaining = (remaining - 1) / 26)
            builder.Insert(0, (char)('a' + (remaining - 1) % 26));
        return builder.ToString();
    }

    private string Roman(int value)
    {
        if (value > LargestRoman) return value.ToString(CultureInfo.InvariantCulture);
        var builder = new StringBuilder();
        var remaining = Math.Max(1, value);
        foreach (var (amount, digits) in _romans)
        {
            while (remaining >= amount)
            {
                builder.Append(digits);
                remaining -= amount;
            }
        }
        return builder.ToString();
    }
}

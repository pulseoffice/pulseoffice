using System.Globalization;
using System.Text;

namespace PulseOffice.Documents.Fields;

public static class SequenceFormats
{
    public const string Arabic = "1";
    public const string LowerRoman = "i";
    public const string UpperRoman = "I";
    public const string LowerLetter = "a";
    public const string UpperLetter = "A";

    public static readonly IReadOnlyList<string> All = [Arabic, LowerRoman, UpperRoman, LowerLetter, UpperLetter];
}

public interface ISequenceNumerals
{
    string Format(int value, string? format);
}

public sealed class SequenceNumerals : ISequenceNumerals
{
    private readonly (int Value, string Symbol)[] _roman =
    [
        (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"),
        (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I"),
    ];

    public string Format(int value, string? format) => format switch
    {
        SequenceFormats.UpperRoman when value is > 0 and < 4000 => Roman(value),
        SequenceFormats.LowerRoman when value is > 0 and < 4000 => Roman(value).ToLowerInvariant(),
        SequenceFormats.UpperLetter when value > 0 => Letters(value),
        SequenceFormats.LowerLetter when value > 0 => Letters(value).ToLowerInvariant(),
        _ => value.ToString(CultureInfo.InvariantCulture),
    };

    private string Roman(int value)
    {
        var builder = new StringBuilder();
        foreach (var (amount, symbol) in _roman)
            while (value >= amount)
            {
                builder.Append(symbol);
                value -= amount;
            }
        return builder.ToString();
    }

    private string Letters(int value) => new((char)('A' + (value - 1) % 26), (value - 1) / 26 + 1);
}

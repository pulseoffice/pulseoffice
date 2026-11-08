using PulseOffice.Documents.Styles;

namespace PulseOffice.Formats.Interchange.Text.Outline;

public sealed class ListNumbering
{
    public string Label(ListPlacement placement, int value) => placement.Kind switch
    {
        ListKind.None => string.Empty,
        ListKind.Bullet => "\u2022",
        _ => Format(value, placement.Format) + ".",
    };

    private string Format(int value, NumberFormat format) => format switch
    {
        NumberFormat.LowerLetter => Letters(value).ToLowerInvariant(),
        NumberFormat.UpperLetter => Letters(value),
        NumberFormat.LowerRoman => Roman(value).ToLowerInvariant(),
        NumberFormat.UpperRoman => Roman(value),
        _ => value.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    private string Letters(int value)
    {
        var text = string.Empty;
        while (value > 0)
        {
            value--;
            text = (char)('A' + value % 26) + text;
            value /= 26;
        }
        return text;
    }

    private string Roman(int value)
    {
        (int Value, string Symbol)[] table = [(1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"), (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")];
        var text = new System.Text.StringBuilder();
        foreach (var (number, symbol) in table)
            while (value >= number && text.Length < 64)
            {
                text.Append(symbol);
                value -= number;
            }
        return text.ToString();
    }
}

using PulseOffice.Documents.Styles;

namespace PulseOffice.Formats.Interchange.Text.Outline;

public sealed class ListNumbering
{
    private readonly PulseOffice.Text.Rich.NumberSystems _systems = new();

    public string Label(ListPlacement placement, int value, string? language = null) => placement.Kind switch
    {
        ListKind.None => string.Empty,
        ListKind.Bullet => "\u2022",
        _ => Format(value, placement.Format, language) + ".",
    };

    public string Format(int value, NumberFormat format, string? language = null) => format switch
    {
        NumberFormat.LowerLetter => Letters(value).ToLowerInvariant(),
        NumberFormat.UpperLetter => Letters(value),
        NumberFormat.LowerRoman => Roman(value).ToLowerInvariant(),
        NumberFormat.UpperRoman => Roman(value),
        _ when Enum.TryParse<PulseOffice.Text.Rich.NumberSystem>(format.ToString(), out var system) => _systems.Render(system, value, language),
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

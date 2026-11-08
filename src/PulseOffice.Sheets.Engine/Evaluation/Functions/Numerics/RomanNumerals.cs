namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;

public static class RomanNumerals
{
    private static readonly (int Value, string Symbol)[] Table =
    [
        (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"),
        (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I"),
    ];

    private static readonly Dictionary<char, int> Symbols = new()
    {
        ['I'] = 1,
        ['V'] = 5,
        ['X'] = 10,
        ['L'] = 50,
        ['C'] = 100,
        ['D'] = 500,
        ['M'] = 1000,
    };

    private const string Letters = "MDCLXVI";
    private static readonly int[] LetterValues = [1000, 500, 100, 50, 10, 5, 1];

    public static string Format(int value, int mode)
    {
        if (mode == 0) return Format(value);
        var builder = new System.Text.StringBuilder();
        var last = LetterValues.Length - 1;
        for (var i = 0; i <= last / 2; i++)
        {
            var index = 2 * i;
            var digit = value / LetterValues[index];
            if (digit % 5 == 4)
            {
                var index2 = digit == 4 ? index - 1 : index - 2;
                var steps = 0;
                while (steps < mode && index < last)
                {
                    steps++;
                    if (LetterValues[index2] - LetterValues[index + 1] <= value) index++;
                    else steps = mode;
                }
                builder.Append(Letters[index]).Append(Letters[index2]);
                value = value + LetterValues[index] - LetterValues[index2];
            }
            else
            {
                if (digit > 4) builder.Append(Letters[index - 1]);
                builder.Append(Letters[index], digit % 5);
                value %= LetterValues[index];
            }
        }
        return builder.ToString();
    }

    public static string Format(int value)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var (amount, symbol) in Table)
            while (value >= amount)
            {
                builder.Append(symbol);
                value -= amount;
            }
        return builder.ToString();
    }

    public static double? Parse(string text)
    {
        if (text.Length == 0) return 0;

        var total = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (!Symbols.TryGetValue(text[i], out var current)) return null;

            var subtractive = i + 1 < text.Length
                              && Symbols.TryGetValue(text[i + 1], out var next)
                              && next > current;

            total += subtractive ? -current : current;
        }
        return total;
    }
}

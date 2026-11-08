namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;

public static class RadixConverter
{
    private const string Digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public static string ToBase(long value, int radix)
    {
        if (value == 0) return "0";

        var builder = new System.Text.StringBuilder();
        while (value > 0)
        {
            builder.Insert(0, Digits[(int)(value % radix)]);
            value /= radix;
        }
        return builder.ToString();
    }

    public static double? FromBase(string text, int radix)
    {
        if (text.Length == 0) return null;

        var result = 0.0;
        foreach (var character in text)
        {
            var digit = Digits.IndexOf(char.ToUpperInvariant(character));
            if (digit < 0 || digit >= radix) return null;
            result = result * radix + digit;
        }
        return result;
    }
}

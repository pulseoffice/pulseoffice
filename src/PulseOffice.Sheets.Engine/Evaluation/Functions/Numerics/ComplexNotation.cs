using System.Globalization;
using System.Numerics;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;

public readonly record struct ComplexText(Complex Value, char? Suffix);

public static class ComplexNotation
{
    public static ComplexText? Parse(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return null;

        var suffix = text[^1];
        if (suffix is not ('i' or 'j'))
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var real)
                ? new ComplexText(new Complex(real, 0), null)
                : null;

        return ParseImaginary(text[..^1]) is { } value ? new ComplexText(value, suffix) : null;
    }

    private static Complex? ParseImaginary(string body)
    {
        var split = -1;
        for (var i = body.Length - 1; i > 0; i--)
            if (body[i] is '+' or '-' && body[i - 1] is not ('e' or 'E'))
            {
                split = i;
                break;
            }

        if (split < 0)
        {
            var imaginaryOnly = body switch
            {
                "" or "+" => 1.0,
                "-" => -1.0,
                _ => double.TryParse(body, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                        ? v : double.NaN,
            };
            return double.IsNaN(imaginaryOnly) ? null : new Complex(0, imaginaryOnly);
        }

        var realPart = body[..split];
        var imaginaryPart = body[split..];

        if (!double.TryParse(realPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var realValue))
            return null;

        var imaginaryValue = imaginaryPart switch
        {
            "+" => 1.0,
            "-" => -1.0,
            _ => double.TryParse(imaginaryPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                    ? v : double.NaN,
        };

        return double.IsNaN(imaginaryValue) ? null : new Complex(realValue, imaginaryValue);
    }

    public static string Format(Complex value, string suffix = "i")
    {
        var real = Significant(value.Real);
        var imaginary = Significant(value.Imaginary);

        if (imaginary == 0) return Number(real);
        if (real == 0) return Imaginary(imaginary) + suffix;

        var sign = imaginary < 0 ? "-" : "+";
        return Number(real) + sign + Imaginary(System.Math.Abs(imaginary)) + suffix;

        static string Imaginary(double v) =>
            System.Math.Abs(v) == 1 ? (v < 0 ? "-" : string.Empty) : Number(v);

        static string Number(double v) => v.ToString("R", CultureInfo.InvariantCulture);

        static double Significant(double v) =>
            double.Parse(v.ToString("G15", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }
}

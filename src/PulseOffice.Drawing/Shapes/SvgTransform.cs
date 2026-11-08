using System.Globalization;
using System.Text.RegularExpressions;

namespace PulseOffice.Drawing.Shapes;

public readonly record struct AffineMatrix(double A, double B, double C, double D, double E, double F)
{
    public static AffineMatrix Identity { get; } = new(1, 0, 0, 1, 0, 0);

    public bool IsIdentity => this == Identity;

    public double Scale => Math.Sqrt(Math.Abs(A * D - B * C));

    public AffineMatrix Then(AffineMatrix inner) => new(
        A * inner.A + C * inner.B,
        B * inner.A + D * inner.B,
        A * inner.C + C * inner.D,
        B * inner.C + D * inner.D,
        A * inner.E + C * inner.F + E,
        B * inner.E + D * inner.F + F);

    public ShapePoint Apply(ShapePoint point) => new(A * point.X + C * point.Y + E, B * point.X + D * point.Y + F);
}

public sealed partial class SvgTransform
{
    [GeneratedRegex(@"(matrix|translate|scale|rotate|skewX|skewY)\s*\(([^)]*)\)")]
    private partial Regex Operations();

    public AffineMatrix Parse(string? text)
    {
        var result = AffineMatrix.Identity;
        if (string.IsNullOrWhiteSpace(text)) return result;

        foreach (Match match in Operations().Matches(text))
        {
            var values = match.Groups[2].Value
                .Split([' ', ',', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
                .Select(part => double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0)
                .ToArray();

            result = result.Then(Operation(match.Groups[1].Value, values));
        }

        return result;
    }

    private AffineMatrix Operation(string name, double[] v)
    {
        double At(int index, double fallback = 0) => index < v.Length ? v[index] : fallback;

        switch (name)
        {
            case "matrix" when v.Length >= 6:
                return new AffineMatrix(v[0], v[1], v[2], v[3], v[4], v[5]);
            case "translate":
                return new AffineMatrix(1, 0, 0, 1, At(0), At(1));
            case "scale":
                return new AffineMatrix(At(0, 1), 0, 0, At(1, At(0, 1)), 0, 0);
            case "rotate":
            {
                var angle = At(0) * Math.PI / 180;
                var rotation = new AffineMatrix(Math.Cos(angle), Math.Sin(angle), -Math.Sin(angle), Math.Cos(angle), 0, 0);
                if (v.Length < 3) return rotation;
                return new AffineMatrix(1, 0, 0, 1, v[1], v[2]).Then(rotation).Then(new AffineMatrix(1, 0, 0, 1, -v[1], -v[2]));
            }
            case "skewX":
                return new AffineMatrix(1, 0, Math.Tan(At(0) * Math.PI / 180), 1, 0, 0);
            case "skewY":
                return new AffineMatrix(1, Math.Tan(At(0) * Math.PI / 180), 0, 1, 0, 0);
            default:
                return AffineMatrix.Identity;
        }
    }
}

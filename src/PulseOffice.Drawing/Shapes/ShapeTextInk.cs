using System.Globalization;

namespace PulseOffice.Drawing.Shapes;

public interface IShapeTextInk
{
    string Resolve(ShapeStyle style, string backdropHex);
}

public sealed class ShapeTextInk : IShapeTextInk
{
    public const string PaperHex = "#FFFFFF";

    private const string DarkInk = "#000000";
    private const string LightInk = "#FFFFFF";
    private const double Threshold = 0.5;

    public string Resolve(ShapeStyle style, string backdropHex)
    {
        ArgumentNullException.ThrowIfNull(style);
        if (!style.Text.AutomaticColor) return style.Text.ColorHex;
        var behind = Rgb(backdropHex);
        var fill = style.Fill;
        if (fill.IsVisible && fill.Kind is not ShapeFillKind.Bitmap)
        {
            var colour = Rgb(fill.Kind is ShapeFillKind.Gradient ? fill.Gradient.StartHex : fill.ColorHex);
            var opacity = Math.Clamp(fill.Opacity, 0, 1);
            behind = (Mix(colour.R, behind.R, opacity), Mix(colour.G, behind.G, opacity), Mix(colour.B, behind.B, opacity));
        }
        return Luminance(behind) > Threshold ? DarkInk : LightInk;
    }

    private double Mix(double over, double under, double opacity) => over * opacity + under * (1 - opacity);

    private double Luminance((double R, double G, double B) colour) => 0.2126 * colour.R + 0.7152 * colour.G + 0.0722 * colour.B;

    private (double R, double G, double B) Rgb(string? hex)
    {
        var digits = (hex ?? string.Empty).TrimStart('#');
        if (digits.Length == 8) digits = digits[2..];
        return digits.Length == 6 && int.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)
            ? (((value >> 16) & 0xFF) / 255.0, ((value >> 8) & 0xFF) / 255.0, (value & 0xFF) / 255.0)
            : (1, 1, 1);
    }
}

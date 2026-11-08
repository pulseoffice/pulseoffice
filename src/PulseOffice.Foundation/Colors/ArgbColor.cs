using System.Globalization;

namespace PulseOffice.Foundation.Colors;

public readonly record struct ArgbColor(uint Value)
{
    public const uint OpaqueAlpha = 0xFF000000;
    public const uint RgbMask = 0x00FFFFFF;

    public static readonly ArgbColor Black = new(OpaqueAlpha);

    public uint Rgb => Value & RgbMask;

    public static ArgbColor Opaque(uint rgb) => new(OpaqueAlpha | (rgb & RgbMask));

    public static ArgbColor? FromRgbHex(string? text)
    {
        if (text is null) return null;
        var digits = text.AsSpan();
        if (digits.Length > 0 && digits[0] == '#') digits = digits[1..];
        return digits.Length == 6 && uint.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb)
            ? Opaque(rgb)
            : null;
    }

    public static ArgbColor? FromHashedRgbHex(string? text) =>
        text is { Length: 7 } && text[0] == '#' ? FromRgbHex(text) : null;

    public string ToRgbDigits() => Rgb.ToString("X6", CultureInfo.InvariantCulture);

    public string ToLowerRgbDigits() => Rgb.ToString("x6", CultureInfo.InvariantCulture);

    public string ToHashedHex() => "#" + ToRgbDigits();

    public string ToLowerHashedHex() => "#" + ToLowerRgbDigits();
}

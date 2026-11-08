using System.Globalization;

namespace PulseOffice.UI.Controls;

public sealed class NumberEntry(NumericBounds bounds, bool integer, CultureInfo culture)
{
    public NumericBounds Bounds { get; } = bounds;

    public bool Integer { get; } = integer;

    public double Normalize(double value)
    {
        var clamped = Bounds.Clamp(value);
        return Integer ? Bounds.Clamp(Math.Round(clamped, MidpointRounding.AwayFromZero)) : clamped;
    }

    public bool TryParse(string? text, out double value)
    {
        value = 0;
        var trimmed = text?.Trim() ?? string.Empty;
        if (!double.TryParse(trimmed, NumberStyles.Float, culture, out var typed) || !double.IsFinite(typed)) return false;
        value = Normalize(typed);
        return true;
    }

    public double Step(double value, int direction) => Normalize(value + direction * Bounds.Step);

    public string Format(double value) => value.ToString(Integer ? "0" : "0." + new string('#', SignificantDecimals), culture);

    private int SignificantDecimals
    {
        get
        {
            if (Bounds.Step is <= 0 or >= 1 || !double.IsFinite(Bounds.Step)) return 6;
            var stepDecimals = 0;
            var step = Bounds.Step;
            while (stepDecimals < 6 && Math.Abs(step - Math.Round(step)) > 1e-9)
            {
                step *= 10;
                stepDecimals++;
            }

            return Math.Min(6, stepDecimals + 1);
        }
    }
}

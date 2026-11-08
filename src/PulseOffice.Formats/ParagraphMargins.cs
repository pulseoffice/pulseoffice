namespace PulseOffice.Formats;

public sealed class ParagraphMargins
{
    public const double Hairline = 0.05;

    public double? Written(double? margin, double? indent) =>
        margin is { } value && Math.Abs(value) < Hairline && indent is { } first && Math.Abs(first) >= Hairline ? Hairline : margin;

    public double? Read(double? margin) => margin is { } value && Math.Abs(value) <= Hairline * 1.5 ? 0 : margin;
}

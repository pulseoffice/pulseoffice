namespace PulseOffice.UI.Controls.Ribbon;

public sealed class SampleTypeScale
{
    public const double Minimum = 8;

    public const double PointsToPixels = 0.75;

    public double Fit(double? points, double fallback, double available)
    {
        var maximum = Math.Max(Minimum, available);
        var wanted = points is { } size && double.IsFinite(size) && size > 0 ? size * PointsToPixels : fallback;
        return Math.Clamp(wanted, Minimum, maximum);
    }

    public double FitLabel(double? points, double interfaceSize, double available) =>
        Math.Min(Math.Max(Minimum, available), Math.Max(interfaceSize, Fit(points, interfaceSize, available)));
}

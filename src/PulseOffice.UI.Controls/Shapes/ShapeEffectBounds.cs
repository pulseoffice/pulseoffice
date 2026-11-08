namespace PulseOffice.UI.Controls.Shapes;

public interface IShapeEffectBounds
{
    double Radius(double points);

    double Opacity(double opacity);
}

public sealed class ShapeEffectBounds : IShapeEffectBounds
{
    public const double MaximumRadiusPoints = 144;

    public double Radius(double points) =>
        double.IsFinite(points) ? Math.Clamp(points, 0, MaximumRadiusPoints) : 0;

    public double Opacity(double opacity) =>
        double.IsFinite(opacity) ? Math.Clamp(opacity, 0, 1) : 1;
}

namespace PulseOffice.Drawing.Shapes;

public readonly record struct ShapeTextMargins(double Left, double Top, double Right, double Bottom)
{
    public static ShapeTextMargins None { get; } = new(0, 0, 0, 0);

    public double Width(double width) => Math.Max(1, width - Left - Right);

    public double Height(double height) => Math.Max(1, height - Top - Bottom);

    public double Across => Left + Right;

    public double Down => Top + Bottom;
}

public interface IShapeTextArea
{
    ShapeTextMargins Margins(ShapeObject shape, double width, double height);
}

public sealed class ShapeTextArea : IShapeTextArea
{
    private const double InscribedEllipse = 0.1464466;

    private const double RoundedCornerInset = 0.2928932;

    public ShapeTextMargins Margins(ShapeObject shape, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(shape);
        if (shape.Computed is not null || shape.Dimension is not null || width <= 0 || height <= 0) return ShapeTextMargins.None;
        var shorter = Math.Min(width, height);
        return shape.Shape switch
        {
            ShapeKind.Ellipse or ShapeKind.FlowConnector or ShapeKind.FlowOr or ShapeKind.FlowSummingJunction
                => Fractions(width, height, InscribedEllipse, InscribedEllipse, InscribedEllipse, InscribedEllipse),
            ShapeKind.Diamond or ShapeKind.FlowDecision => Fractions(width, height, 0.25, 0.25, 0.25, 0.25),
            ShapeKind.Triangle => Fractions(width, height, 0.25, 0.5, 0.25, 0),
            ShapeKind.RightTriangle => Fractions(width, height, 1.0 / 12, 7.0 / 12, 5.0 / 12, 1.0 / 12),
            ShapeKind.RoundedRectangle => Rounded(width, height, Math.Clamp(double.IsFinite(shape.CornerRatio) ? shape.CornerRatio : 0.18, 0, 0.5)),
            ShapeKind.Octagon => new ShapeTextMargins(shorter * InscribedEllipse, shorter * InscribedEllipse, shorter * InscribedEllipse, shorter * InscribedEllipse),
            ShapeKind.Cylinder or ShapeKind.FlowMagneticDisk => new ShapeTextMargins(0, shorter * 0.25, 0, shorter * 0.125),
            ShapeKind.FlowDocument => Fractions(width, height, 0, 0, 0, 1 - 17322.0 / 21600),
            ShapeKind.FlowTerminator => Fractions(width, height, 1018.0 / 21600, 3163.0 / 21600, 1018.0 / 21600, 3163.0 / 21600),
            _ => ShapeTextMargins.None,
        };
    }

    private ShapeTextMargins Rounded(double width, double height, double corner) =>
        Fractions(width, height, corner * RoundedCornerInset, corner * RoundedCornerInset, corner * RoundedCornerInset, corner * RoundedCornerInset);

    private ShapeTextMargins Fractions(double width, double height, double left, double top, double right, double bottom) =>
        new(width * left, height * top, width * right, height * bottom);
}

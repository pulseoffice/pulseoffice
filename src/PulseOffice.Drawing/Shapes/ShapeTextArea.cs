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
            ShapeKind.FlowSequentialAccess or ShapeKind.SmileyFace or ShapeKind.NoSymbol or ShapeKind.Donut
                => Fractions(width, height, InscribedEllipse, InscribedEllipse, InscribedEllipse, InscribedEllipse),
            ShapeKind.Sun => Fractions(width, height, 0.3, 0.3, 0.3, 0.3),
            ShapeKind.Cloud => Fractions(width, height, 0.17, 0.17, 0.17, 0.2),
            ShapeKind.Heart => Fractions(width, height, 0.15, 0.2, 0.15, 0.3),
            ShapeKind.Parallelogram => new ShapeTextMargins(shorter / 6, height / 12, shorter / 6, height / 12),
            ShapeKind.Trapezoid => new ShapeTextMargins(shorter / 12, height * shorter / (6 * width), shorter / 12, 0),
            ShapeKind.Pentagon => Fractions(width, height, 0.19, 0.25, 0.19, 0),
            ShapeKind.Hexagon => new ShapeTextMargins(shorter / 8, height * InscribedEllipse, shorter / 8, height * InscribedEllipse),
            ShapeKind.Star4 => Fractions(width, height, 0.32, 0.32, 0.32, 0.32),
            ShapeKind.Star5 => Fractions(width, height, 0.3, 0.38, 0.3, 0.2),
            ShapeKind.Star6 => Fractions(width, height, 0.25, 0.25, 0.25, 0.25),
            ShapeKind.Star8 => Fractions(width, height, 0.15, 0.15, 0.15, 0.15),
            ShapeKind.ArrowRight => new ShapeTextMargins(0, height / 4, shorter / 4, height / 4),
            ShapeKind.ArrowLeft => new ShapeTextMargins(shorter / 4, height / 4, 0, height / 4),
            ShapeKind.ArrowUp => new ShapeTextMargins(width / 4, shorter / 4, width / 4, 0),
            ShapeKind.ArrowDown => new ShapeTextMargins(width / 4, 0, width / 4, shorter / 4),
            ShapeKind.DoubleArrow => new ShapeTextMargins(shorter / 4, height / 4, shorter / 4, height / 4),
            ShapeKind.Chevron => new ShapeTextMargins(shorter / 4, 0, shorter / 4, 0),
            ShapeKind.Cross => new ShapeTextMargins(0, shorter / 4, 0, shorter / 4),
            ShapeKind.FlowAlternateProcess => Uniform(shorter / 6 * RoundedCornerInset),
            ShapeKind.FlowData => Fractions(width, height, 0.1, 0, 0.1, 0),
            ShapeKind.FlowPredefinedProcess => Fractions(width, height, 0.125, 0, 0.125, 0),
            ShapeKind.FlowInternalStorage => Fractions(width, height, 0.125, 0.125, 0, 0),
            ShapeKind.FlowMultiDocument => Fractions(width, height, 0, 0.1, 0.17, 0.25),
            ShapeKind.FlowPreparation or ShapeKind.FlowManualOperation => Fractions(width, height, 0.2, 0, 0.2, 0),
            ShapeKind.FlowManualInput or ShapeKind.FlowCard => Fractions(width, height, 0, 0.2, 0, 0),
            ShapeKind.FlowOffPageConnector => Fractions(width, height, 0, 0, 0, 0.2),
            ShapeKind.FlowPunchedTape => Fractions(width, height, 0, 0.2, 0, 0.2),
            ShapeKind.FlowCollate or ShapeKind.FlowSort => Fractions(width, height, 0.25, 0.25, 0.25, 0.25),
            ShapeKind.FlowExtract => Fractions(width, height, 0.25, 0.5, 0.25, 0),
            ShapeKind.FlowMerge => Fractions(width, height, 0.25, 0, 0.25, 0.5),
            ShapeKind.FlowStoredData or ShapeKind.FlowDisplay => Fractions(width, height, 1.0 / 6, 0, 1.0 / 6, 0),
            ShapeKind.FlowDelay => Fractions(width, height, 0, InscribedEllipse, InscribedEllipse, InscribedEllipse),
            ShapeKind.FlowDirectAccess => Fractions(width, height, 1.0 / 12, 0, 0.25, 0),
            ShapeKind.FoldedCorner => new ShapeTextMargins(0, 0, 0, shorter / 6),
            ShapeKind.Bevel or ShapeKind.Frame => Uniform(shorter / 8),
            ShapeKind.Plaque => Uniform(shorter / 6 * 0.7071068),
            _ => ShapeTextMargins.None,
        };
    }

    private ShapeTextMargins Uniform(double inset) => new(inset, inset, inset, inset);

    private ShapeTextMargins Rounded(double width, double height, double corner) =>
        Fractions(width, height, corner * RoundedCornerInset, corner * RoundedCornerInset, corner * RoundedCornerInset, corner * RoundedCornerInset);

    private ShapeTextMargins Fractions(double width, double height, double left, double top, double right, double bottom) =>
        new(width * left, height * top, width * right, height * bottom);
}

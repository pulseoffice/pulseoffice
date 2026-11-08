using PulseOffice.Drawing.Shapes;

namespace PulseOffice.Formats.OpenXml;

internal sealed class DrawingStyleFidelity
{
    private readonly double[] _arrowScales = [0.5, 1, 2];

    public bool LosesGradient(ShapeStyle style) =>
        style.Fill.Kind is ShapeFillKind.Gradient
        && (style.Fill.Gradient.Kind is GradientKind.Elliptical or GradientKind.Square || style.Fill.Gradient.Border > 0);

    public bool LosesHatch(ShapeStyle style) =>
        style.Fill.Kind is ShapeFillKind.Hatch
        && (Math.Abs(style.Fill.Hatch.AngleDegrees - ShapeHatch.Default.AngleDegrees) > 0.01 || style.Fill.Hatch.Distance != ShapeHatch.Default.Distance);

    public bool LosesBitmapTile(ShapeStyle style) =>
        style.Fill is { Kind: ShapeFillKind.Bitmap, Bitmap: { Tiled: true } bitmap } && bitmap.HasChosenTileSize;

    public bool LosesDashShape(ShapeStyle style) =>
        style.Line is { CustomDash: { } dash, Dash: not ShapeStrokeStyle.Solid and not ShapeStrokeStyle.None } && (dash.Round || !dash.Relative);

    public bool LosesArrowSize(ShapeStyle style) =>
        style.Line.StartArrow is not ArrowEnd.None || style.Line.EndArrow is not ArrowEnd.None
            ? !_arrowScales.Any(scale => Math.Abs(scale - style.Line.ArrowScale) < 0.001)
            : false;

    public string ArrowSize(double scale) => scale < 0.75 ? "sm" : scale > 1.5 ? "lg" : "med";

    public double ArrowScale(string? size) => size switch
    {
        "sm" => 0.5,
        "lg" => 2,
        _ => 1,
    };
}

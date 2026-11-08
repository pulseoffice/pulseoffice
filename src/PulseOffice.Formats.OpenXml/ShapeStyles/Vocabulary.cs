using PulseOffice.Drawing.Shapes;

namespace PulseOffice.Formats.OpenXml;

internal sealed class DrawingVocabulary
{
    public TokenMap<ShapeStrokeStyle> Dashes { get; } = new(ShapeStrokeStyle.Solid, "solid",
        (ShapeStrokeStyle.Dashed, "dash"), (ShapeStrokeStyle.Dotted, "sysDot"), (ShapeStrokeStyle.Dotted, "dot"),
        (ShapeStrokeStyle.DashDot, "dashDot"), (ShapeStrokeStyle.DashDotDot, "lgDashDotDot"), (ShapeStrokeStyle.LongDash, "lgDash"));

    public TokenMap<LineCapStyle> Caps { get; } = new(LineCapStyle.Flat, "flat",
        (LineCapStyle.Round, "rnd"), (LineCapStyle.Square, "sq"));

    public TokenMap<ArrowEnd> Arrows { get; } = new(ArrowEnd.Arrow, "triangle",
        (ArrowEnd.None, "none"), (ArrowEnd.OpenArrow, "stealth"), (ArrowEnd.Circle, "oval"), (ArrowEnd.Diamond, "diamond"));

    public TokenMap<BevelStyle> Bevels { get; } = new(BevelStyle.Round, "circle",
        (BevelStyle.Angle, "angle"), (BevelStyle.SoftRound, "softRound"), (BevelStyle.Cross, "cross"), (BevelStyle.Relaxed, "relaxedInset"));

    public TokenMap<HatchKind> Hatches { get; } = new(HatchKind.Single, "ltUpDiag",
        (HatchKind.Double, "cross"), (HatchKind.Triple, "openDmnd"));

    public TokenMap<TextVerticalPlacement> Anchors { get; } = new(TextVerticalPlacement.Middle, "ctr",
        (TextVerticalPlacement.Top, "t"), (TextVerticalPlacement.Bottom, "b"));

    public TokenMap<TextHorizontalPlacement> Alignments { get; } = new(TextHorizontalPlacement.Centre, "ctr",
        (TextHorizontalPlacement.Left, "l"), (TextHorizontalPlacement.Right, "r"), (TextHorizontalPlacement.Justify, "just"));

    public TokenMap<ConnectorKind> Connectors { get; } = new(ConnectorKind.Elbow, "bentConnector3",
        (ConnectorKind.Straight, "straightConnector1"), (ConnectorKind.Curved, "curvedConnector3"));
}

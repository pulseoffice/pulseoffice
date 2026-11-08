namespace PulseOffice.Drawing.Shapes;

public static class ShapeFamilies
{
    public static readonly IReadOnlyList<ShapeKind> Basic =
    [
        ShapeKind.Rectangle, ShapeKind.RoundedRectangle, ShapeKind.Ellipse, ShapeKind.Triangle, ShapeKind.RightTriangle,
        ShapeKind.Diamond, ShapeKind.Parallelogram, ShapeKind.Trapezoid, ShapeKind.Pentagon, ShapeKind.Hexagon, ShapeKind.Octagon,
        ShapeKind.Cross, ShapeKind.Cylinder, ShapeKind.Frame, ShapeKind.Donut, ShapeKind.BlockArc,
        ShapeKind.Arc, ShapeKind.Pie, ShapeKind.Chord,
    ];

    public static readonly IReadOnlyList<ShapeKind> Arrows =
    [
        ShapeKind.ArrowRight, ShapeKind.ArrowLeft, ShapeKind.ArrowUp, ShapeKind.ArrowDown, ShapeKind.DoubleArrow, ShapeKind.Chevron,
    ];

    public static readonly IReadOnlyList<ShapeKind> Stars =
    [
        ShapeKind.Star4, ShapeKind.Star5, ShapeKind.Star6, ShapeKind.Star8,
    ];

    public static readonly IReadOnlyList<ShapeKind> Callouts =
    [
        ShapeKind.Callout, ShapeKind.Cloud,
    ];

    public static readonly IReadOnlyList<ShapeKind> FlowChart =
    [
        ShapeKind.FlowProcess, ShapeKind.FlowAlternateProcess, ShapeKind.FlowDecision, ShapeKind.FlowData,
        ShapeKind.FlowPredefinedProcess, ShapeKind.FlowInternalStorage, ShapeKind.FlowDocument, ShapeKind.FlowMultiDocument,
        ShapeKind.FlowTerminator, ShapeKind.FlowPreparation, ShapeKind.FlowManualInput, ShapeKind.FlowManualOperation,
        ShapeKind.FlowConnector, ShapeKind.FlowOffPageConnector, ShapeKind.FlowCard, ShapeKind.FlowPunchedTape,
        ShapeKind.FlowSummingJunction, ShapeKind.FlowOr, ShapeKind.FlowCollate, ShapeKind.FlowSort, ShapeKind.FlowExtract,
        ShapeKind.FlowMerge, ShapeKind.FlowStoredData, ShapeKind.FlowDelay, ShapeKind.FlowSequentialAccess,
        ShapeKind.FlowMagneticDisk, ShapeKind.FlowDirectAccess, ShapeKind.FlowDisplay,
    ];

    public static readonly IReadOnlyList<ShapeKind> Symbols =
    [
        ShapeKind.SmileyFace, ShapeKind.Sun, ShapeKind.Moon, ShapeKind.LightningBolt, ShapeKind.Heart, ShapeKind.Cloud,
        ShapeKind.NoSymbol, ShapeKind.Donut, ShapeKind.BlockArc, ShapeKind.FoldedCorner, ShapeKind.Bevel, ShapeKind.Frame,
        ShapeKind.Plaque, ShapeKind.BracketPair, ShapeKind.BracePair, ShapeKind.Cross, ShapeKind.Cylinder,
    ];
}

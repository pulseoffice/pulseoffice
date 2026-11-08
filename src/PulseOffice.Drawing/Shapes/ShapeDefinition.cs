using PulseOffice.Drawing;
using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Modules;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Drawing.Shapes;

public enum ShapeKind
{
    Rectangle, RoundedRectangle, Ellipse, Triangle, RightTriangle, Diamond,
    Parallelogram, Trapezoid, Pentagon, Hexagon, Octagon,
    Star4, Star5, Star6, Star8,
    ArrowRight, ArrowLeft, ArrowUp, ArrowDown, DoubleArrow, Chevron,
    Cross, Heart, Cylinder, Callout, Line,
    FlowProcess, FlowAlternateProcess, FlowDecision, FlowData, FlowPredefinedProcess, FlowInternalStorage,
    FlowDocument, FlowMultiDocument, FlowTerminator, FlowPreparation, FlowManualInput, FlowManualOperation,
    FlowConnector, FlowOffPageConnector, FlowCard, FlowPunchedTape, FlowSummingJunction, FlowOr, FlowCollate,
    FlowSort, FlowExtract, FlowMerge, FlowStoredData, FlowDelay, FlowSequentialAccess, FlowMagneticDisk,
    FlowDirectAccess, FlowDisplay,
    SmileyFace, Sun, Moon, LightningBolt, Cloud, NoSymbol, Donut, BlockArc, FoldedCorner, Bevel, Frame, Plaque,
    BracketPair, BracePair,
    Arc, Pie, Chord,
}

public sealed record ShapeObject : EmbeddedObject, IDrawingObject
{
    public TResult Accept<TResult>(IDrawingObjectVisitor<TResult> visitor) => visitor.VisitShape(this);

    public required ShapeKind Shape { get; init; }

    public Geometry.ComputedGeometry? Computed { get; init; }

    public ShapeStyle Style { get; init; } = ShapeStyle.Default;

    public string? StyleName { get; init; }

    public double RotationDegrees { get; init; }

    public bool FlipHorizontal { get; init; }

    public bool FlipVertical { get; init; }

    public double CornerRatio { get; init; } = 0.18;

    public double InnerRatio { get; init; } = 0.45;

    public double ArcStartDegrees { get; init; } = 90;

    public double ArcEndDegrees { get; init; }

    public string? Text { get; init; }

    public IReadOnlyList<ShapeTextRun>? Runs { get; init; }

    public IReadOnlyList<ShapeListItem?>? Lists { get; init; }

    public DimensionLine? Dimension { get; init; }

    public override EmbeddedObjectKind Kind => EmbeddedObjectKind.Shape;

    public override IReadOnlyCollection<ModuleKind> EditableIn =>
        [ModuleKind.Documents, ModuleKind.Sheets, ModuleKind.Presentations];
}

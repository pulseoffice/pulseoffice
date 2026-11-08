namespace PulseOffice.Drawing.Shapes;

public interface IExtendedShapes
{
    bool Knows(ShapeKind kind);

    ShapeOutline Outline(ShapeKind kind);
}

public sealed class ExtendedShapes : IExtendedShapes
{
    private readonly IReadOnlyDictionary<ShapeKind, Func<OutlineBuilder, OutlineBuilder>> _shapes =
        new Dictionary<ShapeKind, Func<OutlineBuilder, OutlineBuilder>>
        {
            [ShapeKind.FlowProcess] = b => b.Rectangle(0, 0, 1, 1),
            [ShapeKind.FlowAlternateProcess] = b => b.Move(0.12, 0).Line(0.88, 0).Cubic(0.95, 0, 1, 0.05, 1, 0.12).Line(1, 0.88)
                .Cubic(1, 0.95, 0.95, 1, 0.88, 1).Line(0.12, 1).Cubic(0.05, 1, 0, 0.95, 0, 0.88).Line(0, 0.12)
                .Cubic(0, 0.05, 0.05, 0, 0.12, 0).Close(),
            [ShapeKind.FlowDecision] = b => b.Polygon((0.5, 0), (1, 0.5), (0.5, 1), (0, 0.5)),
            [ShapeKind.FlowData] = b => b.Polygon((0.2, 0), (1, 0), (0.8, 1), (0, 1)),
            [ShapeKind.FlowPredefinedProcess] = b => b.Rectangle(0, 0, 1, 1).Polyline((0.12, 0), (0.12, 1)).Polyline((0.88, 0), (0.88, 1)),
            [ShapeKind.FlowInternalStorage] = b => b.Rectangle(0, 0, 1, 1).Polyline((0.15, 0), (0.15, 1)).Polyline((0, 0.15), (1, 0.15)),
            [ShapeKind.FlowDocument] = b => b.Move(0, 0).Line(1, 0).Line(1, 0.83).Cubic(0.75, 0.75, 0.5, 1.05, 0.25, 0.95)
                .Cubic(0.12, 0.9, 0.05, 0.85, 0, 0.83).Close(),
            [ShapeKind.FlowMultiDocument] = b => b.Polyline((0.1, 0.1), (0.1, 0), (1, 0), (1, 0.7), (0.9, 0.7))
                .Polyline((0.05, 0.15), (0.05, 0.05), (0.95, 0.05), (0.95, 0.75))
                .Move(0, 0.1).Line(0.9, 0.1).Line(0.9, 0.85).Cubic(0.68, 0.78, 0.45, 1.05, 0.22, 0.96).Cubic(0.1, 0.92, 0.04, 0.88, 0, 0.86).Close(),
            [ShapeKind.FlowTerminator] = b => b.Move(0.2, 0).Line(0.8, 0).Cubic(0.91, 0, 1, 0.22, 1, 0.5).Cubic(1, 0.78, 0.91, 1, 0.8, 1)
                .Line(0.2, 1).Cubic(0.09, 1, 0, 0.78, 0, 0.5).Cubic(0, 0.22, 0.09, 0, 0.2, 0).Close(),
            [ShapeKind.FlowPreparation] = b => b.Polygon((0.2, 0), (0.8, 0), (1, 0.5), (0.8, 1), (0.2, 1), (0, 0.5)),
            [ShapeKind.FlowManualInput] = b => b.Polygon((0, 0.2), (1, 0), (1, 1), (0, 1)),
            [ShapeKind.FlowManualOperation] = b => b.Polygon((0, 0), (1, 0), (0.8, 1), (0.2, 1)),
            [ShapeKind.FlowConnector] = b => b.Ellipse(0.5, 0.5, 0.5, 0.5),
            [ShapeKind.FlowOffPageConnector] = b => b.Polygon((0, 0), (1, 0), (1, 0.8), (0.5, 1), (0, 0.8)),
            [ShapeKind.FlowCard] = b => b.Polygon((0.2, 0), (1, 0), (1, 1), (0, 1), (0, 0.2)),
            [ShapeKind.FlowPunchedTape] = b => b.Move(0, 0.1).Cubic(0.25, -0.1, 0.25, 0.3, 0.5, 0.1).Cubic(0.75, -0.1, 0.75, 0.3, 1, 0.1)
                .Line(1, 0.9).Cubic(0.75, 1.1, 0.75, 0.7, 0.5, 0.9).Cubic(0.25, 1.1, 0.25, 0.7, 0, 0.9).Close(),
            [ShapeKind.FlowSummingJunction] = b => b.Ellipse(0.5, 0.5, 0.5, 0.5).Polyline((0.15, 0.15), (0.85, 0.85)).Polyline((0.85, 0.15), (0.15, 0.85)),
            [ShapeKind.FlowOr] = b => b.Ellipse(0.5, 0.5, 0.5, 0.5).Polyline((0.5, 0), (0.5, 1)).Polyline((0, 0.5), (1, 0.5)),
            [ShapeKind.FlowCollate] = b => b.Polygon((0, 0), (1, 0), (0, 1), (1, 1)),
            [ShapeKind.FlowSort] = b => b.Polygon((0.5, 0), (1, 0.5), (0.5, 1), (0, 0.5)).Polyline((0, 0.5), (1, 0.5)),
            [ShapeKind.FlowExtract] = b => b.Polygon((0.5, 0), (1, 1), (0, 1)),
            [ShapeKind.FlowMerge] = b => b.Polygon((0, 0), (1, 0), (0.5, 1)),
            [ShapeKind.FlowStoredData] = b => b.Move(0.17, 0).Line(1, 0).Cubic(0.9, 0.1, 0.83, 0.3, 0.83, 0.5).Cubic(0.83, 0.7, 0.9, 0.9, 1, 1)
                .Line(0.17, 1).Cubic(0.07, 1, 0, 0.78, 0, 0.5).Cubic(0, 0.22, 0.07, 0, 0.17, 0).Close(),
            [ShapeKind.FlowDelay] = b => b.Move(0, 0).Line(0.5, 0).Cubic(0.78, 0, 1, 0.22, 1, 0.5).Cubic(1, 0.78, 0.78, 1, 0.5, 1).Line(0, 1).Close(),
            [ShapeKind.FlowSequentialAccess] = b => b.Move(0.5, 1).Cubic(0.22, 1, 0, 0.78, 0, 0.5).Cubic(0, 0.22, 0.22, 0, 0.5, 0)
                .Cubic(0.78, 0, 1, 0.22, 1, 0.5).Cubic(1, 0.7, 0.9, 0.88, 0.75, 0.96).Line(1, 0.96).Line(1, 1).Close(),
            [ShapeKind.FlowMagneticDisk] = b => b.Move(0, 0.12).Cubic(0, -0.04, 1, -0.04, 1, 0.12).Line(1, 0.88).Cubic(1, 1.04, 0, 1.04, 0, 0.88).Close()
                .Move(0, 0.12).Cubic(0, 0.28, 1, 0.28, 1, 0.12),
            [ShapeKind.FlowDirectAccess] = b => b.Move(0.12, 0).Line(0.88, 0).Cubic(1.04, 0, 1.04, 1, 0.88, 1).Line(0.12, 1).Cubic(-0.04, 1, -0.04, 0, 0.12, 0).Close()
                .Move(0.88, 0).Cubic(0.72, 0, 0.72, 1, 0.88, 1),
            [ShapeKind.FlowDisplay] = b => b.Move(0, 0.5).Line(0.17, 0).Line(0.8, 0).Cubic(0.93, 0, 1, 0.22, 1, 0.5).Cubic(1, 0.78, 0.93, 1, 0.8, 1).Line(0.17, 1).Close(),
            [ShapeKind.SmileyFace] = b => b.Ellipse(0.5, 0.5, 0.5, 0.5).Ellipse(0.33, 0.37, 0.06, 0.06).Ellipse(0.67, 0.37, 0.06, 0.06)
                .Move(0.25, 0.65).Cubic(0.35, 0.82, 0.65, 0.82, 0.75, 0.65),
            [ShapeKind.Sun] = b => Sun(b),
            [ShapeKind.Moon] = b => b.Move(0.75, 0).Cubic(0.3, 0, 0, 0.25, 0, 0.5).Cubic(0, 0.75, 0.3, 1, 0.75, 1).Cubic(0.5, 0.85, 0.4, 0.68, 0.4, 0.5)
                .Cubic(0.4, 0.32, 0.5, 0.15, 0.75, 0).Close(),
            [ShapeKind.LightningBolt] = b => b.Polygon((0.39, 0), (0.62, 0.3), (0.53, 0.34), (0.8, 0.6), (0.71, 0.64), (1, 1), (0.48, 0.7), (0.57, 0.65), (0.2, 0.43), (0.31, 0.38), (0, 0.17)),
            [ShapeKind.Cloud] = b => b.Move(0.25, 0.85).Cubic(0.05, 0.85, 0, 0.6, 0.15, 0.52).Cubic(0.08, 0.35, 0.25, 0.22, 0.38, 0.28)
                .Cubic(0.45, 0.08, 0.7, 0.08, 0.72, 0.28).Cubic(0.9, 0.22, 1, 0.42, 0.9, 0.55).Cubic(1, 0.7, 0.9, 0.88, 0.75, 0.85).Close(),
            [ShapeKind.NoSymbol] = b => b.Ellipse(0.5, 0.5, 0.5, 0.5).Ellipse(0.5, 0.5, 0.36, 0.36).Polygon((0.22, 0.3), (0.3, 0.22), (0.78, 0.7), (0.7, 0.78)),
            [ShapeKind.Donut] = b => b.Ellipse(0.5, 0.5, 0.5, 0.5).Ellipse(0.5, 0.5, 0.3, 0.3),
            [ShapeKind.BlockArc] = b => b.Move(0, 0.5).Cubic(0, 0.22, 0.22, 0, 0.5, 0).Cubic(0.78, 0, 1, 0.22, 1, 0.5).Line(0.75, 0.5)
                .Cubic(0.75, 0.36, 0.64, 0.25, 0.5, 0.25).Cubic(0.36, 0.25, 0.25, 0.36, 0.25, 0.5).Close(),
            [ShapeKind.FoldedCorner] = b => b.Polygon((0, 0), (1, 0), (1, 0.8), (0.8, 1), (0, 1)).Polyline((0.8, 1), (0.84, 0.84), (1, 0.8)),
            [ShapeKind.Bevel] = b => b.Rectangle(0, 0, 1, 1).Rectangle(0.12, 0.12, 0.76, 0.76)
                .Polyline((0, 0), (0.12, 0.12)).Polyline((1, 0), (0.88, 0.12)).Polyline((1, 1), (0.88, 0.88)).Polyline((0, 1), (0.12, 0.88)),
            [ShapeKind.Frame] = b => b.Rectangle(0, 0, 1, 1).Rectangle(0.12, 0.12, 0.76, 0.76),
            [ShapeKind.Plaque] = b => b.Move(0.15, 0).Line(0.85, 0).Cubic(0.85, 0.08, 0.92, 0.15, 1, 0.15).Line(1, 0.85).Cubic(0.92, 0.85, 0.85, 0.92, 0.85, 1)
                .Line(0.15, 1).Cubic(0.15, 0.92, 0.08, 0.85, 0, 0.85).Line(0, 0.15).Cubic(0.08, 0.15, 0.15, 0.08, 0.15, 0).Close(),
            [ShapeKind.BracketPair] = b => b.Polyline((0.12, 0), (0, 0), (0, 1), (0.12, 1)).Polyline((0.88, 0), (1, 0), (1, 1), (0.88, 1)),
            [ShapeKind.BracePair] = b => b.Move(0.12, 0).Cubic(0.04, 0, 0.06, 0.45, 0, 0.5).Cubic(0.06, 0.55, 0.04, 1, 0.12, 1)
                .Move(0.88, 0).Cubic(0.96, 0, 0.94, 0.45, 1, 0.5).Cubic(0.94, 0.55, 0.96, 1, 0.88, 1),
        };

    public bool Knows(ShapeKind kind) => _shapes.ContainsKey(kind);

    public ShapeOutline Outline(ShapeKind kind) =>
        _shapes.TryGetValue(kind, out var build) ? build(new OutlineBuilder()).Build() : ShapeOutline.Empty;

    private static OutlineBuilder Sun(OutlineBuilder builder)
    {
        builder.Ellipse(0.5, 0.5, 0.22, 0.22);
        for (var ray = 0; ray < 8; ray++)
        {
            var angle = Math.PI * ray / 4;
            var side = 0.06;
            var (cos, sin) = (Math.Cos(angle), Math.Sin(angle));
            builder.Polygon(
                (0.5 + 0.3 * cos - side * sin, 0.5 + 0.3 * sin + side * cos),
                (0.5 + 0.5 * cos, 0.5 + 0.5 * sin),
                (0.5 + 0.3 * cos + side * sin, 0.5 + 0.3 * sin - side * cos));
        }
        return builder;
    }
}

using PulseOffice.Drawing;
using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Modules;

namespace PulseOffice.Drawing.Shapes;

public enum ConnectorKind
{
    Straight,
    Elbow,
    Curved,
}

public enum ConnectorSite
{
    Automatic,
    Left,
    Top,
    Right,
    Bottom,
}

public enum ArrowEnd
{
    None,
    Arrow,
    OpenArrow,
    Circle,
    Diamond,
}

public sealed record ConnectorEndpoint(string? ObjectId, ConnectorSite Site = ConnectorSite.Automatic)
{
    public static readonly ConnectorEndpoint Free = new((string?)null);

    public bool IsAttached => !string.IsNullOrEmpty(ObjectId);
}

public sealed record ConnectorObject : EmbeddedObject, IDrawingObject
{
    public TResult Accept<TResult>(IDrawingObjectVisitor<TResult> visitor) => visitor.VisitConnector(this);

    public ConnectorKind Shape { get; init; } = ConnectorKind.Elbow;

    public ConnectorEndpoint Start { get; init; } = ConnectorEndpoint.Free;

    public ConnectorEndpoint End { get; init; } = ConnectorEndpoint.Free;

    public ShapeStyle Style { get; init; } = ShapeStyle.Default with
    {
        Fill = ShapeFill.None,
        Line = ShapeLine.Default with { EndArrow = ArrowEnd.Arrow },
    };

    public ArrowEnd StartArrow => Style.Line.StartArrow;

    public ArrowEnd EndArrow => Style.Line.EndArrow;

    public ShapeOutline Path { get; init; } = ShapeOutline.Empty;

    public override EmbeddedObjectKind Kind => EmbeddedObjectKind.Shape;

    public override IReadOnlyCollection<ModuleKind> EditableIn =>
        [ModuleKind.Documents, ModuleKind.Sheets, ModuleKind.Presentations];
}

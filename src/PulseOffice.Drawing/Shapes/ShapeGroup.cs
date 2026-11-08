using PulseOffice.Drawing;
using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Modules;

namespace PulseOffice.Drawing.Shapes;

public sealed record GroupChild(EmbeddedObject Content, double X, double Y, double Width, double Height)
{
    public double RotationDegrees { get; init; }

    public string? Hyperlink { get; init; }

    public (double X, double Y, double Width, double Height) Scaled(double width, double height) =>
        (X * width, Y * height, Width * width, Height * height);
}

public sealed record ShapeGroupObject : EmbeddedObject, IDrawingObject
{
    public TResult Accept<TResult>(IDrawingObjectVisitor<TResult> visitor) => visitor.VisitGroup(this);

    public required IReadOnlyList<GroupChild> Children { get; init; }

    public double RotationDegrees { get; init; }

    public override EmbeddedObjectKind Kind => EmbeddedObjectKind.ShapeGroup;

    public override IReadOnlyCollection<ModuleKind> EditableIn =>
        [ModuleKind.Documents, ModuleKind.Sheets, ModuleKind.Presentations];
}

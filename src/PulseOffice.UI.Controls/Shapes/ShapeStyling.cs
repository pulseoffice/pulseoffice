using PulseOffice.Document.Embedding;
using PulseOffice.Drawing;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;

namespace PulseOffice.UI.Controls.Shapes;

public interface IShapeStyling
{
    StyledOutline? Editable(EmbeddedObject content);

    EmbeddedObject? Apply(EmbeddedObject content, StyledOutline edited);

    EmbeddedObject? Restyle(EmbeddedObject content, Func<ShapeStyle, ShapeStyle> change);
}

public sealed class ShapeStyling(IShapeGeometry geometry) : IShapeStyling
{
    public StyledOutline? Editable(EmbeddedObject content) =>
        content is IDrawingObject drawing ? drawing.Accept(new EditableVisitor(geometry)) : null;

    public EmbeddedObject? Apply(EmbeddedObject content, StyledOutline edited) =>
        content is IDrawingObject drawing ? drawing.Accept(new RestyleVisitor(edited)) : null;

    public EmbeddedObject? Restyle(EmbeddedObject content, Func<ShapeStyle, ShapeStyle> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        return Editable(content) is { } editable ? Apply(content, editable with { Style = change(editable.Style) }) : null;
    }

    private sealed class EditableVisitor(IShapeGeometry geometry) : DrawingObjectVisitor<StyledOutline?>
    {
        public override StyledOutline? VisitShape(ShapeObject shape) =>
            new(geometry.Outline(shape), shape.Style) { Text = shape.Text, RotationDegrees = shape.RotationDegrees };

        public override StyledOutline? VisitFreeform(FreeformObject freeform) =>
            new(freeform.Path.ToOutline(), freeform.Style) { RotationDegrees = freeform.RotationDegrees };

        protected override StyledOutline? Otherwise(EmbeddedObject content) => null;
    }

    private sealed class RestyleVisitor(StyledOutline edited) : DrawingObjectVisitor<EmbeddedObject?>
    {
        public override EmbeddedObject? VisitShape(ShapeObject shape) =>
            shape with
            {
                Style = edited.Style,
                Text = edited.Text,
                Runs = string.Equals(shape.Text, edited.Text, StringComparison.Ordinal) ? shape.Runs : null,
                RotationDegrees = edited.RotationDegrees,
            };

        public override EmbeddedObject? VisitFreeform(FreeformObject freeform) =>
            freeform with { Style = edited.Style, RotationDegrees = edited.RotationDegrees };

        protected override EmbeddedObject? Otherwise(EmbeddedObject content) => null;
    }
}

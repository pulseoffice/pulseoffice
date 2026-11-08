using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;

namespace PulseOffice.Presentations;

public sealed record EmbeddedStyled(EmbeddedObject Content, ShapeStyle Style, string? StyleName)
{
    public static EmbeddedStyled? Of(EmbeddedObject content) => content switch
    {
        ShapeObject shape => new(shape, shape.Style, shape.StyleName),
        FreeformObject freeform => new(freeform, freeform.Style, freeform.StyleName),
        _ => null,
    };

    public EmbeddedStyled With(ShapeStyle style, string? name) => Content switch
    {
        ShapeObject shape => new(shape with { Style = style, StyleName = name }, style, name),
        FreeformObject freeform => new(freeform with { Style = style, StyleName = name }, style, name),
        _ => this,
    };
}

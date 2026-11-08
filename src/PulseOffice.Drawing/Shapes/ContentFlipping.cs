using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Assets;

namespace PulseOffice.Drawing.Shapes;

public interface IContentFlipping
{
    bool Flips(EmbeddedObject content);

    EmbeddedObject Flip(EmbeddedObject content, bool horizontal);
}

public sealed class ContentFlipping : IContentFlipping
{
    public bool Flips(EmbeddedObject content) => content is ShapeObject or VectorGraphicObject;

    public EmbeddedObject Flip(EmbeddedObject content, bool horizontal) => content switch
    {
        ShapeObject shape => horizontal ? shape with { FlipHorizontal = !shape.FlipHorizontal } : shape with { FlipVertical = !shape.FlipVertical },
        VectorGraphicObject vector => horizontal ? vector with { FlipHorizontal = !vector.FlipHorizontal } : vector with { FlipVertical = !vector.FlipVertical },
        _ => content,
    };
}

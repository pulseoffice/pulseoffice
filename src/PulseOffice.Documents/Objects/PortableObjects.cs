using PulseOffice.Document.Embedding;
using PulseOffice.Documents.Blocks;
using PulseOffice.Drawing.Shapes;

namespace PulseOffice.Documents.Objects;

public interface IPortableObjects
{
    EmbeddedObject? Portable(AnchoredContent content);
}

public sealed class PortableObjects : IPortableObjects
{
    private readonly BlockText _text = new();

    public EmbeddedObject? Portable(AnchoredContent content) => content switch
    {
        EmbeddedContent { Value: ShapeObject shape, Story: { } story } => shape with { Text = _text.Of(story) is { Length: > 0 } text ? text : null },
        EmbeddedContent embedded => embedded.Value,
        _ => null,
    };
}

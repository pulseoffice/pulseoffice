using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Objects;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Documents.Layout.Flow;

public interface IShapeStorySizes
{
    bool Settle(TextDocument document, DocumentLayout layout);
}

public sealed class ShapeStorySizes : IShapeStorySizes
{
    private const int Tolerance = 1;

    public bool Settle(TextDocument document, DocumentLayout layout)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(layout);
        var settled = false;
        foreach (var box in layout.Pages.SelectMany(page => page.Objects).Where(box => box.Layer != ObjectLayer.Inline))
        {
            if (document.Objects.Find(box.ObjectId) is not { } item || !Resizes(item)) continue;
            var height = Twip.FromPoints(box.Bounds.Height);
            if (Math.Abs(height.Value - item.Size.Height.Value) <= Tolerance) continue;
            item.Size = new DocumentSize(item.Size.Width, height);
            settled = true;
        }
        return settled;
    }

    private bool Resizes(AnchoredObject item) =>
        item.Content is EmbeddedContent { Value: PulseOffice.Drawing.Shapes.ShapeObject { Dimension: null, Style.Text.Autofit: PulseOffice.Text.Rich.TextAutofit.ResizeShape } shape, Story: { } story }
        && shape.Shape != PulseOffice.Drawing.Shapes.ShapeKind.Line
        && story.Blocks.Count > 0;
}

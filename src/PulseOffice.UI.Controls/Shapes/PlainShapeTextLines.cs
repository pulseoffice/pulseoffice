using Avalonia;
using Avalonia.Media;
using PulseOffice.UI.Controls.Text.Rich;

namespace PulseOffice.UI.Controls.Shapes;

public interface IPlainShapeTextLines
{
    IReadOnlyList<RichTextLineBox> Of(FormattedText text, string content, Point origin, Matrix placement);
}

public sealed class PlainShapeTextLines : IPlainShapeTextLines
{
    public IReadOnlyList<RichTextLineBox> Of(FormattedText text, string content, Point origin, Matrix placement)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(content);
        var boxes = new List<RichTextLineBox>();
        var start = 0;
        var pieces = content.Split('\n');
        for (var index = 0; index < pieces.Length; index++)
        {
            var piece = pieces[index];
            if (piece.Trim().Length > 0 && text.BuildHighlightGeometry(origin, start, piece.Length) is { } area && area.Bounds is { Width: > 0 } bounds)
                boxes.Add(new RichTextLineBox(index, null, bounds.TransformToAABB(placement)));
            start += piece.Length + 1;
        }
        return boxes;
    }
}

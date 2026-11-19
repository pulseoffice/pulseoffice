using Avalonia;
using PulseOffice.Text.Rich;

namespace PulseOffice.UI.Controls.Text.Rich;

public sealed record RichTextLineBox(int Paragraph, Rect? Label, Rect Body);

public interface IRichTextLineBoxes
{
    IReadOnlyList<RichTextLineBox> Of(RichText text, ComposedTextFrame composed);
}

public sealed class RichTextLineBoxes : IRichTextLineBoxes
{
    public IReadOnlyList<RichTextLineBox> Of(RichText text, ComposedTextFrame composed)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(composed);
        var boxes = new List<RichTextLineBox>();
        var counted = -1;
        for (var index = 0; index < composed.Lines.Count; index++)
        {
            var line = composed.Lines[index];
            if (line.Offset == 0) counted++;
            var paragraph = IndexOf(text, line.Paragraph) ?? Math.Max(0, counted);
            Rect? body = null;
            foreach (var segment in line.Segments)
            {
                var area = new Rect(composed.Origin(index, segment), new Size(segment.Text.WidthIncludingTrailingWhitespace, segment.Text.Height));
                body = body is { } known ? known.Union(area) : area;
            }
            var label = line.Label is { } shown
                ? new Rect(composed.Left(index) + line.LabelX, composed.Tops[index], shown.WidthIncludingTrailingWhitespace, shown.Height).TransformToAABB(composed.Placement)
                : (Rect?)null;
            if (body is { } bounds) boxes.Add(new RichTextLineBox(paragraph, label, bounds.TransformToAABB(composed.Placement)));
        }
        return boxes;
    }

    private int? IndexOf(RichText text, TextParagraph paragraph)
    {
        for (var index = 0; index < text.Paragraphs.Count; index++)
            if (ReferenceEquals(text.Paragraphs[index], paragraph)) return index;
        return null;
    }
}

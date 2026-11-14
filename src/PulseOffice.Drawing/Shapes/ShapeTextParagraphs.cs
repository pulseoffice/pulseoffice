using PulseOffice.Text.Rich;

namespace PulseOffice.Drawing.Shapes;

public interface IShapeTextParagraphs
{
    TextParagraph First(ShapeObject shape, int start, int end);

    ShapeObject Apply(ShapeObject shape, int start, int end, Func<TextParagraph, TextParagraph> change);
}

public sealed class ShapeTextParagraphs(IShapeTextLists lists, IShapeRichText rich) : IShapeTextParagraphs
{
    public ShapeTextParagraphs() : this(new ShapeTextLists(), new ShapeRichText())
    {
    }

    public TextParagraph First(ShapeObject shape, int start, int end)
    {
        ArgumentNullException.ThrowIfNull(shape);
        var paragraphs = rich.ToRich(shape).Paragraphs;
        if (paragraphs.Count == 0) return new TextParagraph(string.Empty);
        if (start < 0 || end < 0) return paragraphs[0];
        var plain = string.Join('\n', paragraphs.Select(paragraph => paragraph.Text));
        var index = plain.AsSpan(0, Math.Clamp(Math.Min(start, end), 0, plain.Length)).Count('\n');
        return paragraphs[Math.Clamp(index, 0, paragraphs.Count - 1)];
    }

    public ShapeObject Apply(ShapeObject shape, int start, int end, Func<TextParagraph, TextParagraph> change)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(change);
        var size = shape.Style.Text.Size;
        var current = rich.ToRich(shape).Paragraphs;
        var changed = Range(current.Count, start, end, shape).Select(index => (Index: index, Paragraph: change(current[index]))).ToList();
        var directions = lists.Directions(shape).ToList();
        foreach (var (index, paragraph) in changed.Where(entry => entry.Index < directions.Count)) directions[index] = paragraph.Direction;
        var listed = lists.Apply(shape, start, end, item => Regenerated(item, rich.Item(change(rich.Listed(new TextParagraph(string.Empty) { FontSize = size }, item)))));
        return lists.WithDirections(listed, directions);
    }

    private IEnumerable<int> Range(int count, int start, int end, ShapeObject shape)
    {
        if (count == 0) return [];
        if (start < 0 || end < 0) return Enumerable.Range(0, count);
        var plain = string.Join('\n', rich.ToRich(shape).Paragraphs.Select(paragraph => paragraph.Text));
        int At(int offset) => Math.Clamp(plain.AsSpan(0, Math.Clamp(offset, 0, plain.Length)).Count('\n'), 0, count - 1);
        var first = At(Math.Min(start, end));
        return Enumerable.Range(first, At(Math.Max(start, end)) - first + 1);
    }

    private ShapeListItem? Regenerated(ShapeListItem? before, ShapeListItem? after) =>
        after is not null && before?.Level != after.Level ? after with { Margin = null, Indent = null } : after;
}

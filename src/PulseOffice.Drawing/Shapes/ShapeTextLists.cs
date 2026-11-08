using PulseOffice.Text.Rich;

namespace PulseOffice.Drawing.Shapes;

public interface IShapeTextLists
{
    IReadOnlyList<ShapeListItem?> Items(ShapeObject shape);

    bool Listed(ShapeObject shape);

    ShapeObject WithItems(ShapeObject shape, IEnumerable<ShapeListItem?> items);

    ShapeObject Apply(ShapeObject shape, int start, int end, Func<ShapeListItem?, ShapeListItem?> change);

    ShapeObject Spliced(ShapeObject shape, string before, int offset, string removed, string inserted);
}

public sealed class ShapeTextLists(IListNumberFormats numbers) : IShapeTextLists
{
    public ShapeTextLists() : this(new ListNumberFormats())
    {
    }

    public const int LevelCount = 9;

    public IReadOnlyList<ShapeListItem?> Items(ShapeObject shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        var count = Paragraphs(Plain(shape));
        var own = shape.Lists ?? [];
        return [.. Enumerable.Range(0, count).Select(index => index < own.Count ? own[index] : null)];
    }

    public bool Listed(ShapeObject shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        return shape.Lists?.Any(item => item is not null) == true;
    }

    public ShapeObject WithItems(ShapeObject shape, IEnumerable<ShapeListItem?> items)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(items);
        var count = Paragraphs(Plain(shape));
        var kept = items.Take(count).Select(Clamped).ToList();
        while (kept.Count > 0 && kept[^1] is null) kept.RemoveAt(kept.Count - 1);
        return shape with { Lists = kept.Count == 0 ? null : kept };
    }

    public ShapeObject Apply(ShapeObject shape, int start, int end, Func<ShapeListItem?, ShapeListItem?> change)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(change);
        var plain = Plain(shape);
        var items = Items(shape).ToList();
        var (first, last) = start < 0 || end < 0
            ? (0, items.Count - 1)
            : (Paragraph(plain, Math.Min(start, end)), Paragraph(plain, Math.Max(start, end)));
        for (var index = first; index <= last && index < items.Count; index++) items[index] = change(items[index]);
        return WithItems(shape, items);
    }

    public ShapeObject Spliced(ShapeObject shape, string before, int offset, string removed, string inserted)
    {
        ArgumentNullException.ThrowIfNull(shape);
        if (shape.Lists is not { Count: > 0 } lists) return shape;
        var at = Paragraph(before, offset);
        var merged = removed.Count(character => character == '\n');
        var added = inserted.Count(character => character == '\n');
        var items = Enumerable.Range(0, Paragraphs(before)).Select(index => index < lists.Count ? lists[index] : null).ToList();
        var carried = items[at];
        items.RemoveRange(at + 1, Math.Min(merged, items.Count - at - 1));
        items.InsertRange(at + 1, Enumerable.Repeat(carried, added));
        return WithItems(shape, items);
    }

    public const double LargestIndent = 1584;

    private ShapeListItem? Clamped(ShapeListItem? item) =>
        item is null ? null : item with
        {
            Level = Math.Clamp(item.Level, 0, LevelCount - 1),
            Margin = Bounded(item.Margin, 0),
            Indent = Bounded(item.Indent, -LargestIndent),
            NumberFormat = item.Numbered ? numbers.Normalized(item.NumberFormat) : null,
        };

    private double? Bounded(double? value, double lowest) =>
        value is { } number && double.IsFinite(number) ? Math.Clamp(number, lowest, LargestIndent) : null;

    private string Plain(ShapeObject shape) =>
        shape.Runs is { Count: > 0 } runs ? string.Concat(runs.Select(run => run.Text)) : shape.Text ?? string.Empty;

    private int Paragraphs(string text) => text.Count(character => character == '\n') + 1;

    private int Paragraph(string text, int offset) =>
        text.AsSpan(0, Math.Clamp(offset, 0, text.Length)).Count('\n');
}

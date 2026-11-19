namespace PulseOffice.Drawing.Shapes;

public enum ShapeListKey
{
    Indent,
    Outdent,
    Return,
    Backspace,
}

public interface IShapeListKeys
{
    ShapeObject? Press(ShapeObject shape, int start, int end, ShapeListKey key);
}

public sealed class ShapeListKeys(IShapeTextLists lists, IShapeTextEditing editing) : IShapeListKeys
{
    public ShapeListKeys() : this(new ShapeTextLists(), new ShapeTextEditing())
    {
    }

    public ShapeObject? Press(ShapeObject shape, int start, int end, ShapeListKey key)
    {
        ArgumentNullException.ThrowIfNull(shape);
        var text = editing.PlainText(shape);
        var (from, to) = (Math.Clamp(Math.Min(start, end), 0, text.Length), Math.Clamp(Math.Max(start, end), 0, text.Length));
        var items = lists.Items(shape);
        var paragraph = text[..from].Count(character => character == '\n');
        var item = paragraph < items.Count ? items[paragraph] : null;
        var lineStart = from == 0 ? 0 : text.LastIndexOf('\n', from - 1) + 1;
        var lineEnd = text.IndexOf('\n', from) is var found and >= 0 ? found : text.Length;

        return key switch
        {
            ShapeListKey.Indent or ShapeListKey.Outdent => Leveled(shape, text, from, to, key == ShapeListKey.Indent ? 1 : -1),
            ShapeListKey.Return when item is not null && from == to && lineStart == lineEnd => item.Level > 0
                ? lists.Apply(shape, from, from, listed => listed is null ? null : listed with { Level = listed.Level - 1 })
                : lists.Apply(shape, from, from, _ => null),
            ShapeListKey.Backspace when item is not null && from == to && from == lineStart => lists.Apply(shape, from, from, _ => null),
            _ => null,
        };
    }

    private ShapeObject? Leveled(ShapeObject shape, string text, int from, int to, int step)
    {
        var items = lists.Items(shape);
        var first = text[..from].Count(character => character == '\n');
        var last = text[..to].Count(character => character == '\n');
        if (!items.Skip(first).Take(last - first + 1).Any(item => item is not null)) return null;
        return lists.Apply(shape, from, to, item => item is null ? null : item with { Level = Math.Clamp(item.Level + step, 0, ShapeTextLists.LevelCount - 1) });
    }
}

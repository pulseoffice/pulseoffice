namespace PulseOffice.Drawing.Shapes;

public interface IShapeTextEditing
{
    string PlainText(ShapeObject shape);

    ShapeObject WithText(ShapeObject shape, string text);

    ShapeTextRun RunAt(ShapeObject shape, int start, int end);

    ShapeObject Format(ShapeObject shape, int start, int end, Func<ShapeTextRun, ShapeTextRun> change);

    ShapeObject WithRuns(ShapeObject shape, IEnumerable<ShapeTextRun> runs);

    bool Formatted(ShapeObject shape);

    string Normalized(string text);
}

public sealed class ShapeTextEditing(IShapeTextLists lists) : IShapeTextEditing
{
    public ShapeTextEditing() : this(new ShapeTextLists())
    {
    }

    public string PlainText(ShapeObject shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        return shape.Runs is { Count: > 0 } runs ? string.Concat(runs.Select(run => run.Text)) : shape.Text ?? string.Empty;
    }

    public ShapeObject WithText(ShapeObject shape, string text)
    {
        ArgumentNullException.ThrowIfNull(shape);
        var edited = Normalized(text ?? string.Empty);
        var before = PlainText(shape);
        if (edited == before) return shape;

        var prefix = 0;
        while (prefix < before.Length && prefix < edited.Length && before[prefix] == edited[prefix]) prefix++;
        var suffix = 0;
        while (suffix < before.Length - prefix && suffix < edited.Length - prefix
               && before[before.Length - 1 - suffix] == edited[edited.Length - 1 - suffix]) suffix++;

        var runs = Runs(shape);
        var inserted = edited.Substring(prefix, edited.Length - prefix - suffix);
        var template = Template(runs, prefix);
        var head = Slice(runs, 0, prefix);
        var tail = Slice(runs, before.Length - suffix, before.Length);
        var next = head.Append(template with { Text = inserted }).Concat(tail).ToList();
        var removed = before.Substring(prefix, before.Length - prefix - suffix);
        var rebuilt = Rebuilt(shape, next);
        return rebuilt.Text is null ? rebuilt : lists.Spliced(rebuilt, before, prefix, removed, inserted);
    }

    public ShapeTextRun RunAt(ShapeObject shape, int start, int end)
    {
        ArgumentNullException.ThrowIfNull(shape);
        var runs = Runs(shape);
        var (from, to) = Range(PlainText(shape).Length, start, end);
        var covered = Slice(runs, from, to).FirstOrDefault(run => run.Text.Length > 0);
        return (covered ?? Template(runs, from)) with { Text = string.Empty };
    }

    public ShapeObject Format(ShapeObject shape, int start, int end, Func<ShapeTextRun, ShapeTextRun> change)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(change);
        var length = PlainText(shape).Length;
        if (length == 0) return shape;
        var (from, to) = Range(length, start, end);
        var runs = Runs(shape);
        var next = Slice(runs, 0, from)
            .Concat(Slice(runs, from, to).Select(run => change(run) with { Text = run.Text }))
            .Concat(Slice(runs, to, length))
            .ToList();
        return Rebuilt(shape, next);
    }

    public ShapeObject WithRuns(ShapeObject shape, IEnumerable<ShapeTextRun> runs)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(runs);
        var style = shape.Style.Text;
        return Rebuilt(shape, [.. runs.Select(run => run with
        {
            Bold = run.Bold && !style.Bold,
            Italic = run.Italic && !style.Italic,
            Underline = run.Underline && !style.Underline,
            Size = run.Size is { } size && size > 0 && Math.Abs(size - style.Size) > 0.01 ? size : null,
            Family = run.Family is { Length: > 0 } family && family != style.Family ? family : null,
            ColorHex = run.ColorHex is { Length: > 0 } colour && !(string.Equals(colour, style.ColorHex, StringComparison.OrdinalIgnoreCase) && !style.AutomaticColor) ? colour.ToUpperInvariant() : null,
        })]);
    }

    public bool Formatted(ShapeObject shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        return shape.Runs?.Any(run => run with { Text = string.Empty } != new ShapeTextRun(string.Empty)) == true;
    }

    private (int From, int To) Range(int length, int start, int end)
    {
        var from = Math.Clamp(Math.Min(start, end), 0, length);
        var to = Math.Clamp(Math.Max(start, end), 0, length);
        return from == to ? (0, length) : (from, to);
    }

    public string Normalized(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');

    private List<ShapeTextRun> Runs(ShapeObject shape) =>
        shape.Runs is { Count: > 0 } runs ? [.. runs] : [new ShapeTextRun(shape.Text ?? string.Empty)];

    private ShapeTextRun Template(IReadOnlyList<ShapeTextRun> runs, int offset)
    {
        var position = 0;
        ShapeTextRun? previous = null;
        foreach (var run in runs)
        {
            if (offset < position + run.Text.Length || offset == position + run.Text.Length && offset > position) return run with { Link = null };
            if (run.Text.Length > 0) previous = run;
            position += run.Text.Length;
        }
        return (previous ?? runs.FirstOrDefault() ?? new ShapeTextRun(string.Empty)) with { Link = null };
    }

    private IEnumerable<ShapeTextRun> Slice(IReadOnlyList<ShapeTextRun> runs, int from, int to)
    {
        var position = 0;
        foreach (var run in runs)
        {
            var start = Math.Max(from, position);
            var end = Math.Min(to, position + run.Text.Length);
            if (end > start) yield return run with { Text = run.Text.Substring(start - position, end - start) };
            position += run.Text.Length;
        }
    }

    private ShapeObject Rebuilt(ShapeObject shape, List<ShapeTextRun> runs)
    {
        var merged = new List<ShapeTextRun>();
        foreach (var run in runs.Where(run => run.Text.Length > 0))
        {
            if (merged.Count > 0 && merged[^1] with { Text = string.Empty } == run with { Text = string.Empty })
                merged[^1] = merged[^1] with { Text = merged[^1].Text + run.Text };
            else merged.Add(run);
        }

        var plain = string.Concat(merged.Select(run => run.Text));
        if (plain.Length == 0) return shape with { Text = null, Runs = null, Lists = null, Directions = null };
        var plainRuns = merged.Count == 1 && merged[0] == new ShapeTextRun(plain);
        return shape with { Text = plain, Runs = plainRuns ? null : merged };
    }
}

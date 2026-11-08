using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Inline;
using PulseOffice.Documents.Layout.Lines;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Layout.Flow;

public sealed record ParagraphFlow(
    double SpaceBefore,
    double SpaceAfter,
    bool KeepWithNext,
    bool KeepTogether,
    int Widows,
    int Orphans,
    bool PageBreakBefore,
    bool Contextual,
    string? PageStyleName,
    string? StyleName)
{
    public int? PageNumberRestart { get; init; }

    public bool ColumnBreakBefore { get; init; }

    public string? NextPageStyleName { get; init; }
}

public sealed record ParagraphDecoration(uint? Background, BoxBorders Borders)
{
    public double Inset(BorderSide side) => Borders.Inset(side);
}

public interface IParagraphComposition
{
    IReadOnlyList<ComposedLine> Compose(ParagraphEntry entry, double columnWidth);

    IReadOnlyList<ComposedLine> Compose(ParagraphEntry entry, double columnWidth, int start, int firstIndex);

    ComposedLine ComposeLine(ParagraphEntry entry, double columnWidth, double left, double right, int start, int index);
}

public sealed class ParagraphComposition(ILineBreaker breaker, ILineComposer composer) : IParagraphComposition
{
    public IReadOnlyList<ComposedLine> Compose(ParagraphEntry entry, double columnWidth) => Compose(entry, columnWidth, 0, 0);

    public IReadOnlyList<ComposedLine> Compose(ParagraphEntry entry, double columnWidth, int start, int firstIndex)
    {
        var spans = breaker.Break(entry.Shaped, entry.Frame, columnWidth, start, firstIndex);
        var request = new CompositionRequest(entry.Block.Id, entry.Shaped, entry.Frame, entry.Spacing, columnWidth);
        return [.. spans.Select((span, index) => composer.Compose(request, span, firstIndex + index))];
    }

    public ComposedLine ComposeLine(ParagraphEntry entry, double columnWidth, double left, double right, int start, int index)
    {
        var frame = entry.Frame.Narrowed(left, columnWidth - right);
        var width = right - left;
        var span = breaker.BreakLine(entry.Shaped, frame, start, index, width - frame.MarginLeft - frame.MarginRight);
        var request = new CompositionRequest(entry.Block.Id, entry.Shaped, frame, entry.Spacing, width);
        return composer.Compose(request, span, index);
    }
}

public sealed class ParagraphEntry(
    ParagraphBlock block,
    TextParagraph content,
    string label,
    ShapedParagraph shaped,
    ParagraphFrame frame,
    LineSpacingRule spacing,
    ParagraphFlow flow,
    IParagraphComposition composition) : FlowItem
{
    private readonly Dictionary<(double Width, int Start), IReadOnlyList<ComposedLine>> _lines = [];

    public ParagraphBlock Block { get; } = block;

    public TextParagraph Content { get; } = content;

    public string Label { get; } = label;

    public ShapedParagraph Shaped { get; } = shaped;

    public ParagraphFrame Frame { get; } = frame;

    public LineSpacingRule Spacing { get; } = spacing;

    public ParagraphFlow Flow { get; } = flow;

    public StoryKind Story { get; init; } = StoryKind.Body;

    public string? StoryId { get; init; }

    public ParagraphDecoration? Decoration { get; init; }

    public IReadOnlyList<string> AnchoredObjects { get; init; } = [];

    public IReadOnlyList<object> ExtraParts { get; init; } = [];

    public Func<string, ParagraphEntry>? PerPage { get; init; }

    public override FlowKey Key => field ??= new(Block.Id, [Content, .. ExtraParts], Label);

    public override T Accept<T>(IFlowItemVisitor<T> visitor) => visitor.VisitParagraph(this);

    public IReadOnlyList<ComposedLine> Lines(double columnWidth) => LinesFrom(columnWidth, 0, 0);

    public IReadOnlyList<ComposedLine> LinesFrom(double columnWidth, int start, int firstIndex)
    {
        if (_lines.TryGetValue((columnWidth, start), out var lines)) return lines;
        if (start == 0)
        {
            lines = composition.Compose(this, columnWidth);
        }
        else
        {
            var whole = LinesFrom(columnWidth, 0, 0);
            lines = firstIndex > 0 && firstIndex < whole.Count && whole[firstIndex - 1].Next == start
                ? new ComposedLineSuffix(whole, firstIndex)
                : composition.Compose(this, columnWidth, start, firstIndex);
        }
        _lines[(columnWidth, start)] = lines;
        return lines;
    }

    private sealed class ComposedLineSuffix(IReadOnlyList<ComposedLine> whole, int from) : IReadOnlyList<ComposedLine>
    {
        public ComposedLine this[int index] => index >= 0 && index < Count ? whole[from + index] : throw new ArgumentOutOfRangeException(nameof(index));

        public int Count => whole.Count - from;

        public IEnumerator<ComposedLine> GetEnumerator()
        {
            for (var index = from; index < whole.Count; index++) yield return whole[index];
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public ComposedLine LineIn(double columnWidth, double left, double right, int start, int index) =>
        composition.ComposeLine(this, columnWidth, left, right, start, index);
}

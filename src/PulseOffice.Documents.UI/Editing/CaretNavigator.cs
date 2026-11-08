using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Layout.Queries;
using PulseOffice.Documents.Layout.Tree;

namespace PulseOffice.Documents.UI.Editing;

public enum CaretMotion
{
    Left,
    Right,
    WordLeft,
    WordRight,
    Up,
    Down,
    LineStart,
    LineEnd,
    DocumentStart,
    DocumentEnd,
    PageUp,
    PageDown,
}

public sealed record CaretTarget(DocumentPosition Position, double? PreferredX);

public interface ICaretNavigator
{
    CaretTarget Move(TextDocument document, DocumentLayout layout, DocumentPosition caret, CaretMotion motion, double? preferredX, int pageLines);
}

public sealed class CaretNavigator(IStoryCatalog stories, IParagraphOrder order,ILayoutLines lines, ILineOffsets geometry, PulseOffice.Text.Rich.ITextClusters clusters, PulseOffice.Text.Rich.IWordMotion words) : ICaretNavigator
{
    public CaretTarget Move(TextDocument document, DocumentLayout layout, DocumentPosition caret, CaretMotion motion, double? preferredX, int pageLines) =>
        Plain(document, layout, caret, Reading(layout, caret, motion), preferredX, pageLines);

    private CaretMotion Reading(DocumentLayout layout, DocumentPosition caret, CaretMotion motion)
    {
        if (motion is not (CaretMotion.Left or CaretMotion.Right or CaretMotion.Up or CaretMotion.Down)) return motion;
        if (lines.LineOf(layout, caret) is not { } current) return motion;
        return lines.TurnOf(layout, current) switch
        {
            90 => motion switch
            {
                CaretMotion.Up => CaretMotion.Left,
                CaretMotion.Down => CaretMotion.Right,
                CaretMotion.Left => CaretMotion.Down,
                _ => CaretMotion.Up,
            },
            270 => motion switch
            {
                CaretMotion.Up => CaretMotion.Right,
                CaretMotion.Down => CaretMotion.Left,
                CaretMotion.Left => CaretMotion.Up,
                _ => CaretMotion.Down,
            },
            _ => motion,
        };
    }

    private CaretTarget Plain(TextDocument document, DocumentLayout layout, DocumentPosition caret, CaretMotion motion, double? preferredX, int pageLines) => motion switch
    {
        CaretMotion.Left => new(Visual(document, layout, caret, -1), null),
        CaretMotion.Right => new(Visual(document, layout, caret, 1), null),
        CaretMotion.WordLeft => new(Word(document, caret, -1), null),
        CaretMotion.WordRight => new(Word(document, caret, 1), null),
        CaretMotion.Up => Vertical(document, layout, caret, preferredX, 1, down: false),
        CaretMotion.Down => Vertical(document, layout, caret, preferredX, 1, down: true),
        CaretMotion.PageUp => Vertical(document, layout, caret, preferredX, Math.Max(1, pageLines), down: false),
        CaretMotion.PageDown => Vertical(document, layout, caret, preferredX, Math.Max(1, pageLines), down: true),
        CaretMotion.LineStart => new(Edge(layout, caret, start: true), null),
        CaretMotion.LineEnd => new(Edge(layout, caret, start: false), null),
        CaretMotion.DocumentStart => new(new DocumentPosition(order.StoryOf(document, caret.ParagraphId)[0].Id, 0), null),
        CaretMotion.DocumentEnd => new(End(order.StoryOf(document, caret.ParagraphId)[^1]), null),
        _ => new(caret, preferredX),
    };

    private DocumentPosition Step(TextDocument document, DocumentPosition caret, int direction)
    {
        var text = stories.Find(document, caret.ParagraphId).Paragraph.Text;
        if (direction < 0 && caret.Offset > 0) return caret with { Offset = clusters.Previous(text, caret.Offset) };
        if (direction > 0 && caret.Offset < text.Length) return caret with { Offset = clusters.Next(text, caret.Offset) };
        var story = order.StoryOf(document, caret.ParagraphId);
        var index = IndexIn(story, caret.ParagraphId);
        if (index < 0) return caret;
        if (direction < 0) return index > 0 ? End(story[index - 1]) : caret;
        return index + 1 < story.Count ? new DocumentPosition(story[index + 1].Id, 0) : caret;
    }

    private DocumentPosition Visual(TextDocument document, DocumentLayout layout, DocumentPosition caret, int direction)
    {
        if (lines.LineOf(layout, caret) is not { } current || !current.Line.Line.Bidirectional || lines.TurnOf(layout, current) != 0)
            return Step(document, caret, direction);
        var line = current.Line;
        var text = stories.Find(document, caret.ParagraphId).Paragraph.Text;
        var start = geometry.StartOf(line);
        var end = Math.Min(text.Length, geometry.EndOf(line));
        var here = geometry.XOf(line, caret.Offset);
        int? best = null;
        var bestX = 0.0;
        for (var offset = start; offset <= end; offset = offset < end ? Math.Max(offset + 1, clusters.Next(text, offset)) : end + 1)
        {
            if (offset == caret.Offset) continue;
            var x = geometry.XOf(line, offset);
            var ahead = direction > 0 ? x > here + Tolerance : x < here - Tolerance;
            if (!ahead || (best is { } chosen && !Closer(x, offset, bestX, chosen, caret.Offset, direction))) continue;
            best = offset;
            bestX = x;
        }
        if (best is { } found) return caret with { Offset = found };
        var forward = direction > 0 != line.Line.RightToLeft;
        if (forward) return line.Line.End < text.Length ? caret with { Offset = line.Line.End } : Step(document, caret with { Offset = text.Length }, 1);
        return Step(document, caret with { Offset = start }, -1);
    }

    private const double Tolerance = 0.01;

    private bool Closer(double x, int offset, double bestX, int best, int origin, int direction)
    {
        if (Math.Abs(x - bestX) <= Tolerance) return Math.Abs(offset - origin) < Math.Abs(best - origin);
        return direction > 0 ? x < bestX : x > bestX;
    }

    private int IndexIn(IReadOnlyList<PulseOffice.Documents.Blocks.ParagraphBlock> story, string paragraphId)
    {
        for (var index = 0; index < story.Count; index++)
            if (story[index].Id == paragraphId) return index;
        return -1;
    }

    private DocumentPosition Word(TextDocument document, DocumentPosition caret, int direction)
    {
        var story = order.StoryOf(document, caret.ParagraphId);
        var paragraph = story.First(candidate => candidate.Id == caret.ParagraphId);
        var text = paragraph.Text;
        var offset = caret.Offset;
        if (direction < 0)
            return offset == 0 ? Step(document, caret, -1) : caret with { Offset = words.Previous(text, offset) };
        return offset >= text.Length ? Step(document, caret, 1) : caret with { Offset = words.Next(text, offset) };
    }

    private CaretTarget Vertical(TextDocument document, DocumentLayout layout, DocumentPosition caret, double? preferredX, int count, bool down)
    {
        if (lines.LineOf(layout, caret) is not { } current) return new(caret, preferredX);
        var x = preferredX ?? geometry.XOf(current.Line, caret.Offset);
        var line = current;
        var scope = Scope(document, current.Line, caret.ParagraphId);
        for (var i = 0; i < count; i++)
        {
            var next = down ? lines.Below(layout, line, x, scope) : lines.Above(layout, line, x, scope);
            if (next is null) break;
            line = next;
        }
        if (ReferenceEquals(line, current) && lines.TurnOf(layout, current) != 0)
            return Sideways(document, caret, down) is { } outside ? new(outside, null) : new(caret with { Offset = down ? geometry.EndOf(line.Line) : geometry.StartOf(line.Line) }, x);
        if (ReferenceEquals(line, current))
            return new(down ? caret with { Offset = geometry.EndOf(line.Line) } : caret with { Offset = geometry.StartOf(line.Line) }, x);
        return new(new DocumentPosition(line.Line.ParagraphId, geometry.OffsetAt(line.Line, x)), x);
    }

    private LineScope Scope(TextDocument document, LineBox line, string paragraphId) => new(
        order.StoryOf(document, paragraphId).Select(paragraph => paragraph.Id).ToHashSet(StringComparer.Ordinal),
        !Repeated(document, stories.Find(document, paragraphId).Story),
        line.Story is StoryKind.Footnote or StoryKind.Endnote ? line.Story : null);

    private bool Repeated(TextDocument document, PulseOffice.Documents.Blocks.BlockContainer story) =>
        document.Styles.Page.All.SelectMany(page => new[] { page.Header, page.Footer })
            .Any(part => ReferenceEquals(part.Default, story) || ReferenceEquals(part.First, story) || ReferenceEquals(part.Even, story));

    private DocumentPosition? Sideways(TextDocument document, DocumentPosition caret, bool down)
    {
        var story = order.StoryOf(document, caret.ParagraphId);
        var index = IndexIn(story, caret.ParagraphId);
        if (index < 0) return null;
        if (down) return index + 1 < story.Count ? new DocumentPosition(story[index + 1].Id, 0) : null;
        return index > 0 ? End(story[index - 1]) : null;
    }

    private DocumentPosition Edge(DocumentLayout layout, DocumentPosition caret, bool start)
    {
        if (lines.LineOf(layout, caret) is not { } current) return caret;
        return caret with { Offset = start ? geometry.StartOf(current.Line) : geometry.EndOf(current.Line) };
    }

    private DocumentPosition End(PulseOffice.Documents.Blocks.ParagraphBlock paragraph) => new(paragraph.Id, paragraph.Length);
}

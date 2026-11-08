using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Commenting;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using LayoutGlyphRun = PulseOffice.Documents.Layout.Tree.GlyphRun;

namespace PulseOffice.Documents.UI.Rendering;

public sealed record FolioMarkIndex(
    IReadOnlyDictionary<string, (int Level, string Title)> Headings,
    IReadOnlyDictionary<string, IReadOnlyList<(string Name, int Offset)>> Targets,
    IReadOnlyDictionary<string, IReadOnlyList<(int Offset, PrintNote Note)>> Notes)
{
    public static readonly FolioMarkIndex Empty = new(
        new Dictionary<string, (int, string)>(), new Dictionary<string, IReadOnlyList<(string, int)>>(), new Dictionary<string, IReadOnlyList<(int, PrintNote)>>());
}

public interface IFolioPrintMarks
{
    FolioMarkIndex Index(TextDocument document);

    void Mark(IPageCanvas canvas, PageBox page, FolioMarkIndex index);
}

public sealed class FolioPrintMarks(IDocumentOutline outline, IBookmarkCatalog bookmarks, ICommentAnchors anchors) : IFolioPrintMarks
{
    private const double NoteMargin = 18;

    public FolioMarkIndex Index(TextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var headings = outline.Headings(document).ToDictionary(entry => entry.Paragraph.Id, entry => (entry.Level, entry.Text.Trim()), StringComparer.Ordinal);
        var targets = bookmarks.Bookmarks(document)
            .GroupBy(bookmark => bookmark.ParagraphId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<(string, int)>)[.. group.Select(bookmark => (bookmark.Name, bookmark.Offset))], StringComparer.Ordinal);
        var notes = anchors.All(document)
            .GroupBy(anchor => anchor.Start.ParagraphId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<(int, PrintNote)>)[.. group.Select(anchor => (anchor.Start.Offset, Note(document, anchor.Comment)))], StringComparer.Ordinal);
        return new FolioMarkIndex(headings, targets, notes);
    }

    public void Mark(IPageCanvas canvas, PageBox page, FolioMarkIndex index)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(page);
        page.Accept(new MarkVisitor(canvas, index));
    }

    private PrintNote Note(TextDocument document, PulseOffice.Documents.Annotations.Comment comment)
    {
        var lines = document.Comments.Thread(comment.Id).Select(entry =>
        {
            var body = string.Join(" ", entry.Content.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text));
            return entry.Id == comment.Id ? body : entry.Author + ": " + body;
        });
        return new PrintNote(comment.Author, string.Join("\n", lines), comment.Date == default ? null : comment.Date);
    }

    private sealed class MarkVisitor(IPageCanvas canvas, FolioMarkIndex index) : ILayoutVisitor
    {
        private readonly ObjectTurns _turns = new();

        public void VisitPage(PageBox page)
        {
        }

        public void VisitTurn(TurnedBox turn) => _turns.Push(turn.Bounds, turn.RotationDegrees);

        public void LeaveTurn(TurnedBox turn) => _turns.Pop();

        public void VisitLine(LineBox line)
        {
            if (line.Story is not (StoryKind.Body or StoryKind.Cell)) return;
            var paragraph = line.ParagraphId;
            if (line.Line.IsFirst && index.Headings.TryGetValue(paragraph, out var heading))
            {
                canvas.MarkHeading(heading.Level, heading.Title, Top(line));
                canvas.MarkTarget("folio-heading-" + paragraph, Top(line));
            }
            if (index.Targets.TryGetValue(paragraph, out var targets))
                foreach (var (name, offset) in targets)
                    if (Contains(line, offset)) canvas.MarkTarget(name, Top(line));
            if (index.Notes.TryGetValue(paragraph, out var notes))
                foreach (var (offset, note) in notes)
                    if (Contains(line, offset))
                        canvas.MarkNote(Rect(_turns.Bounds(new LayoutRect(Math.Max(0, line.Left - NoteMargin), line.Top, NoteMargin, NoteMargin))), note);
        }

        public void VisitRun(LayoutGlyphRun run, LineBox line)
        {
            if (run.Style?.Hyperlink is not { Length: > 0 } target || run.Width <= 0) return;
            canvas.MarkLink(Rect(_turns.Bounds(new LayoutRect(line.Left + run.X, line.Top, run.Width, line.Line.Height))), target);
        }

        private double Top(LineBox line) => _turns.Bounds(line.Bounds).Y;

        private DocumentRect Rect(LayoutRect rect) => Rect(rect.X, rect.Y, rect.Width, rect.Height);

        private bool Contains(LineBox line, int offset) =>
            offset >= line.Line.Start && (offset < line.Line.End || (line.Line.IsLast && offset == line.Line.End));

        private DocumentRect Rect(double x, double y, double width, double height) =>
            new(Twip.FromPoints(x), Twip.FromPoints(y), Twip.FromPoints(width), Twip.FromPoints(height));
    }
}

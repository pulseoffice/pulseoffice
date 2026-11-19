using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Commenting;
using PulseOffice.Documents.Controls;
using PulseOffice.Documents.Editing;
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
    public IReadOnlyDictionary<string, IReadOnlyList<FolioFormSpan>> Fields { get; init; } = new Dictionary<string, IReadOnlyList<FolioFormSpan>>();

    public static readonly FolioMarkIndex Empty = new(
        new Dictionary<string, (int, string)>(), new Dictionary<string, IReadOnlyList<(string, int)>>(), new Dictionary<string, IReadOnlyList<(int, PrintNote)>>());
}

public sealed record FolioFormSpan(string Id, int Start, int End, PrintFormField Field, bool ShowsPlaceholder);

public interface IFolioPrintMarks
{
    FolioMarkIndex Index(TextDocument document);

    void Mark(IPageCanvas canvas, PageBox page, FolioMarkIndex index);
}

public sealed class FolioPrintMarks(IDocumentOutline outline, IBookmarkCatalog bookmarks, ICommentAnchors anchors, IContentControlCatalog controls, IStoryCatalog stories) : IFolioPrintMarks
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
        return new FolioMarkIndex(headings, targets, notes) { Fields = Fields(document) };
    }

    public void Mark(IPageCanvas canvas, PageBox page, FolioMarkIndex index)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(page);
        var visitor = new MarkVisitor(canvas, index);
        page.Accept(visitor);
        visitor.MarkFields();
    }

    private Dictionary<string, IReadOnlyList<FolioFormSpan>> Fields(TextDocument document)
    {
        var all = controls.All(document);
        var exported = all.Where(span => Kind(span.Control.Kind) is not null && !Encloses(span, all)).ToList();
        var fields = new Dictionary<string, List<FolioFormSpan>>(StringComparer.Ordinal);
        if (exported.Count == 0) return new Dictionary<string, IReadOnlyList<FolioFormSpan>>(StringComparer.Ordinal);
        var paragraphs = stories.Paragraphs(document).Select(paragraph => paragraph.Id).ToList();
        foreach (var span in exported)
        {
            var field = Field(span);
            for (var order = span.StartOrder; order <= span.EndOrder && order - 1 < paragraphs.Count; order++)
            {
                if (order < 1) continue;
                var id = order == span.StartOrder ? span.Start.ParagraphId : order == span.EndOrder ? span.End.ParagraphId : paragraphs[order - 1];
                var start = order == span.StartOrder ? span.Start.Offset : 0;
                var end = order == span.EndOrder ? span.End.Offset : int.MaxValue;
                if (!fields.TryGetValue(id, out var list)) fields[id] = list = [];
                list.Add(field with { Start = start, End = end });
            }
        }
        return fields.ToDictionary(entry => entry.Key, entry => (IReadOnlyList<FolioFormSpan>)entry.Value, StringComparer.Ordinal);
    }

    private static bool Encloses(ContentControlSpan span, IReadOnlyList<ContentControlSpan> all) =>
        span.Control.Kind == ContentControlKind.RichText && all.Any(other => !ReferenceEquals(other, span) && Kind(other.Control.Kind) is not null &&
            (span.StartOrder, span.Start.Offset).CompareTo((other.StartOrder, other.Start.Offset)) <= 0 &&
            (other.EndOrder, other.End.Offset).CompareTo((span.EndOrder, span.End.Offset)) <= 0);

    private static PrintFormFieldKind? Kind(ContentControlKind kind) => kind switch
    {
        ContentControlKind.PlainText or ContentControlKind.Date or ContentControlKind.RichText => PrintFormFieldKind.Text,
        ContentControlKind.Picture => PrintFormFieldKind.Picture,
        ContentControlKind.CheckBox => PrintFormFieldKind.CheckBox,
        ContentControlKind.DropDownList => PrintFormFieldKind.ListBox,
        ContentControlKind.ComboBox => PrintFormFieldKind.ComboBox,
        _ => null,
    };

    private static FolioFormSpan Field(ContentControlSpan span)
    {
        var control = span.Control;
        var field = new PrintFormField(control.Title ?? control.Tag ?? "field-" + span.Id, Kind(control.Kind)!.Value)
        {
            Checked = control.Checked,
            MultiLine = control.MultiLine || control.Kind == ContentControlKind.RichText || !span.InOneParagraph,
            ReadOnly = control.ContentLocked,
            Tooltip = control.Title,
            Options = [.. control.Items.Select(item => item.DisplayText)],
        };
        return new FolioFormSpan(span.Id, span.Start.Offset, span.End.Offset, field, control.ShowingPlaceholder);
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
        private readonly Dictionary<string, (FolioFormSpan Span, LayoutRect Area, System.Text.StringBuilder Text, string Paragraph)> _fields = new(StringComparer.Ordinal);
        private readonly List<string> _fieldOrder = [];

        public void MarkFields()
        {
            foreach (var id in _fieldOrder)
            {
                var (span, area, text, _) = _fields[id];
                var value = span.Field.Kind is PrintFormFieldKind.CheckBox or PrintFormFieldKind.Picture || span.ShowsPlaceholder ? "" : text.ToString();
                canvas.MarkField(Rect(area), span.Field with { Value = value });
            }
        }

        private void Collect(LayoutGlyphRun run, LineBox line)
        {
            if (run.SourceLength == 0 || run.Width <= 0 || !index.Fields.TryGetValue(line.ParagraphId, out var spans)) return;
            foreach (var span in spans)
            {
                var start = Math.Max(span.Start, run.SourceOffset);
                var end = Math.Min(span.End, run.SourceOffset + run.SourceLength);
                if (start >= end) continue;
                var area = _turns.Bounds(new LayoutRect(line.Left + run.X, line.Top, run.Width, line.Line.Height));
                var shown = run.ObjectId is null ? run.Text.AsSpan(Math.Min(run.Text.Length, start - run.SourceOffset), Math.Max(0, Math.Min(end, run.SourceOffset + run.Text.Length) - start)) : ReadOnlySpan<char>.Empty;
                if (_fields.TryGetValue(span.Id, out var known))
                {
                    if (known.Paragraph != line.ParagraphId) known.Text.Append('\n');
                    _fields[span.Id] = (span, Union(known.Area, area), known.Text.Append(shown), line.ParagraphId);
                }
                else
                {
                    _fieldOrder.Add(span.Id);
                    _fields[span.Id] = (span, area, new System.Text.StringBuilder().Append(shown), line.ParagraphId);
                }
            }
        }

        private static LayoutRect Union(LayoutRect first, LayoutRect second)
        {
            var left = Math.Min(first.X, second.X);
            var top = Math.Min(first.Y, second.Y);
            return new LayoutRect(left, top, Math.Max(first.X + first.Width, second.X + second.Width) - left, Math.Max(first.Y + first.Height, second.Y + second.Height) - top);
        }

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
            Collect(run, line);
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

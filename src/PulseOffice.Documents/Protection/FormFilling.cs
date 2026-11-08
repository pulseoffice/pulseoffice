using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Controls;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Protection;

public sealed class FormOutline(IReadOnlyList<object> entries)
{
    public bool Matches(FormOutline other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return entries.SequenceEqual(other.Entries);
    }

    private IReadOnlyList<object> Entries => entries;
}

public interface IFormFilling
{
    bool Restricts(TextDocument document);

    bool Fills(TextDocument document, DocumentRange range);

    bool Chooses(TextDocument document, ContentControlSpan span);

    FormOutline Outline(TextDocument document);
}

public sealed class FormFilling(IContentControlCatalog catalog, IContentControlGuard controls, IEditGuard guard, IStoryCatalog stories) : IFormFilling
{
    private const string Interior = "\u0001";

    private static readonly IReadOnlyList<TextRun> NoRuns = [];

    public bool Restricts(TextDocument document) => document.Protection.Restriction == DocumentRestriction.Forms;

    public bool Fills(TextDocument document, DocumentRange range)
    {
        if (!Restricts(document)) return false;
        var start = (catalog.Order(document, range.Start.ParagraphId), range.Start.Offset);
        var end = (catalog.Order(document, range.End.ParagraphId), range.End.Offset);
        if (start.Item1 < 0 || end.Item1 < 0) return false;
        var span = catalog.All(document)
            .Where(candidate => (candidate.StartOrder, candidate.Start.Offset).CompareTo(start) <= 0 && end.CompareTo((candidate.EndOrder, candidate.End.Offset)) <= 0)
            .OrderByDescending(candidate => (candidate.StartOrder, candidate.Start.Offset))
            .FirstOrDefault();
        return span is not null && controls.AcceptsTyping(span.Control) && Open(document, span);
    }

    public bool Chooses(TextDocument document, ContentControlSpan span)
    {
        ArgumentNullException.ThrowIfNull(span);
        return Restricts(document) && !span.Control.ContentLocked && Open(document, span);
    }

    private bool Open(TextDocument document, ContentControlSpan span) =>
        guard.CheckContent(document, span.Start.ParagraphId) == EditVerdict.Allowed
        && guard.CheckContent(document, span.End.ParagraphId) == EditVerdict.Allowed;

    public FormOutline Outline(TextDocument document)
    {
        var spans = catalog.All(document).Where(span => !span.Control.ContentLocked).ToList();
        var entries = new List<object>();
        var order = 0;
        foreach (var paragraph in stories.Paragraphs(document))
        {
            order++;
            if (spans.Any(span => span.StartOrder < order && order < span.EndOrder)) continue;
            var interiors = Interiors(spans, order, paragraph.Length);
            entries.Add(paragraph.Id);
            entries.Add(paragraph.Content with { Runs = NoRuns, Text = string.Empty });
            entries.AddRange(Pieces(paragraph, interiors));
            entries.AddRange(Marks(paragraph, interiors));
        }
        return new FormOutline(entries);
    }

    private List<(int From, int To)> Interiors(IReadOnlyList<ContentControlSpan> spans, int order, int length)
    {
        var found = spans.Where(span => span.StartOrder <= order && order <= span.EndOrder)
            .Select(span => (From: span.StartOrder == order ? span.Start.Offset : 0, To: span.EndOrder == order ? span.End.Offset : length))
            .OrderBy(interior => interior.From)
            .ToList();
        var merged = new List<(int From, int To)>();
        foreach (var interior in found)
        {
            if (merged.Count > 0 && interior.From <= merged[^1].To) merged[^1] = (merged[^1].From, Math.Max(merged[^1].To, interior.To));
            else merged.Add(interior);
        }
        return merged;
    }

    private List<object> Pieces(ParagraphBlock paragraph, IReadOnlyList<(int From, int To)> interiors)
    {
        var formats = paragraph.Content.EffectiveRuns().SelectMany(run => Enumerable.Repeat(run with { Text = string.Empty }, run.Text.Length)).ToList();
        var text = paragraph.Text;
        var pieces = new List<object>();
        var cursor = 0;
        foreach (var (from, to) in interiors)
        {
            Outside(pieces, text, formats, cursor, from);
            pieces.Add(Interior);
            cursor = to;
        }
        Outside(pieces, text, formats, cursor, text.Length);
        return pieces;
    }

    private void Outside(List<object> pieces, string text, IReadOnlyList<TextRun> formats, int from, int to)
    {
        for (var offset = from; offset < to && offset < formats.Count; offset++)
            Append(pieces, formats[offset] with { Text = text[offset].ToString() });
    }

    private void Append(List<object> pieces, TextRun piece)
    {
        if (pieces.Count > 0 && pieces[^1] is TextRun previous && previous with { Text = string.Empty } == piece with { Text = string.Empty })
            pieces[^1] = previous with { Text = previous.Text + piece.Text };
        else pieces.Add(piece);
    }

    private IEnumerable<object> Marks(ParagraphBlock paragraph, IReadOnlyList<(int From, int To)> interiors)
    {
        foreach (var mark in paragraph.Marks)
        {
            if (interiors.Any(interior => interior.From < mark.Offset && mark.Offset < interior.To)) continue;
            var projected = mark.Offset - interiors.Where(interior => interior.To <= mark.Offset).Sum(interior => interior.To - interior.From);
            yield return mark is ContentControlMark { Control: { } control } owned
                ? owned with { Offset = projected, Control = control with { ShowingPlaceholder = false, Checked = false, Date = null } }
                : mark.At(projected);
        }
    }
}

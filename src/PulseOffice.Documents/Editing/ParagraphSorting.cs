using System.Globalization;
using PulseOffice.Document.Commands;
using PulseOffice.Documents.Blocks;

namespace PulseOffice.Documents.Editing;

public sealed record ParagraphSortKey(int Column, char? Separator, bool Numeric, bool Descending, bool MatchCase);

public interface IParagraphSorting
{
    bool CanSort(TextDocument document, DocumentRange range);

    DocumentEdit Sort(TextDocument document, UndoStack history, DocumentRange range, ParagraphSortKey key, CultureInfo culture);
}

public sealed class ParagraphSorting(IStoryCatalog stories) : IParagraphSorting
{
    public bool CanSort(TextDocument document, DocumentRange range)
    {
        var first = stories.Find(document, range.Start.ParagraphId);
        var last = stories.Find(document, range.End.ParagraphId);
        return ReferenceEquals(first.Location.Container, last.Location.Container) && last.Location.Index > first.Location.Index;
    }

    public DocumentEdit Sort(TextDocument document, UndoStack history, DocumentRange range, ParagraphSortKey key, CultureInfo culture)
    {
        var recorder = new EditRecorder(document, "sort-paragraphs");
        if (!CanSort(document, range)) return recorder.Commit(history);
        var first = stories.Find(document, range.Start.ParagraphId).Location;
        var last = stories.Find(document, range.End.ParagraphId).Location;
        var container = first.Container;
        var blocks = container.Blocks.Skip(first.Index).Take(last.Index - first.Index + 1).ToList();
        if (blocks.Any(block => block is not ParagraphBlock)) return recorder.Commit(history);
        var comparer = StringComparer.Create(culture, !key.MatchCase);
        var ordered = blocks.Cast<ParagraphBlock>().Select((paragraph, index) => (paragraph, index, text: Field(paragraph.Text, key))).ToList();
        ordered.Sort((a, b) =>
        {
            var compared = key.Numeric ? Number(a.text, culture).CompareTo(Number(b.text, culture)) : comparer.Compare(a.text, b.text);
            if (key.Descending) compared = -compared;
            return compared != 0 ? compared : a.index.CompareTo(b.index);
        });
        recorder.Touch(container);
        container.RemoveRange(first.Index, blocks.Count);
        container.InsertRange(first.Index, ordered.Select(entry => (Block)entry.paragraph));
        return recorder.Commit(history);
    }

    private string Field(string text, ParagraphSortKey key)
    {
        if (key.Separator is not { } separator || key.Column <= 1) return text.Trim();
        var parts = text.Split(separator);
        return key.Column <= parts.Length ? parts[key.Column - 1].Trim() : string.Empty;
    }

    private double Number(string text, CultureInfo culture) =>
        double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, culture, out var value) ? value : double.MaxValue;
}

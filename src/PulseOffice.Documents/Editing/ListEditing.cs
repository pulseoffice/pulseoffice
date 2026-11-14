using PulseOffice.Document.Commands;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Editing;

public interface IListEditing
{
    DocumentEdit ShiftLevel(TextDocument document, UndoStack history, DocumentRange range, int delta, bool withSubpoints);

    DocumentEdit Move(TextDocument document, UndoStack history, DocumentRange range, int direction, bool withSubpoints);

    DocumentEdit Restart(TextDocument document, UndoStack history, DocumentRange range, int? start);

    DocumentEdit SetNumbered(TextDocument document, UndoStack history, DocumentRange range, bool numbered);

    DocumentEdit Continue(TextDocument document, UndoStack history, DocumentRange range);

    bool CanContinue(TextDocument document, DocumentRange range);

    string? NextInLevel(TextDocument document, string paragraphId, int direction);

    bool CanMove(TextDocument document, DocumentRange range, int direction, bool withSubpoints);
}

public sealed class ListEditing(IStoryCatalog stories, IStyleResolver resolver) : IListEditing
{
    private const int MaxOutline = 10;

    private sealed record Span(BlockContainer Container, int First, int Last);

    public DocumentEdit ShiftLevel(TextDocument document, UndoStack history, DocumentRange range, int delta, bool withSubpoints)
    {
        var span = Selected(document, range, withSubpoints);
        var recorder = new EditRecorder(document, delta > 0 ? "list-demote" : "list-promote");
        foreach (var paragraph in Paragraphs(span))
        {
            var content = paragraph.Content;
            var effective = resolver.EffectiveParagraph(document.Styles, content);
            TextParagraph? changed = null;
            if (content.ListStyleName is not null)
            {
                var level = Math.Clamp(content.BulletLevel + delta, 0, ListStyle.LevelCount - 1);
                changed = Flags(document, content with { BulletLevel = level }, !content.ListHeader);
            }
            else if (effective.OutlineLevel is { } outline)
            {
                changed = Outline(document, content, Math.Clamp(outline + delta, 1, MaxOutline));
            }
            if (changed is null || changed == content) continue;
            recorder.Touch(paragraph);
            paragraph.Content = changed;
        }
        return recorder.Commit(history);
    }

    public bool CanMove(TextDocument document, DocumentRange range, int direction, bool withSubpoints)
    {
        var span = Selected(document, range, withSubpoints);
        return direction < 0 ? span.First > 0 : span.Last < span.Container.Count - 1;
    }

    public DocumentEdit Move(TextDocument document, UndoStack history, DocumentRange range, int direction, bool withSubpoints)
    {
        var span = Selected(document, range, withSubpoints);
        var recorder = new EditRecorder(document, direction < 0 ? "list-move-up" : "list-move-down").Touch(span.Container);
        var container = span.Container;
        if (direction < 0 && span.First > 0)
        {
            var start = withSubpoints && Level(container[span.First]) is { } level ? GroupStart(container, span.First - 1, level) : span.First - 1;
            var moved = container.Blocks.Skip(start).Take(span.First - start).ToList();
            container.RemoveRange(start, moved.Count);
            container.InsertRange(span.Last - moved.Count + 1, moved);
        }
        else if (direction > 0 && span.Last < container.Count - 1)
        {
            var end = withSubpoints ? Subpoints(container, span.Last + 1) : span.Last + 1;
            var moved = container.Blocks.Skip(span.Last + 1).Take(end - span.Last).ToList();
            container.RemoveRange(span.Last + 1, moved.Count);
            container.InsertRange(span.First, moved);
        }
        return recorder.Commit(history);
    }

    public DocumentEdit Restart(TextDocument document, UndoStack history, DocumentRange range, int? start)
    {
        var found = stories.Find(document, range.Start.ParagraphId).Paragraph;
        var recorder = new EditRecorder(document, "list-restart");
        if (found.Content.ListRestart != start && found.Content.ListStyleName is not null)
        {
            recorder.Touch(found);
            found.Content = found.Content with { ListRestart = start };
        }
        return recorder.Commit(history);
    }

    public DocumentEdit SetNumbered(TextDocument document, UndoStack history, DocumentRange range, bool numbered)
    {
        var recorder = new EditRecorder(document, numbered ? "list-numbered" : "list-unnumbered");
        foreach (var paragraph in Paragraphs(Selected(document, range, false)).Where(paragraph => paragraph.Content.ListStyleName is not null))
        {
            var changed = Flags(document, paragraph.Content, numbered);
            if (changed == paragraph.Content) continue;
            recorder.Touch(paragraph);
            paragraph.Content = changed;
        }
        return recorder.Commit(history);
    }

    public DocumentEdit Continue(TextDocument document, UndoStack history, DocumentRange range)
    {
        var recorder = new EditRecorder(document, "list-continue");
        foreach (var (paragraph, changed) in Continued(document, range))
        {
            recorder.Touch(paragraph);
            paragraph.Content = changed;
        }
        return recorder.Commit(history);
    }

    public bool CanContinue(TextDocument document, DocumentRange range) => Continued(document, range).Any();

    private IEnumerable<(ParagraphBlock Paragraph, TextParagraph Changed)> Continued(TextDocument document, DocumentRange range)
    {
        var span = Selected(document, range, false);
        var previous = span.Container.Blocks.Take(span.First).OfType<ParagraphBlock>().LastOrDefault(paragraph => paragraph.Content.ListStyleName is not null)?.Content;
        foreach (var paragraph in Paragraphs(span).ToList())
        {
            var content = paragraph.Content;
            var changed = content.ListStyleName is null && previous is not null
                ? Flags(document, content with { ListStyleName = previous.ListStyleName, BulletLevel = previous.BulletLevel, ListRestart = null, ListId = previous.ListId }, true)
                : content with { ListRestart = null, ListId = previous is not null && previous.ListStyleName == content.ListStyleName ? previous.ListId : content.ListId };
            if (changed != content) yield return (paragraph, changed);
        }
    }

    public string? NextInLevel(TextDocument document, string paragraphId, int direction)
    {
        var found = stories.Find(document, paragraphId);
        var container = found.Location.Container;
        var content = found.Paragraph.Content;
        if (content.ListStyleName is null) return null;
        for (var index = found.Location.Index + direction; index >= 0 && index < container.Count; index += direction)
        {
            if (container[index] is not ParagraphBlock { Content: var other } candidate || other.ListStyleName is null) return null;
            if (other.BulletLevel < content.BulletLevel) return null;
            if (other.BulletLevel == content.BulletLevel) return candidate.Id;
        }
        return null;
    }

    private TextParagraph Flags(TextDocument document, TextParagraph content, bool numbered)
    {
        var format = document.Styles.List.Find(content.ListStyleName)?.Level(content.BulletLevel).Format;
        return content with
        {
            Bullet = numbered && format == NumberFormat.Bullet,
            Numbered = numbered && format is not null and not NumberFormat.Bullet,
            ListHeader = !numbered,
        };
    }

    private TextParagraph Outline(TextDocument document, TextParagraph content, int level)
    {
        var heading = DefaultStyleNames.HeadingLevel(level);
        var isHeadingStyle = content.StyleName is { } name && Enumerable.Range(1, MaxOutline).Any(n => DefaultStyleNames.HeadingLevel(n) == name);
        return isHeadingStyle && document.Styles.Paragraph.Contains(heading)
            ? content with { StyleName = heading, OutlineLevel = null }
            : content with { OutlineLevel = level };
    }

    private Span Selected(TextDocument document, DocumentRange range, bool withSubpoints)
    {
        var start = stories.Find(document, range.Start.ParagraphId);
        var container = start.Location.Container;
        var last = start.Location.Index;
        if (range.End.ParagraphId != range.Start.ParagraphId && container.IndexOf(range.End.ParagraphId) is var end and >= 0)
            last = Math.Max(last, end);
        var first = Math.Min(start.Location.Index, last);
        if (withSubpoints) last = Subpoints(container, last);
        return new Span(container, first, last);
    }

    private int Subpoints(BlockContainer container, int index)
    {
        if (container[index] is not ParagraphBlock { Content.ListStyleName: not null } head) return index;
        var level = head.Content.BulletLevel;
        var last = index;
        while (last + 1 < container.Count && container[last + 1] is ParagraphBlock { Content: { ListStyleName: not null } next } && next.BulletLevel > level) last++;
        return last;
    }

    private int GroupStart(BlockContainer container, int index, int level)
    {
        var start = index;
        while (start > 0 && Level(container[start]) is { } current && current > level && Level(container[start - 1]) is not null) start--;
        return start;
    }

    private int? Level(Block block) => block is ParagraphBlock { Content: { ListStyleName: not null } content } ? content.BulletLevel : null;

    private IEnumerable<ParagraphBlock> Paragraphs(Span span) =>
        Enumerable.Range(span.First, span.Last - span.First + 1).Select(index => span.Container[index]).OfType<ParagraphBlock>();
}

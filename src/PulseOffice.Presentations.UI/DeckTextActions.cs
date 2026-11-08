using PulseOffice.Document.Commands;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

public sealed class DeckTextActions(Presentation deck, ITextCatalog texts, DeckActionRecorder recorder, ITextEditSplice _splice)
{
    public void EditText(Slide slide, string elementId, RichText updated)
    {
        if (slide.Find(elementId) is not TextElement current) return;

        var after = Fitted(slide, current with { Text = updated });

        Do(texts["deck.action.editText"],
            () => { slide.Replace(after); deck.Raise(); },
            () => { slide.Replace(current); deck.Raise(); });
    }

    public int RewriteTexts(string labelKey, Func<RichText, RichText> text, Func<string, string>? notes = null)
    {
        var elements = deck.Slides
            .SelectMany(slide => slide.Elements.OfType<TextElement>().Select(element => (Slide: slide, Before: element)))
            .Select(entry => (entry.Slide, entry.Before, After: Fitted(entry.Slide, entry.Before with { Text = text(entry.Before.Text) })))
            .Where(entry => entry.After.Text.PlainText != entry.Before.Text.PlainText)
            .ToList();
        var notesChanges = notes is null
            ? []
            : deck.Slides.Select(slide => (Slide: slide, Before: slide.Notes, After: notes(slide.Notes)))
                .Where(entry => entry.After != entry.Before)
                .ToList();

        var changed = elements.Count + notesChanges.Count;
        if (changed == 0 || IsReadOnly) return 0;

        Do(texts[labelKey],
            () =>
            {
                foreach (var entry in elements) entry.Slide.Replace(entry.After);
                foreach (var entry in notesChanges) entry.Slide.Notes = entry.After;
                deck.Raise();
            },
            () =>
            {
                foreach (var entry in elements) entry.Slide.Replace(entry.Before);
                foreach (var entry in notesChanges) entry.Slide.Notes = entry.Before;
                deck.Raise();
            });

        return changed;
    }

    public void AppendToText(Slide slide, string elementId, string text) => AppendRun(slide, elementId, new TextRun(text));

    public void AppendRun(Slide slide, string elementId, TextRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (slide.Find(elementId) is not TextElement te) return;

        var paragraphs = te.Text.Paragraphs.ToList();
        if (paragraphs.Count == 0)
        {
            paragraphs.Add(new TextParagraph(run.Text) { Runs = [run] });
        }
        else
        {
            var last = paragraphs[^1];
            paragraphs[^1] = last.WithRuns([.. last.EffectiveRuns(), run]);
        }

        var before = te;
        var after = Fitted(slide, te with { Text = te.Text with { Paragraphs = paragraphs } });

        Do(texts["deck.action.insertSymbol"],
            () => { slide.Replace(after); deck.Raise(); },
            () => { slide.Replace(before); deck.Raise(); });
    }

    public void AddComment(Slide slide, SlideComment comment)
    {
        Do(texts["deck.action.comment"],
            () => { slide.AddComment(comment); deck.Raise(); },
            () => { slide.RemoveComment(comment.Id); deck.Raise(); });
    }

    public void EditComment(Slide slide, SlideComment before, string text)
    {
        if (before.Text == text) return;

        var after = before with { Text = text };
        Do(texts["deck.action.editComment"],
            () => { slide.ReplaceComment(after); deck.Raise(); },
            () => { slide.ReplaceComment(before); deck.Raise(); });
    }

    public void RemoveComments(IReadOnlyList<(Slide Slide, SlideComment Comment)> comments)
    {
        if (comments.Count == 0) return;
        var placed = comments.Select(item => (item.Slide, item.Comment, Index: item.Slide.CommentIndex(item.Comment.Id)))
            .Where(item => item.Index >= 0)
            .OrderBy(item => item.Index)
            .ToList();

        Do(texts["deck.action.removeComments"],
            () =>
            {
                foreach (var (slide, comment, _) in placed) slide.RemoveComment(comment.Id);
                deck.Raise();
            },
            () =>
            {
                foreach (var (slide, comment, index) in placed) slide.InsertComment(index, comment);
                deck.Raise();
            });
    }

    public void SetNotes(Slide slide, string notes)
    {
        var before = slide.Notes;
        if (before == notes) return;

        Do(texts["deck.action.notes"],
            () => { slide.Notes = notes; deck.Raise(); },
            () => { slide.Notes = before; deck.Raise(); });
    }

    public void SetSlideText(Slide slide, PlaceholderRole role, string text) =>
        SetSlideTexts([(slide, role, text)]);

    public void SetSlideTexts(IReadOnlyList<(Slide Slide, PlaceholderRole Role, string Text)> changes)
    {
        var swaps = new List<(Slide Slide, TextElement Before, TextElement After)>();
        foreach (var (slide, role, text) in changes)
        {
            if (slide.Placeholder(role) is not { } placeholder) continue;
            var before = placeholder.Text;
            var after = _splice.Apply(before, text);
            if (ReferenceEquals(after, before)) continue;
            swaps.Add((slide, placeholder, Fitted(slide, placeholder with { Text = after })));
        }
        if (swaps.Count == 0) return;

        Do(texts["deck.action.editText"],
            () => { foreach (var swap in swaps) swap.Slide.Replace(swap.After); deck.Raise(); },
            () => { foreach (var swap in swaps) swap.Slide.Replace(swap.Before); deck.Raise(); });
    }

    private bool IsReadOnly => recorder.IsReadOnly;

    private void Do(string description, Action redo, Action undo) => recorder.Do(description, redo, undo);

    private T Fitted<T>(Slide slide, T element) where T : SlideElement => recorder.Fitted(slide, element);
}

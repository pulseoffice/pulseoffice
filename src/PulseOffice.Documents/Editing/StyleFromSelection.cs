using PulseOffice.Document.Commands;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Editing;

public interface IStyleFromSelection
{
    DocumentEdit NewParagraphStyle(TextDocument document, UndoStack history, string paragraphId, string name);

    DocumentEdit UpdateParagraphStyle(TextDocument document, UndoStack history, string paragraphId);

    DocumentEdit NewCharacterStyle(TextDocument document, UndoStack history, DocumentRange range, string name);

    DocumentEdit UpdateCharacterStyle(TextDocument document, UndoStack history, DocumentRange range);
}

public sealed class StyleFromSelection(IStoryCatalog stories, IParagraphText text, IDirectFormatting direct) : IStyleFromSelection
{
    public DocumentEdit NewParagraphStyle(TextDocument document, UndoStack history, string paragraphId, string name)
    {
        var paragraph = stories.Find(document, paragraphId).Paragraph;
        var content = paragraph.Content;
        var recorder = new EditRecorder(document, "style-new-paragraph");
        if (document.Styles.Paragraph.Contains(name)) return recorder.Commit(history);
        var current = document.Styles.Paragraph.Find(content.StyleName);
        var style = new ParagraphStyle(name)
        {
            ParentName = content.StyleName ?? DefaultStyleNames.Standard,
            NextStyleName = current?.NextStyleName == content.StyleName ? name : current?.NextStyleName,
            Paragraph = Movable(direct.Paragraph(content)),
            Character = CommonCharacter(content),
            Category = current?.Category ?? ParagraphStyleCategory.Text,
        };
        var styles = document.Styles.Paragraph;
        recorder.Touch(styles, () => styles.All, styles.ReplaceAll);
        styles.Add(style);
        recorder.Touch(paragraph);
        paragraph.Content = Cleared(content, style.Character) with { StyleName = name };
        return recorder.Commit(history);
    }

    public DocumentEdit UpdateParagraphStyle(TextDocument document, UndoStack history, string paragraphId)
    {
        var paragraph = stories.Find(document, paragraphId).Paragraph;
        var content = paragraph.Content;
        var recorder = new EditRecorder(document, "style-update-paragraph");
        var styles = document.Styles.Paragraph;
        if (styles.Find(content.StyleName ?? DefaultStyleNames.Standard) is not { } existing) return recorder.Commit(history);
        var character = CommonCharacter(content);
        var updated = (ParagraphStyle)existing.Clone();
        updated.Paragraph = Movable(direct.Paragraph(content)).Over(existing.Paragraph);
        updated.Character = character.Over(existing.Character);
        recorder.Touch(styles, () => styles.All, styles.ReplaceAll);
        styles.Replace(updated);
        recorder.Touch(paragraph);
        paragraph.Content = Cleared(content, character);
        return recorder.Commit(history);
    }

    public DocumentEdit NewCharacterStyle(TextDocument document, UndoStack history, DocumentRange range, string name)
    {
        var recorder = new EditRecorder(document, "style-new-character");
        if (document.Styles.Character.Contains(name) || First(document, range) is not { } run) return recorder.Commit(history);
        var properties = direct.Run(run);
        var styles = document.Styles.Character;
        recorder.Touch(styles, () => styles.All, styles.ReplaceAll);
        styles.Add(new CharacterStyle(name) { ParentName = run.StyleName, Character = properties });
        Map(document, recorder, range, item => Strip(item, properties) with { StyleName = name });
        return recorder.Commit(history);
    }

    public DocumentEdit UpdateCharacterStyle(TextDocument document, UndoStack history, DocumentRange range)
    {
        var recorder = new EditRecorder(document, "style-update-character");
        if (First(document, range) is not { StyleName: { } name } run || document.Styles.Character.Find(name) is not { } existing) return recorder.Commit(history);
        var properties = direct.Run(run);
        var updated = (CharacterStyle)existing.Clone();
        updated.Character = properties.Over(existing.Character);
        var styles = document.Styles.Character;
        recorder.Touch(styles, () => styles.All, styles.ReplaceAll);
        styles.Replace(updated);
        Map(document, recorder, range, item => item.StyleName == name ? Strip(item, properties) : item);
        return recorder.Commit(history);
    }

    private ParagraphProperties Movable(ParagraphProperties properties) =>
        properties with { PageBreakBefore = null, ColumnBreakBefore = null, PageStyleName = null, PageNumberRestart = null };

    private CharacterProperties CommonCharacter(TextParagraph content)
    {
        var runs = content.Runs.Where(run => run.Text.Length > 0 && run.Field == TextFieldKind.None).ToList();
        if (runs.Count == 0) return CharacterProperties.None;
        var first = direct.Run(runs[0]);
        return runs.Skip(1).Select(direct.Run).All(other => other == first) ? first : CharacterProperties.None;
    }

    private TextParagraph Cleared(TextParagraph content, CharacterProperties moved)
    {
        var runs = content.Runs.Select(run => Strip(run, moved)).ToList();
        return direct.Clear(content) with
        {
            PageBreakBefore = content.PageBreakBefore,
            ColumnBreakBefore = content.ColumnBreakBefore,
            Bullet = content.Bullet,
            Numbered = content.Numbered,
            ListHeader = content.ListHeader,
            ListRestart = content.ListRestart,
            Runs = runs,
            Text = content.Text,
        };
    }

    private TextRun Strip(TextRun run, CharacterProperties moved) =>
        moved != CharacterProperties.None && direct.Run(run) == moved
            ? direct.Clear(run) with { Field = run.Field, FieldArgument = run.FieldArgument, Hyperlink = run.Hyperlink, StyleName = run.StyleName }
            : run;

    private TextRun? First(TextDocument document, DocumentRange range)
    {
        var paragraph = stories.Find(document, range.Start.ParagraphId).Paragraph;
        TextRun? found = null;
        var end = range.Start.ParagraphId == range.End.ParagraphId ? range.End.Offset : paragraph.Length;
        var start = range.Start.Offset;
        if (end <= start)
        {
            start = Math.Max(0, start - 1);
            end = Math.Min(paragraph.Length, start + 1);
        }
        text.Map(paragraph.State, start, end, run =>
        {
            found ??= run;
            return run;
        });
        return found;
    }

    private void Map(TextDocument document, EditRecorder recorder, DocumentRange range, Func<TextRun, TextRun> change)
    {
        var first = stories.Find(document, range.Start.ParagraphId);
        var container = first.Location.Container;
        var last = range.End.ParagraphId == range.Start.ParagraphId ? first.Location.Index : container.IndexOf(range.End.ParagraphId);
        if (last < first.Location.Index) last = first.Location.Index;
        for (var index = first.Location.Index; index <= last; index++)
        {
            if (container[index] is not ParagraphBlock paragraph) continue;
            var start = paragraph.Id == range.Start.ParagraphId ? range.Start.Offset : 0;
            var end = paragraph.Id == range.End.ParagraphId ? range.End.Offset : paragraph.Length;
            if (end <= start) continue;
            recorder.Touch(paragraph);
            paragraph.State = text.Map(paragraph.State, start, end, change);
        }
    }
}

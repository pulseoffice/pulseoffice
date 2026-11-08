using PulseOffice.Document.Commands;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Editing;

public sealed record FormatSample(CharacterProperties Character, string? CharacterStyle, ParagraphProperties Paragraph, string? ParagraphStyle);

public interface IFormatBrush
{
    FormatSample Capture(TextDocument document, DocumentPosition at);

    DocumentEdit Paint(TextDocument document, UndoStack history, DocumentRange range, FormatSample sample, bool paragraphs);
}

public sealed class FormatBrush(IStoryCatalog stories, IParagraphText text, IDirectFormatting direct) : IFormatBrush
{
    public FormatSample Capture(TextDocument document, DocumentPosition at)
    {
        var paragraph = stories.Find(document, at.ParagraphId).Paragraph;
        var content = paragraph.Content;
        var run = RunAt(content, at.Offset);
        return new FormatSample(
            direct.Run(run),
            run.StyleName,
            direct.Paragraph(content) with { PageBreakBefore = null, ColumnBreakBefore = null, PageStyleName = null, PageNumberRestart = null },
            content.StyleName);
    }

    public DocumentEdit Paint(TextDocument document, UndoStack history, DocumentRange range, FormatSample sample, bool paragraphs)
    {
        var recorder = new EditRecorder(document, "clone-formatting");
        var first = stories.Find(document, range.Start.ParagraphId);
        var container = first.Location.Container;
        var last = range.End.ParagraphId == range.Start.ParagraphId ? first.Location.Index : container.IndexOf(range.End.ParagraphId);
        if (last < first.Location.Index) last = first.Location.Index;
        for (var index = first.Location.Index; index <= last; index++)
        {
            if (container[index] is not ParagraphBlock paragraph) continue;
            var start = paragraph.Id == range.Start.ParagraphId ? range.Start.Offset : 0;
            var end = paragraph.Id == range.End.ParagraphId ? range.End.Offset : paragraph.Length;
            var whole = start == 0 && end == paragraph.Length;
            recorder.Touch(paragraph);
            var state = paragraph.State;
            if (end > start)
                state = text.Map(state, start, end, item => direct.Set(direct.Clear(item) with
                {
                    Text = item.Text,
                    Field = item.Field,
                    FieldArgument = item.FieldArgument,
                    Hyperlink = item.Hyperlink,
                    StyleName = sample.CharacterStyle,
                }, sample.Character));
            if (whole || paragraphs)
            {
                var cleared = direct.Clear(state.Content) with
                {
                    Runs = state.Content.Runs,
                    Text = state.Content.Text,
                    PageBreakBefore = state.Content.PageBreakBefore,
                    ColumnBreakBefore = state.Content.ColumnBreakBefore,
                    StyleName = sample.ParagraphStyle ?? state.Content.StyleName,
                };
                state = state with { Content = direct.Set(cleared, sample.Paragraph) };
            }
            paragraph.State = state;
        }
        return recorder.Commit(history);
    }

    private TextRun RunAt(TextParagraph content, int offset)
    {
        var runs = content.Runs.Count == 0 ? [new TextRun(content.Text)] : content.Runs;
        var at = 0;
        foreach (var run in runs)
        {
            at += run.Text.Length;
            if (offset < at) return run;
        }
        return runs[^1];
    }
}

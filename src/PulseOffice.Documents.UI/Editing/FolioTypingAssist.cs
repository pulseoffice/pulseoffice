using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using PulseOffice.Text.Spelling;

namespace PulseOffice.Documents.UI.Editing;

public interface IFolioTypingAssist
{
    void Learn(TextDocument document);

    void AfterTyping(FolioEditor editor, string text);

    bool AcceptCompletion(FolioEditor editor);

    bool WithdrawCompletion(FolioEditor editor);

    bool EndEmptyListItem(FolioEditor editor);

    AutoFormatMatch? FormatBeforeSplit(FolioEditor editor);

    void SettleAfterSplit(FolioEditor editor, AutoFormatMatch? applied, string formattedParagraphId);
}

public sealed class FolioTypingAssist(
    IAutoCorrectSettings settings,
    IDocumentAutoFormat formatting,
    IWordCompletion completion,
    IWordBoundaryCorrection boundary) : IFolioTypingAssist
{
    private DocumentRange? _offered;

    public void Learn(TextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!settings.Enabled || !settings.Options.WordCompletion) return;
        foreach (var block in document.Body.Blocks.OfType<ParagraphBlock>()) completion.Collect(block.Text);
    }

    public void AfterTyping(FolioEditor editor, string text)
    {
        _offered = null;
        if (!settings.Enabled || !settings.Options.WordCompletion || text.Length != 1 || editor.HasSelection) return;
        var caret = editor.Caret;
        var paragraph = editor.TextOf(caret.ParagraphId) ?? string.Empty;
        if (boundary.IsBoundary(text[0]))
        {
            completion.Collect(paragraph[..Math.Min(caret.Offset, paragraph.Length)]);
            return;
        }
        if (!char.IsLetter(text[0]) || caret.Offset < paragraph.Length && char.IsLetterOrDigit(paragraph[caret.Offset])) return;
        var start = caret.Offset;
        while (start > 0 && char.IsLetter(paragraph[start - 1])) start--;
        var prefix = paragraph[start..caret.Offset];
        if (completion.Suggest(prefix) is not { } word) return;
        var rest = word[prefix.Length..];
        editor.Insert((writer, at) => writer.InsertText(editor.Document, editor.History, at, rest), "word-completion", TypingRun.Of(caret, rest));
        var end = caret with { Offset = caret.Offset + rest.Length };
        editor.Select(caret, end);
        _offered = new DocumentRange(caret, end);
    }

    public bool AcceptCompletion(FolioEditor editor)
    {
        if (_offered is not { } offered) return false;
        _offered = null;
        if (!editor.HasSelection || editor.Selection != offered) return false;
        editor.Select(offered.End, offered.End);
        return true;
    }

    public bool WithdrawCompletion(FolioEditor editor)
    {
        if (_offered is not { } offered) return false;
        _offered = null;
        return editor.HasSelection && editor.Selection == offered && editor.Backspace();
    }

    public bool EndEmptyListItem(FolioEditor editor)
    {
        if (editor.HasSelection || editor.ContentOf(editor.Caret.ParagraphId) is not { ListStyleName: not null, Text.Length: 0 }) return false;
        var id = editor.Caret.ParagraphId;
        return editor.Apply((writer, range) => writer.SetList(editor.Document, editor.History, range, null, 0), DocumentRange.In(id, 0, 0));
    }

    public AutoFormatMatch? FormatBeforeSplit(FolioEditor editor)
    {
        _offered = null;
        var options = settings.Options;
        if (!settings.Enabled || !options.AutomaticLists && !options.ApplyBorders || editor.HasSelection) return null;
        var caret = editor.Caret;
        var content = editor.ContentOf(caret.ParagraphId);
        if (content is null || content.ListStyleName is not null || caret.Offset != content.Text.Length) return null;
        var id = caret.ParagraphId;
        if (editor.Services.Stories.Find(editor.Document, id).Paragraph is not { } paragraph
            || formatting.Format(editor.Document, editor.History, paragraph, options.AutomaticLists, options.ApplyBorders) is not { } match) return null;
        var end = new DocumentPosition(id, content.Text.Length - match.PrefixLength);
        editor.Select(end, end);
        return match;
    }

    public void SettleAfterSplit(FolioEditor editor, AutoFormatMatch? applied, string formattedParagraphId)
    {
        if (applied is not { Kind: AutoFormatKind.Border }) return;
        var story = editor.Services.Order.StoryOf(editor.Document, formattedParagraphId);
        var index = story.ToList().FindIndex(paragraph => paragraph.Id == formattedParagraphId);
        if (index < 0 || index + 1 >= story.Count) return;
        var next = story[index + 1];
        editor.Services.Editor.SetParagraph(editor.Document, editor.History, DocumentRange.In(next.Id, 0, next.Length), new ParagraphProperties { Borders = new BoxBorders() });
    }
}

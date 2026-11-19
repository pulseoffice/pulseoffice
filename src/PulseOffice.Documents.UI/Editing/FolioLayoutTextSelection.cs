using PulseOffice.Documents.Layout.Queries;

namespace PulseOffice.Documents.UI.Editing;

public interface IFolioLayoutTextSelection
{
    IReadOnlyList<LayoutText> Of(FolioEditor editor);
}

public sealed class FolioLayoutTextSelection(ILayoutQueries queries, ILayoutReadOnlyText notices, IParagraphOrder order) : IFolioLayoutTextSelection
{
    public IReadOnlyList<LayoutText> Of(FolioEditor editor)
    {
        IReadOnlyList<LayoutText> chosen = editor.LayoutText is { } explicitText ? [explicitText] : [];
        if (!editor.HasSelection || editor.SelectedObjectId is not null) return chosen;
        var layout = editor.DisplayLayout;
        var range = editor.Selection;
        if (queries.Caret(layout, range.Start) is not { } start || queries.Caret(layout, range.End) is not { } end) return chosen;
        var last = AtStoryEnd(editor, range.End) ? layout.Pages.Count - 1 : end.Page - 1;
        var spanned = notices.OnPages(layout, start.Page, last);
        return [.. spanned, .. chosen.Where(text => !spanned.Any(other => Same(text, other)))];
    }

    private bool AtStoryEnd(FolioEditor editor, PulseOffice.Documents.Editing.DocumentPosition position)
    {
        var story = order.StoryOf(editor.Document, position.ParagraphId);
        return story.Count > 0 && story[^1].Id == position.ParagraphId && position.Offset >= story[^1].Length;
    }

    private bool Same(LayoutText left, LayoutText right) =>
        left.Page == right.Page && left.Lines.Count > 0 && right.Lines.Count > 0 && left.Lines[0] == right.Lines[0];
}

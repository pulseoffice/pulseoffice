using PulseOffice.Document.Commands;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Pages;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Documents.UI.Editing;

public sealed record PageGeometry(DocumentSize Size, PageOrientation Orientation, Margins Margins, SectionColumns Columns);

public interface IFolioPageStyles
{
    PageStyle Current(FolioEditor editor);

    string Anchor(FolioEditor editor);

    PageGeometry Read(PageStyle style);

    bool Change(FolioEditor editor, Func<PageGeometry, PageGeometry> change, string description);
}

public sealed class FolioPageStyles(IBlockTree tree) : IFolioPageStyles
{
    public PageStyle Current(FolioEditor editor)
    {
        var sheet = editor.Document.Styles;
        var name = editor.Document.InitialPageStyle;
        foreach (var paragraph in tree.Paragraphs(editor.Document.Body))
        {
            if (paragraph.Content.PageStyleName is { } started && sheet.Page.Contains(started)) name = started;
            if (paragraph.Id == editor.Caret.ParagraphId) break;
        }
        return sheet.Page.Find(name) ?? sheet.Page.All[0];
    }

    public string Anchor(FolioEditor editor)
    {
        var blocks = tree.Paragraphs(editor.Document.Body).ToList();
        var anchor = blocks.FirstOrDefault()?.Id ?? editor.Caret.ParagraphId;
        foreach (var paragraph in blocks)
        {
            if (paragraph.Content.PageStyleName is not null) anchor = paragraph.Id;
            if (paragraph.Id == editor.Caret.ParagraphId) break;
        }
        return anchor;
    }

    public PageGeometry Read(PageStyle style) => new(style.Size, style.Orientation, style.Margins, style.Columns);

    public bool Change(FolioEditor editor, Func<PageGeometry, PageGeometry> change, string description)
    {
        if (editor.RefusesDocumentEdit()) return false;
        var style = Current(editor);
        var before = Read(style);
        var after = change(before);
        if (after == before) return false;
        var action = new DelegateAction(description, () => Write(editor, style, after), () => Write(editor, style, before));
        Write(editor, style, after);
        editor.History.Push(action);
        return true;
    }

    private void Write(FolioEditor editor, PageStyle style, PageGeometry geometry)
    {
        style.Size = geometry.Size;
        style.Orientation = geometry.Orientation;
        style.Margins = geometry.Margins;
        style.Columns = geometry.Columns;
        editor.Document.Raise();
    }
}

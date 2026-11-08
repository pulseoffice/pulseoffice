using PulseOffice.Document.Commands;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;

namespace PulseOffice.Documents.Editing;

public sealed record TitlePageSettings(int Count, string TitleStyle, int? RestartNumbering);

public interface ITitlePages
{
    bool CanStart(TextDocument document);

    DocumentEdit Insert(TextDocument document, UndoStack history, TitlePageSettings settings);

    DocumentEdit Convert(TextDocument document, UndoStack history, string contentParagraphId, TitlePageSettings settings);
}

public sealed class TitlePages(IDocumentEditor editor) : ITitlePages
{
    public bool CanStart(TextDocument document) => document.Body.Blocks.Count > 0 && document.Body.Blocks[0] is ParagraphBlock;

    public DocumentEdit Insert(TextDocument document, UndoStack history, TitlePageSettings settings)
    {
        var first = First(document);
        var contentStyle = ContentStyle(document, first, settings);
        using var transaction = history.BeginTransaction("title-page-insert");
        var titles = new List<string> { first.Id };
        var content = first.Id;
        for (var page = 0; page < Math.Max(1, settings.Count); page++)
        {
            var edit = editor.SplitParagraph(document, history, new DocumentPosition(content, 0));
            content = ((ParagraphSplit)edit.Changes[0]).NewParagraphId;
            if (page < settings.Count - 1) titles.Add(content);
        }
        editor.SetPageStyle(document, history, first.Id, settings.TitleStyle);
        foreach (var title in titles.Skip(1))
        {
            editor.SetPageStyle(document, history, title, null);
            editor.SetParagraph(document, history, DocumentRange.In(title, 0, 0), new ParagraphProperties { PageBreakBefore = true });
        }
        return editor.SetPageStyle(document, history, content, contentStyle, settings.RestartNumbering);
    }

    public DocumentEdit Convert(TextDocument document, UndoStack history, string contentParagraphId, TitlePageSettings settings)
    {
        var first = First(document);
        var contentStyle = ContentStyle(document, first, settings);
        using var transaction = history.BeginTransaction("title-page-convert");
        var edit = editor.SetPageStyle(document, history, first.Id, settings.TitleStyle);
        if (contentParagraphId == first.Id) return edit;
        return editor.SetPageStyle(document, history, contentParagraphId, contentStyle, settings.RestartNumbering);
    }

    private ParagraphBlock First(TextDocument document) =>
        CanStart(document) ? (ParagraphBlock)document.Body.Blocks[0] : throw new InvalidOperationException("Title pages need a paragraph at the start of the document.");

    private string ContentStyle(TextDocument document, ParagraphBlock first, TitlePageSettings settings)
    {
        if (!document.Styles.Page.Contains(settings.TitleStyle)) throw new KeyNotFoundException($"Page style '{settings.TitleStyle}' not found.");
        var current = first.Content.PageStyleName ?? document.InitialPageStyle;
        if (current != settings.TitleStyle) return current;
        return document.Styles.Page.Find(settings.TitleStyle)?.NextStyleName is { } next && next != settings.TitleStyle ? next : DefaultStyleNames.Standard;
    }
}

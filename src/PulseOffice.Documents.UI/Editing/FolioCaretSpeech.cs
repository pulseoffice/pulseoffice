using PulseOffice.Documents.Objects;
using PulseOffice.Documents.UI.StyleCatalog;
using PulseOffice.Localization;

namespace PulseOffice.Documents.UI.Editing;

public sealed record FolioCaretAnnouncement(string Name, string Help);

public sealed record FolioCaretPlace(int Page, int Pages, int Line, int Column);

public interface IFolioCaretSpeech
{
    FolioCaretPlace? Place(FolioEditor editor);

    FolioCaretAnnouncement Describe(FolioEditor editor);
}

public sealed class FolioCaretSpeech(ITextCatalog texts, IFolioPageJump pages, IFolioStyleNames styles) : IFolioCaretSpeech
{
    public FolioCaretPlace? Place(FolioEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        var layout = editor.Layout.Pages;
        if (layout.Count == 0) return null;
        var caret = editor.Caret;
        var page = Math.Clamp(pages.PageOf(editor, caret), 1, layout.Count);
        var lines = layout[page - 1].Lines;
        var host = lines.Where(line => line.ParagraphId == caret.ParagraphId && line.Line.Start <= caret.Offset).LastOrDefault();
        if (host is null) return new FolioCaretPlace(page, layout.Count, 1, caret.Offset + 1);
        var row = lines.Select(line => Math.Round(line.Top, 1)).Distinct().Count(top => top < Math.Round(host.Top, 1)) + 1;
        return new FolioCaretPlace(page, layout.Count, row, caret.Offset - host.Line.Start + 1);
    }

    public FolioCaretAnnouncement Describe(FolioEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        var surface = texts["folio.surface.name"];
        if (editor.SelectedObjectId is { } id && editor.Document.Objects.Find(id) is { } item)
            return new FolioCaretAnnouncement(texts.Format("folio.surface.object", surface, Title(item)), string.Empty);

        var name = Place(editor) is { } place
            ? texts.Format("folio.surface.position", surface, place.Page, place.Pages, place.Line, place.Column)
            : surface;
        if (editor.Services.Tables.Editing.Locate(editor.Document, editor.Caret.ParagraphId) is { } cell)
            name += ", " + texts.Format("folio.surface.cell", cell.Row + 1, cell.Column + 1);
        return new FolioCaretAnnouncement(name, Format(editor));
    }

    private string Format(FolioEditor editor)
    {
        var run = editor.EffectiveRun();
        var paragraph = editor.EffectiveParagraph();
        var parts = new List<string>
        {
            texts.Format("folio.surface.format", styles.Title(paragraph.StyleName ?? editor.CurrentParagraph().StyleName ?? "Standard"), run.Font ?? string.Empty, run.FontSize ?? 12),
        };
        if (editor.IsOn(CharacterToggle.Bold)) parts.Add(texts["folio.surface.bold"]);
        if (editor.IsOn(CharacterToggle.Italic)) parts.Add(texts["folio.surface.italic"]);
        if (editor.IsOn(CharacterToggle.Underline)) parts.Add(texts["folio.surface.underline"]);
        return string.Join(", ", parts);
    }

    private string Title(AnchoredObject item) => item.Name is { Length: > 0 } named ? named : item.Content switch
    {
        TextBoxContent => texts["folio.surface.kind.frame"],
        EmbeddedContent { Value: PulseOffice.Drawing.Assets.RasterImageObject } => texts["folio.surface.kind.image"],
        EmbeddedContent { Value: PulseOffice.Charts.ChartDefinition } => texts["folio.surface.kind.chart"],
        _ => texts["folio.surface.kind.shape"],
    };
}

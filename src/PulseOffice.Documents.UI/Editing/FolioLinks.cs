using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Fields;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Links;

namespace PulseOffice.Documents.UI.Editing;

public interface IFolioLinks
{
    IReadOnlyList<LinkPlace> Places(TextDocument document);

    DocumentRange Range(FolioEditor editor);
}

public sealed class FolioLinks(IDocumentOutline outline, IBookmarkCatalog bookmarks, IBlockTree tree, ITextCatalog texts) : IFolioLinks
{
    public IReadOnlyList<LinkPlace> Places(TextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var groups = new List<LinkPlace>();
        if (Headings(document) is { Count: > 0 } headings) groups.Add(Group("link.place.headings", "link.place.heading", headings));
        var marks = bookmarks.Bookmarks(document).Where(bookmark => !bookmark.Name.StartsWith('_'))
            .Select(bookmark => new LinkPlace(bookmark.Name, "#" + bookmark.Name, "link.place.bookmark")).ToList();
        if (marks.Count > 0) groups.Add(Group("link.place.bookmarks", "link.place.bookmark", marks));
        var tables = tree.Blocks(document.Body).OfType<TableBlock>().Where(table => table.Name is { Length: > 0 })
            .Select(table => new LinkPlace(table.Name!, "#" + table.Name + "|table", "link.place.table")).ToList();
        if (tables.Count > 0) groups.Add(Group("link.place.tables", "link.place.table", tables));
        var sections = tree.Blocks(document.Body).OfType<SectionBlock>().Where(section => !section.IsIndex && section.Name.Length > 0)
            .Select(section => new LinkPlace(section.Name, "#" + section.Name + "|region", "link.place.section")).ToList();
        if (sections.Count > 0) groups.Add(Group("link.place.sections", "link.place.section", sections));
        return groups;
    }

    public DocumentRange Range(FolioEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        if (editor.HasSelection) return editor.Selection;
        var caret = editor.Caret;
        var paragraph = editor.Services.Stories.Find(editor.Document, caret.ParagraphId).Paragraph;
        var link = editor.EffectiveRun().Hyperlink;
        var start = 0;
        var from = -1;
        var to = -1;
        foreach (var run in paragraph.Content.Runs)
        {
            var end = start + run.Text.Length;
            if (run.Hyperlink == link && (from < 0 ? caret.Offset >= start && caret.Offset <= end : start == to))
            {
                if (from < 0) from = start;
                to = end;
            }
            else if (from >= 0 && start >= to) break;
            start = end;
        }
        return from < 0 ? DocumentRange.At(caret) : DocumentRange.In(caret.ParagraphId, from, to);
    }

    private LinkPlace Group(string titleKey, string icon, IReadOnlyList<LinkPlace> children) =>
        new(texts[titleKey], string.Empty, icon) { Selectable = false, Children = children };

    private List<LinkPlace> Headings(TextDocument document)
    {
        var roots = new List<LinkPlace>();
        var stack = new List<(int Level, List<LinkPlace> Children, LinkPlace Place)>();
        foreach (var heading in outline.Headings(document))
        {
            if (heading.Text.Trim().Length == 0) continue;
            while (stack.Count > 0 && stack[^1].Level >= heading.Level) Close(stack, roots);
            stack.Add((heading.Level, [], new LinkPlace(heading.Text.Trim(), "#" + heading.Text + "|outline", "link.place.heading")));
        }
        while (stack.Count > 0) Close(stack, roots);
        return roots;
    }

    private void Close(List<(int Level, List<LinkPlace> Children, LinkPlace Place)> stack, List<LinkPlace> roots)
    {
        var (_, children, place) = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        var closed = place with { Children = children };
        if (stack.Count > 0) stack[^1].Children.Add(closed);
        else roots.Add(closed);
    }
}

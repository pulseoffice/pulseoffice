using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Layout.Tree;

namespace PulseOffice.Documents.Layout.Flow;

public interface IFieldLayoutFactsFactory
{
    IFieldLayoutFacts Create(TextDocument document, DocumentLayout layout);
}

public sealed class FieldLayoutFactsFactory(IPageNumberText numbers) : IFieldLayoutFactsFactory
{
    public IFieldLayoutFacts Create(TextDocument document, DocumentLayout layout)
    {
        var pages = new Dictionary<string, string>(StringComparer.Ordinal);
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        var citations = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var page in layout.Pages)
        {
            var shown = numbers.Render(numbers.FormatOf(document.Styles, page.StyleName), page.Number);
            foreach (var line in page.AllLines.Concat(page.Tables.SelectMany(table => table.Lines)))
            {
                pages.TryAdd(line.ParagraphId, shown);
                foreach (var run in line.Line.Runs)
                {
                    if (run.Kind == GlyphRunKind.Label && line.Line.IsFirst) labels.TryAdd(line.ParagraphId, run.Text);
                    if (run.Kind == GlyphRunKind.Citation && run.NoteId is { } note) citations.TryAdd(note, run.Text);
                }
            }
        }
        return new LayoutFieldFacts(pages, labels, citations, layout.Pages.Count);
    }
}

public sealed class LayoutFieldFacts(
    IReadOnlyDictionary<string, string> pages,
    IReadOnlyDictionary<string, string> labels,
    IReadOnlyDictionary<string, string> citations,
    int pageCount) : IFieldLayoutFacts
{
    public string? PageOf(string paragraphId) => pages.GetValueOrDefault(paragraphId);

    public string? NoteCitation(string noteId) => citations.GetValueOrDefault(noteId);

    public string? ListLabel(string paragraphId) => labels.GetValueOrDefault(paragraphId);

    public int? PageCount => pageCount;
}

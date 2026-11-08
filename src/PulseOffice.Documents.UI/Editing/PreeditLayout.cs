using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.Layout.Tree;

namespace PulseOffice.Documents.UI.Editing;

public interface IPreeditLayout
{
    DocumentLayout Compose(IDocumentLayoutEngine engine, TextDocument document, DocumentLayout committed, DocumentPosition at, string preedit);
}

public sealed class PreeditLayout(IStoryCatalog stories, IParagraphText text) : IPreeditLayout
{
    public DocumentLayout Compose(IDocumentLayoutEngine engine, TextDocument document, DocumentLayout committed, DocumentPosition at, string preedit)
    {
        var paragraph = stories.Find(document, at.ParagraphId).Paragraph;
        var original = paragraph.State;
        var offset = Math.Clamp(at.Offset, 0, paragraph.Length);
        var inserted = text.Insert(original, offset, preedit);
        paragraph.State = text.Map(inserted, offset, offset + preedit.Length, run => run with { Underline = true });
        try
        {
            return engine.Relayout(document, committed);
        }
        finally
        {
            paragraph.State = original;
        }
    }
}

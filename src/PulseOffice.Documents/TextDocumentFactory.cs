using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;

namespace PulseOffice.Documents;

public interface ITextDocumentFactory
{
    TextDocument Create();
}

public sealed class TextDocumentFactory(IDefaultStyleSheetFactory styles) : ITextDocumentFactory
{
    public TextDocument Create()
    {
        var document = new TextDocument(styles.Create());
        document.Body.Add(new ParagraphBlock(document.NextId("p"), string.Empty, DefaultStyleNames.Standard));
        return document;
    }
}

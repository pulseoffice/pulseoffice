using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class BookmarkAcrossParagraphsTests
{
    [Fact]
    public void IlTestoDiUnSegnalibroSuPiuParagrafiComprendeQuelliInMezzo()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Replace(
        [
            new ParagraphBlock("a", "Prima riga") { Marks = [new BookmarkMark(6, "b", MarkEdge.Start)] },
            new ParagraphBlock("m", "In mezzo"),
            new ParagraphBlock("z", "Ultima riga") { Marks = [new BookmarkMark(6, "b", MarkEdge.End)] },
        ]);
        var catalog = new BookmarkCatalog(new StoryCatalog(new BlockTree()));

        Assert.Equal("riga In mezzo Ultima", catalog.TextOf(document, catalog.Find(document, "b")!));
    }
}

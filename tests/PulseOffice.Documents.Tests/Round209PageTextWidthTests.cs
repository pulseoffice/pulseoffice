using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round209PageTextWidthTests
{
    [Fact]
    public void The_page_text_width_of_a_section_ignores_its_columns_and_follows_its_page_style()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Styles.Page.Add(new PageStyle("Narrow") { Margins = new Margins(new Twip(3000), new Twip(1000), new Twip(3000), new Twip(1000)) });
        var lead = new ParagraphBlock(document.NextId("p"), new TextParagraph("uno") { PageStyleName = "Narrow" });
        document.Body.Add(new SectionBlock(document.NextId("s"), "Columns", new BlockContainer([lead])) { Columns = new SectionColumns(2, new Twip(400)) });
        var pages = new PageTextWidth();

        var narrow = document.Styles.Page.Find("Narrow")!;
        Assert.Equal(narrow.Width.Value - narrow.BoundMargins.Left.Value - narrow.Margins.Right.Value, pages.PageAt(document, lead.Id));
        Assert.Equal((pages.PageAt(document, lead.Id) - 400) / 2, pages.At(document, lead.Id));
    }
}

using System.Text;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Interchange.Tests.Text;

public sealed class Round215ListSuppressionExportTests
{
    private readonly TextInterchange _formats = new();

    private TextDocument Document(bool suppressed)
    {
        var document = _formats.Factory.Create();
        document.Styles.Paragraph.Add(new ParagraphStyle("Listed") { ParentName = DefaultStyleNames.Standard, ListStyleName = DefaultStyleNames.List1 });
        document.Body.Replace([new ParagraphBlock("p0", new TextParagraph("a") { Runs = [new TextRun("a")], StyleName = "Listed", ListSuppressed = suppressed })]);
        return document;
    }

    [Fact]
    public void Rtf_writes_no_list_for_a_paragraph_that_switches_off_its_style_list()
    {
        Assert.Contains(@"\ls", Encoding.UTF8.GetString(TextInterchange.Bytes(Document(false), _formats.RtfWriter)), StringComparison.Ordinal);
        Assert.DoesNotContain(@"\ilvl", Encoding.UTF8.GetString(TextInterchange.Bytes(Document(true), _formats.RtfWriter)), StringComparison.Ordinal);
    }

    [Fact]
    public void Html_writes_no_list_item_for_a_paragraph_that_switches_off_its_style_list()
    {
        Assert.Contains("<li", Encoding.UTF8.GetString(TextInterchange.Bytes(Document(false), _formats.HtmlWriter)), StringComparison.Ordinal);
        Assert.DoesNotContain("<li", Encoding.UTF8.GetString(TextInterchange.Bytes(Document(true), _formats.HtmlWriter)), StringComparison.Ordinal);
    }
}

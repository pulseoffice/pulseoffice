using System.Text;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Interchange.Tests.Text;

public sealed class Round196ListNumberExportTests
{
    private readonly TextInterchange _formats = new();

    private TextDocument Document(NumberFormat format)
    {
        var document = _formats.Factory.Create();
        document.Language = "it-IT";
        document.Styles.List.Add(new ListStyle("Round196", [new ListLevel(format)]));
        document.Body.Replace([new ParagraphBlock("p0", new TextParagraph("a") { Runs = [new TextRun("a")], ListStyleName = "Round196", Numbered = true })]);
        return document;
    }

    [Theory]
    [InlineData(NumberFormat.OrdinalText, 7)]
    [InlineData(NumberFormat.CardinalText, 6)]
    [InlineData(NumberFormat.DecimalZero, 22)]
    [InlineData(NumberFormat.Hebrew, 45)]
    [InlineData(NumberFormat.RussianUpper, 59)]
    public void Rtf_writes_the_number_format_code(NumberFormat format, int code)
    {
        var rtf = Encoding.UTF8.GetString(TextInterchange.Bytes(Document(format), _formats.RtfWriter));

        Assert.Contains(@"\levelnfc" + code + @"\", rtf, StringComparison.Ordinal);
    }

    [Fact]
    public void Html_writes_spelled_labels_in_the_document_language()
    {
        var html = Encoding.UTF8.GetString(TextInterchange.Bytes(Document(NumberFormat.OrdinalText), _formats.HtmlWriter));

        Assert.Contains("list-style-type:'Primo. '", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Html_writes_a_css_keyword_where_one_exists()
    {
        var html = Encoding.UTF8.GetString(TextInterchange.Bytes(Document(NumberFormat.DecimalZero), _formats.HtmlWriter));

        Assert.Contains("list-style-type:decimal-leading-zero", html, StringComparison.Ordinal);
    }
}

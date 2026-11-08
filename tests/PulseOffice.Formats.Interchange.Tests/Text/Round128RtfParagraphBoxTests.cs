using System.Text;
using PulseOffice.Documents.Blocks;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Interchange.Tests.Text;

public sealed class Round128RtfParagraphBoxTests
{
    private readonly TextInterchange _formats = new();

    private TextParagraph RoundTrip(TextParagraph paragraph)
    {
        var document = new PulseOffice.Documents.TextDocumentFactory(new PulseOffice.Documents.Styles.DefaultStyleSheetFactory()).Create();
        document.Body.Replace([new ParagraphBlock("p1", paragraph)]);
        var again = TextInterchange.Read(TextInterchange.Bytes(document, _formats.RtfWriter), _formats.RtfReader);
        return ((ParagraphBlock)again.Body[0]).Content;
    }

    [Fact]
    public void Inner_spacing_on_a_side_without_a_line_survives_an_rtf_round_trip()
    {
        var borders = new BoxBorders { Top = new BorderLine(1, 0xFF000000), PaddingTop = 2, PaddingLeft = 5 };

        Assert.Equal(borders, RoundTrip(new TextParagraph("x") { Runs = [new TextRun("x")], Borders = borders }).Borders);
    }

    [Fact]
    public void Paragraph_shading_is_read_from_cbpat()
    {
        var document = TextInterchange.Read(Encoding.ASCII.GetBytes(@"{\rtf1\ansi{\colortbl;\red255\green255\blue0;}\pard\cbpat1 Shaded\par\pard Plain\par}"), _formats.RtfReader);

        Assert.Equal(0xFFFFFF00u, ((ParagraphBlock)document.Body[0]).Content.Background);
        Assert.Null(((ParagraphBlock)document.Body[1]).Content.Background);
    }

    [Fact]
    public void Paragraph_shading_survives_an_rtf_round_trip()
    {
        Assert.Equal(0xFF2563EBu, RoundTrip(new TextParagraph("x") { Runs = [new TextRun("x")], Background = 0xFF2563EB }).Background);
    }

    [Fact]
    public void Cell_shading_is_not_taken_as_paragraph_shading()
    {
        var document = TextInterchange.Read(Encoding.ASCII.GetBytes(
            @"{\rtf1\ansi{\colortbl;\red255\green0\blue0;}\trowd\clcbpat1\cellx2000\pard\intbl A\cell\row\pard After\par}"), _formats.RtfReader);

        Assert.All(document.Body.Blocks.OfType<ParagraphBlock>(), block => Assert.Null(block.Content.Background));
    }
}

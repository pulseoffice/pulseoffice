using System.Text;
using PulseOffice.Documents.Blocks;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Interchange.Tests.Text;

public sealed class Round127RtfParagraphBorderTests
{
    private readonly TextInterchange _formats = new();

    private TextParagraph First(string rtf) =>
        ((ParagraphBlock)TextInterchange.Read(Encoding.ASCII.GetBytes(rtf), _formats.RtfReader).Body[0]).Content;

    [Fact]
    public void Paragraph_borders_are_read_with_style_width_colour_and_spacing()
    {
        var paragraph = First(@"{\rtf1\ansi{\colortbl;\red255\green0\blue0;}\pard\brdrt\brdrdash\brdrw30\brdrcf1\brsp40\brdrb\brdrs\brdrw10 Boxed\par}");

        var borders = Assert.IsType<BoxBorders>(paragraph.Borders);
        Assert.Equal(new BorderLine(1.5, 0xFFFF0000) { Style = BorderLineStyle.Dashed }, borders.Top);
        Assert.Equal(2, borders.PaddingTop, 3);
        Assert.Equal(new BorderLine(0.5, 0xFF000000), borders.Bottom);
        Assert.Null(borders.Left);
        Assert.Null(borders.Right);
    }

    [Fact]
    public void A_box_border_draws_all_four_sides_and_pard_clears_it()
    {
        var document = TextInterchange.Read(Encoding.ASCII.GetBytes(@"{\rtf1\ansi\pard\box\brdrdb\brdrw15 One\par\pard Two\par}"), _formats.RtfReader);

        var boxed = ((ParagraphBlock)document.Body[0]).Content.Borders!;
        Assert.All(new[] { boxed.Top, boxed.Left, boxed.Bottom, boxed.Right }, line => Assert.Equal(new BorderLine(0.75, 0xFF000000) { Style = BorderLineStyle.Double }, line));
        Assert.Null(((ParagraphBlock)document.Body[1]).Content.Borders);
    }

    [Fact]
    public void Paragraph_borders_survive_an_rtf_round_trip()
    {
        var document = new PulseOffice.Documents.TextDocumentFactory(new PulseOffice.Documents.Styles.DefaultStyleSheetFactory()).Create();
        var borders = new BoxBorders
        {
            Top = new BorderLine(1, 0xFF2563EB) { Style = BorderLineStyle.Dotted },
            Left = new BorderLine(0.5, 0xFF000000),
            PaddingTop = 3,
            PaddingLeft = 4,
        };
        document.Body.Replace([new ParagraphBlock("p1", new TextParagraph("x") { Runs = [new TextRun("x")], Borders = borders })]);

        var again = TextInterchange.Read(TextInterchange.Bytes(document, _formats.RtfWriter), _formats.RtfReader);

        Assert.Equal(borders, ((ParagraphBlock)again.Body[0]).Content.Borders);
    }

    [Fact]
    public void Cell_borders_after_a_paragraph_border_still_belong_to_the_cell()
    {
        var document = TextInterchange.Read(Encoding.ASCII.GetBytes(
            @"{\rtf1\ansi\pard\brdrt\brdrs\brdrw10 Above\par\trowd\clbrdrb\brdrdot\brdrw20\cellx2000\pard\intbl A\cell\row\pard After\par}"), _formats.RtfReader);

        Assert.NotNull(((ParagraphBlock)document.Body[0]).Content.Borders?.Top);
        var table = document.Body.Blocks.OfType<TableBlock>().Single();
        Assert.Equal(BorderLineStyle.Dotted, table.Rows[0].Cells[0].Borders?.Bottom?.Style);
        Assert.Null(table.Rows[0].Cells[0].Borders?.Top);
    }
}

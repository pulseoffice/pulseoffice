using System.Text;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Interchange.Text.Html;
using PulseOffice.Formats.Interchange.Text.Outline;
using PulseOffice.Formats.Interchange.Text.Rtf;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Interchange.Tests.Text;

public sealed class Round119ReaderTests
{
    private readonly TextInterchange _formats = new();
    private readonly HtmlTableReader _tables = new(new HtmlTokenizer(), new CssDeclarations(), new HtmlCellBorders(new CssDeclarations()));

    private static HtmlPageRenderer Pages()
    {
        var resolver = new StyleResolver();
        return new HtmlPageRenderer(new ParagraphSemantics(resolver, new StyleChain()), resolver, new DirectFormatting());
    }

    [Fact]
    public void A_row_whose_cells_are_all_spanned_from_above_is_kept()
    {
        var table = _tables.FirstTable("<table><tr><td rowspan=\"2\">A</td><td rowspan=\"2\">B</td></tr><tr></tr><tr><td>C</td><td>D</td></tr></table>")!;

        Assert.Equal(3, table.Rows.Count);
        Assert.Equal("C", table.Rows[2][0]!.Text);
        Assert.Equal(2, table.Rows[0][0]!.RowSpan);
    }

    [Fact]
    public void A_row_span_beyond_the_last_row_is_cut_to_the_table()
    {
        var table = _tables.FirstTable("<table><tr><td rowspan=\"50\">A</td><td>B</td></tr><tr><td>C</td></tr></table>")!;

        Assert.Equal(2, table.Rows[0][0]!.RowSpan);
    }

    [Fact]
    public void Trailing_cells_covered_from_above_are_placeholders()
    {
        var table = _tables.FirstTable("<table><tr><td>A</td><td rowspan=\"2\">B</td></tr><tr><td>C</td></tr></table>")!;

        Assert.Equal(2, table.Rows[1].Count);
        Assert.Null(table.Rows[1][1]);
    }

    [Fact]
    public void An_empty_border_attribute_draws_the_table_borders()
    {
        var borders = new HtmlCellBorders(new CssDeclarations());
        var tag = new HtmlTokenizer().Tokenize("<table border>").OfType<HtmlStartTag>().Single();

        Assert.True(borders.TableBordered(tag));
    }

    [Fact]
    public void A_huge_picture_width_does_not_overflow_to_the_minimum()
    {
        var picture = new RtfPicture();
        picture.Set("picw", 200_000_000);

        Assert.Equal(int.MaxValue, picture.Width(null));
    }

    [Fact]
    public void A_list_header_survives_html_as_an_unnumbered_item()
    {
        var document = TextInterchange.Rich();
        document.Body.Replace(
        [
            new ParagraphBlock("a", new TextParagraph("Uno") { Runs = [new TextRun("Uno")], ListStyleName = DefaultStyleNames.Numbering123, Numbered = true }),
            new ParagraphBlock("b", new TextParagraph("Testa") { Runs = [new TextRun("Testa")], ListStyleName = DefaultStyleNames.Numbering123, Numbered = true, ListHeader = true }),
            new ParagraphBlock("c", new TextParagraph("Due") { Runs = [new TextRun("Due")], ListStyleName = DefaultStyleNames.Numbering123, Numbered = true }),
        ]);

        var html = Encoding.UTF8.GetString(TextInterchange.Bytes(document, _formats.HtmlWriter));
        var back = TextInterchange.Read(Encoding.UTF8.GetBytes(html), _formats.HtmlReader);

        Assert.Contains("list-style-type:none", html);
        var paragraphs = back.Body.Blocks.OfType<ParagraphBlock>().ToList();
        Assert.True(paragraphs.Single(paragraph => paragraph.Content.Text == "Testa").Content.ListHeader);
        Assert.False(paragraphs.Single(paragraph => paragraph.Content.Text == "Due").Content.ListHeader);
    }

    [Fact]
    public void Heading_ids_never_take_the_name_of_a_bookmark()
    {
        var document = TextInterchange.Rich();
        document.Body.Replace(
        [
            new ParagraphBlock("a", new TextParagraph("Primo") { StyleName = DefaultStyleNames.HeadingLevel(1), Runs = [new TextRun("Primo")] }),
            new ParagraphBlock("b", new TextParagraph("Secondo") { StyleName = DefaultStyleNames.HeadingLevel(1), Runs = [new TextRun("Secondo")] })
            {
                Marks = [new BookmarkMark(0, "h1", MarkEdge.Point), new BookmarkMark(0, "h2", MarkEdge.Point)],
            },
        ]);

        var page = Pages().Render(document, HtmlFlavor.Strict, new InlineImageSink());

        Assert.Equal(2, page.Headings.Select(heading => heading.Id).Distinct().Count());
        Assert.DoesNotContain(page.Headings, heading => heading.Id is "h1" or "h2");
        foreach (var heading in page.Headings) Assert.Single(System.Text.RegularExpressions.Regex.Matches(page.Markup, $"id=\"{heading.Id}\""));
    }

    [Fact]
    public void An_undefined_numeric_field_kind_is_ignored()
    {
        var document = TextInterchange.Read("<html><body><p><span data-field=\"9999\">x</span></p></body></html>"u8.ToArray(), _formats.HtmlReader);

        var runs = document.Body.Blocks.OfType<ParagraphBlock>().SelectMany(paragraph => paragraph.Content.Runs);
        Assert.All(runs, run => Assert.True(Enum.IsDefined(run.Field)));
    }

    [Fact]
    public void A_relative_font_size_grows_from_the_base_size()
    {
        var document = TextInterchange.Read("<html><body><p><font size=\"+1\">x</font></p></body></html>"u8.ToArray(), _formats.HtmlReader);

        var run = document.Body.Blocks.OfType<ParagraphBlock>().SelectMany(paragraph => paragraph.Content.Runs).Single(item => item.Text == "x");
        Assert.Equal(13.5, run.FontSize);
    }
}

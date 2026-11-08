using System.Text;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using PulseOffice.Text.Rich.Bidi;
using Xunit;

namespace PulseOffice.Formats.Interchange.Tests.Text;

public sealed class InterchangeRound115Tests
{
    private readonly TextInterchange _formats = new();

    private TextDocument RightToLeft()
    {
        var document = _formats.Factory.Create();
        var paragraph = new TextParagraph(string.Empty) { Direction = TextDirection.RightToLeft, Align = TextAlign.Right, Explicit = ParagraphOverrides.Align }
            .WithRuns([new TextRun("שלום") { RightToLeft = true }, new TextRun(" abc")]);
        document.Body.Replace([new ParagraphBlock("p1", paragraph)]);
        return document;
    }

    private static TextParagraph First(TextDocument document) => document.Body.Blocks.OfType<ParagraphBlock>().First().Content;

    [Fact]
    public void Rtf_keeps_paragraph_direction_and_right_to_left_runs()
    {
        var read = First(TextInterchange.Read(TextInterchange.Bytes(RightToLeft(), _formats.RtfWriter), _formats.RtfReader));
        Assert.Equal(TextDirection.RightToLeft, read.Direction);
        Assert.True(read.Runs[0].RightToLeft);
        Assert.NotEqual(true, read.Runs[^1].RightToLeft);
    }

    [Fact]
    public void Html_keeps_paragraph_direction_logical_alignment_and_right_to_left_runs()
    {
        var bytes = TextInterchange.Bytes(RightToLeft(), _formats.HtmlWriter);
        Assert.Contains("dir=\"rtl\"", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
        var read = First(TextInterchange.Read(bytes, _formats.HtmlReader));
        Assert.Equal(TextDirection.RightToLeft, read.Direction);
        Assert.Equal(TextAlign.Right, read.Align);
        Assert.True(read.Runs[0].RightToLeft);
    }

    [Fact]
    public void Html_reads_physical_right_alignment_of_a_right_to_left_paragraph_as_its_start()
    {
        var read = First(TextInterchange.Read(Encoding.UTF8.GetBytes("<html><body><p dir=\"rtl\" style=\"text-align:right\">x</p></body></html>"), _formats.HtmlReader));
        Assert.Equal(TextAlign.Left, read.Align);
    }

    [Fact]
    public void Html_style_names_that_sanitize_alike_get_distinct_classes()
    {
        var document = _formats.Factory.Create();
        document.Styles.Paragraph.Add(new ParagraphStyle("A b") { ParentName = DefaultStyleNames.Standard, Character = new CharacterProperties { Bold = true } });
        document.Styles.Paragraph.Add(new ParagraphStyle("A_b") { ParentName = DefaultStyleNames.Standard, Character = new CharacterProperties { Italic = true } });
        document.Body.Replace([new ParagraphBlock("p1", "one", "A b"), new ParagraphBlock("p2", "two", "A_b")]);
        var html = Encoding.UTF8.GetString(TextInterchange.Bytes(document, _formats.HtmlWriter));
        Assert.Contains("class=\"p-A_b\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"p-A_b-2\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Html_declared_charset_decodes_legacy_bytes()
    {
        var cyrillic = System.Text.CodePagesEncodingProvider.Instance.GetEncoding(1251)!;
        var head = Encoding.ASCII.GetBytes("<html><head><meta charset=\"windows-1251\"></head><body><p>");
        var bytes = head.Concat(cyrillic.GetBytes("Привет")).Concat(Encoding.ASCII.GetBytes("</p></body></html>")).ToArray();
        Assert.Equal("Привет", First(TextInterchange.Read(bytes, _formats.HtmlReader)).Text);
    }

    [Fact]
    public void Html_numeric_field_kinds_are_not_read_as_fields()
    {
        var read = First(TextInterchange.Read(Encoding.UTF8.GetBytes("<html><body><p><span data-field=\"999\">x</span></p></body></html>"), _formats.HtmlReader));
        Assert.All(read.Runs, run => Assert.Equal(TextFieldKind.None, run.Field));
    }
}

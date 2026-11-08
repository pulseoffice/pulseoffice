using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxBlockPropertiesTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void Nested_sections_keep_name_protection_hidden_state_condition_and_link_through_docx_and_odt()
    {
        var document = new TextDocument();
        var inner = new SectionBlock("s2", "Inner", new BlockContainer([new ParagraphBlock("p2", "inner", DefaultStyleNames.Standard)]))
        {
            Hidden = true,
            Condition = "x == 1",
        };
        var outer = new SectionBlock("s1", "Outer", new BlockContainer([new ParagraphBlock("p1", "first", DefaultStyleNames.Standard), inner, new ParagraphBlock("p3", "last", DefaultStyleNames.Standard)]))
        {
            Protected = true,
            ProtectionKey = [1, 2, 3, 4],
            LinkedFile = "chapter.odt",
        };
        document.Body.Replace([new ParagraphBlock("p0", "before", DefaultStyleNames.Standard), outer, new ParagraphBlock("p4", "after", DefaultStyleNames.Standard)]);

        foreach (var read in new[] { ThroughDocx(document), ThroughOdt(ThroughDocx(document)) })
        {
            var section = Assert.Single(read.Body.Blocks.OfType<SectionBlock>());
            Assert.Equal("Outer", section.Name);
            Assert.True(section.Protected);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, section.ProtectionKey);
            Assert.Equal("chapter.odt", section.LinkedFile);
            Assert.Equal(3, section.Content.Count);
            var nested = Assert.IsType<SectionBlock>(section.Content[1]);
            Assert.Equal("Inner", nested.Name);
            Assert.True(nested.Hidden);
            Assert.Equal("x == 1", nested.Condition);
            Assert.Equal("inner", Assert.IsType<ParagraphBlock>(Assert.Single(nested.Content.Blocks)).Text);
        }
    }

    [Fact]
    public void Carrier_bookmarks_stay_out_of_the_bookmark_list_and_unknown_entries_are_ignored()
    {
        var document = new TextDocument();
        document.Body.Replace([new SectionBlock("s1", "Named", new BlockContainer([new ParagraphBlock("p1", "x", DefaultStyleNames.Standard)]))]);
        var bytes = Write(document);
        using (var package = WordprocessingDocument.Open(new MemoryStream(bytes), false))
            Assert.Contains(package.MainDocumentPart!.Document!.Body!.Descendants<W.BookmarkStart>(), start => start.Name!.Value!.StartsWith(DocxBlockProperties.BookmarkPrefix, StringComparison.Ordinal));
        var read = _composer.Reader(DocxFlavour.Document).Read(new MemoryStream(bytes)).Value!;
        Assert.DoesNotContain(read.Body.Blocks.OfType<SectionBlock>().SelectMany(section => section.Content.Blocks).OfType<ParagraphBlock>().SelectMany(paragraph => paragraph.Marks),
            mark => mark is PulseOffice.Documents.Marks.BookmarkMark);
    }

    [Fact]
    public void A_hidden_paragraph_field_keeps_its_condition_and_state_through_docx()
    {
        var document = new TextDocument();
        document.Body.Replace([new ParagraphBlock("p1", new TextParagraph(string.Empty).WithRuns(
            [new TextRun("text"), new TextRun(PulseOffice.Documents.Fields.ConditionalFieldSource.HiddenParagraphMark) { Field = TextFieldKind.HiddenParagraph, FieldArgument = "a == 2", Hidden = true }]))]);

        var bytes = Write(document);
        var read = _composer.Reader(DocxFlavour.Document).Read(new MemoryStream(bytes)).Value!;

        var run = Assert.Single(read.Body.Blocks.OfType<ParagraphBlock>().SelectMany(paragraph => paragraph.Content.Runs), candidate => candidate.Field == TextFieldKind.HiddenParagraph);
        Assert.Equal("a == 2", run.FieldArgument);
        Assert.True(run.Hidden);
        using var package = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        Assert.Contains(package.MainDocumentPart!.Document!.Body!.Descendants<W.SimpleField>(), field => field.Instruction!.Value!.TrimStart().StartsWith("IF ", StringComparison.Ordinal));
    }

    [Fact]
    public void A_cell_number_format_with_symbol_survives_docx()
    {
        var document = new TextDocument();
        var cell = new TableCell(new BlockContainer([new ParagraphBlock("p1", "12,00 €", DefaultStyleNames.Standard)]))
        {
            NumberFormat = new CellNumberFormat(CellValueKind.Currency, 3, false) { Symbol = "€" },
        };
        document.Body.Replace([new TableBlock("t1", [new TableRow([cell])], [Twip.FromPoints(100)])]);

        var read = ThroughDocx(document);

        var format = Assert.Single(read.Body.Blocks.OfType<TableBlock>()).Rows[0].Cells[0].NumberFormat;
        Assert.Equal(new CellNumberFormat(CellValueKind.Currency, 3, false) { Symbol = "€" }, format);
    }

    private byte[] Write(TextDocument document)
    {
        using var buffer = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, buffer).Succeeded);
        return buffer.ToArray();
    }

    private TextDocument ThroughDocx(TextDocument document)
    {
        var read = _composer.Reader(DocxFlavour.Document).Read(new MemoryStream(Write(document)));
        Assert.True(read.Succeeded);
        return read.Value!;
    }

    private static TextDocument ThroughOdt(TextDocument document)
    {
        using var buffer = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, buffer).Succeeded);
        buffer.Position = 0;
        return new OdtReader().Read(buffer).Value!;
    }
}

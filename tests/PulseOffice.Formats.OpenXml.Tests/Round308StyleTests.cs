using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round308StyleTests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void Styles_with_built_in_word_names_are_written_as_built_in_with_gallery_flags()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Styles.Paragraph.Add(new ParagraphStyle("Lead") { ParentName = DefaultStyleNames.Standard });
        if (document.Styles.Paragraph.Find("Heading 1") is null) document.Styles.Paragraph.Add(new ParagraphStyle("Heading 1") { ParentName = DefaultStyleNames.Standard, OutlineLevel = 1 });
        document.Body.Replace([new ParagraphBlock("p", "x")]);
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        using var package = WordprocessingDocument.Open(stream, false);
        var styles = package.MainDocumentPart!.StyleDefinitionsPart!.Styles!.Elements<W.Style>().ToList();

        var heading = styles.Single(style => style.StyleName?.Val?.Value == "heading 1");
        Assert.Null(heading.CustomStyle);
        Assert.Equal(9, heading.UIPriority?.Val?.Value);
        Assert.NotNull(heading.PrimaryStyle);
        var lead = styles.Single(style => style.StyleName?.Val?.Value == "Lead");
        Assert.True(lead.CustomStyle?.Value);
        Assert.Null(lead.UIPriority);
    }

    [Fact]
    public void A_heading_numbered_by_a_second_list_keeps_that_list()
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.AddNewPart<NumberingDefinitionsPart>().Numbering = new W.Numbering(
                $"<w:numbering {Namespace}>"
                + "<w:abstractNum w:abstractNumId=\"0\"><w:lvl w:ilvl=\"0\"><w:start w:val=\"1\"/><w:numFmt w:val=\"decimal\"/><w:lvlText w:val=\"%1.\"/></w:lvl><w:lvl w:ilvl=\"1\"><w:start w:val=\"1\"/><w:numFmt w:val=\"decimal\"/><w:lvlText w:val=\"%1.%2.\"/></w:lvl></w:abstractNum>"
                + "<w:abstractNum w:abstractNumId=\"1\"><w:lvl w:ilvl=\"0\"><w:start w:val=\"1\"/><w:numFmt w:val=\"upperLetter\"/><w:lvlText w:val=\"%1)\"/></w:lvl><w:lvl w:ilvl=\"1\"><w:start w:val=\"1\"/><w:numFmt w:val=\"lowerLetter\"/><w:lvlText w:val=\"%2)\"/></w:lvl></w:abstractNum>"
                + "<w:num w:numId=\"1\"><w:abstractNumId w:val=\"0\"/></w:num><w:num w:numId=\"2\"><w:abstractNumId w:val=\"1\"/></w:num></w:numbering>");
            main.AddNewPart<StyleDefinitionsPart>().Styles = new W.Styles(
                $"<w:styles {Namespace}>"
                + "<w:style w:type=\"paragraph\" w:styleId=\"Heading1\"><w:name w:val=\"heading 1\"/><w:pPr><w:numPr><w:ilvl w:val=\"0\"/><w:numId w:val=\"1\"/></w:numPr><w:outlineLvl w:val=\"0\"/></w:pPr></w:style>"
                + "<w:style w:type=\"paragraph\" w:styleId=\"Heading2\"><w:name w:val=\"heading 2\"/><w:pPr><w:numPr><w:ilvl w:val=\"1\"/><w:numId w:val=\"2\"/></w:numPr><w:outlineLvl w:val=\"1\"/></w:pPr></w:style>"
                + "</w:styles>");
            main.Document = new W.Document($"<w:document {Namespace}><w:body><w:p><w:pPr><w:pStyle w:val=\"Heading2\"/></w:pPr><w:r><w:t>T</w:t></w:r></w:p></w:body></w:document>");
        }
        stream.Position = 0;

        var read = _composer.Reader(DocxFlavour.Document).Read(stream).Value!;

        Assert.Null(read.Styles.Paragraph.Find("Heading 1")!.ListStyleName);
        var second = read.Styles.Paragraph.Find("Heading 2")!;
        Assert.NotNull(second.ListStyleName);
        Assert.Equal(2, second.ListLevel);
        Assert.Equal(NumberFormat.Arabic, read.Styles.Outline.Level(0).Format);
    }
}

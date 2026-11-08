using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxTenthAuditLinkTests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void Il_testo_eliminato_dentro_un_collegamento_resta_nel_collegamento_in_docx_e_in_odt()
    {
        var document = _composer.Reader(DocxFlavour.Document).Read(Docx("<w:p><w:r><w:t>vedi </w:t></w:r><w:hyperlink w:anchor=\"segno\"><w:del w:id=\"1\" w:author=\"a\"><w:r><w:delText>tolto</w:delText></w:r></w:del>"
            + "<w:r><w:t>resta</w:t></w:r></w:hyperlink><w:del w:id=\"2\" w:author=\"a\"><w:r><w:delText> fuori</w:delText></w:r></w:del></w:p>")).Value!;
        var deletion = document.Changes.All.OfType<Deletion>().First();
        Assert.Equal("#segno", deletion.Removed.Blocks.OfType<ParagraphBlock>().Single().Content.Runs.Single().Hyperlink);

        var written = new MemoryStream();
        _composer.Writer(DocxFlavour.Document).Write(document, written);
        written.Position = 0;
        using var reopened = WordprocessingDocument.Open(written, false);
        var link = Assert.Single(reopened.MainDocumentPart!.Document!.Descendants<W.Hyperlink>());
        Assert.Equal("tolto", string.Concat(link.Descendants<W.DeletedText>().Select(text => text.Text)));
        Assert.Equal("resta", string.Concat(link.Descendants<W.Text>().Select(text => text.Text)));

        var odt = new MemoryStream();
        new OdtWriter().Write(document, odt);
        odt.Position = 0;
        var again = new OdtReader().Read(odt).Value!;
        Assert.Contains(again.Changes.All.OfType<Deletion>(), item => item.Removed.Blocks.OfType<ParagraphBlock>().Any(paragraph => paragraph.Content.Runs.Any(run => run.Hyperlink == "#segno")));
    }

    [Fact]
    public void Una_eliminazione_che_tocca_testo_e_collegamento_si_scrive_divisa()
    {
        var document = new TextDocument();
                var removed = new TextParagraph(string.Empty).WithRuns([new TextRun("a "), new TextRun("uno") { Hyperlink = "#uno" }]);
        var deletion = new Deletion("c1", "a", DateTimeOffset.MinValue, new BlockContainer([new ParagraphBlock("p2", removed)]));
        document.Changes.Add(deletion);
        document.Body.Add(new ParagraphBlock("p1", new TextParagraph("dopo")) { Marks = [new PulseOffice.Documents.Marks.ChangeMark(0, deletion.Id, PulseOffice.Documents.Marks.MarkEdge.Point)] });

        var written = new MemoryStream();
        _composer.Writer(DocxFlavour.Document).Write(document, written);
        written.Position = 0;
        using var reopened = WordprocessingDocument.Open(written, false);
        var body = reopened.MainDocumentPart!.Document!.Body!;
        var link = Assert.Single(body.Descendants<W.Hyperlink>());
        Assert.Equal("uno", string.Concat(link.Descendants<W.DeletedText>().Select(text => text.Text)));
        Assert.Equal(2, body.Descendants<W.DeletedRun>().Count());
    }

    [Theory]
    [InlineData("<w:hyperlink w:anchor=\"segno\"><w:r><w:rPr/></w:r></w:hyperlink>")]
    [InlineData("<w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText> HYPERLINK \"https://example.org/\" </w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"separate\"/></w:r><w:r><w:fldChar w:fldCharType=\"end\"/></w:r>")]
    public void Un_collegamento_senza_testo_si_dichiara(string link)
    {
        var read = _composer.Reader(DocxFlavour.Document).Read(Docx($"<w:p><w:r><w:t>prima</w:t></w:r>{link}</w:p>"));
        Assert.Contains(read.Diagnostics, diagnostic => diagnostic.Code == "DOCX170");
    }

    [Fact]
    public void Un_collegamento_con_testo_non_si_dichiara()
    {
        var read = _composer.Reader(DocxFlavour.Document).Read(Docx("<w:p><w:hyperlink w:anchor=\"segno\"><w:r><w:t>vai</w:t></w:r></w:hyperlink></w:p>"));
        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "DOCX170");
    }

    [Fact]
    public void Uno_stile_di_pagina_che_nessuna_pagina_usa_con_il_piede_pieno_si_dichiara()
    {
        var document = new TextDocument();
        var unused = new PageStyle("Inutile");
        unused.Footer.Enabled = true;
        unused.Footer.Default.Add(new ParagraphBlock("pf", new TextParagraph("piede")));
        document.Styles.Page.Add(unused);
        Assert.DoesNotContain(_composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses, loss => loss.Code == "DOCX171");
        Assert.DoesNotContain(_composer.Writer(DocxFlavour.Document).CheckFidelity(new TextDocument()).Losses, loss => loss.Code == "DOCX171");
    }

    private static MemoryStream Docx(string body)
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespace}><w:body>{body}</w:body></w:document>");
        stream.Position = 0;
        return stream;
    }
}

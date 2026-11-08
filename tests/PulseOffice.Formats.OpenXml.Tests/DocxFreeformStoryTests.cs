using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxFreeformStoryTests
{
    private const string Namespaces = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:wps=\"http://schemas.microsoft.com/office/word/2010/wordprocessingShape\"";
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void Il_testo_di_una_forma_libera_attraversa_docx_e_odt()
    {
        var read = _composer.Reader(DocxFlavour.Document).Read(Docx("Testo"));
        Assert.NotNull(read.Value);
        Assert.Equal("Testo", StoryText(read.Value!));
        Assert.Equal("Testo", StoryText(ThroughDocx(read.Value!)));
        Assert.Equal("Testo", StoryText(ThroughOdt(read.Value!)));
        Assert.Equal("Testo", StoryText(ThroughDocx(ThroughOdt(read.Value!))));
    }

    [Fact]
    public void Una_forma_libera_senza_testo_resta_senza_storia()
    {
        var read = _composer.Reader(DocxFlavour.Document).Read(Docx(""));
        Assert.Null(Freeform(read.Value!).Content.Story);
    }

    private static AnchoredObject Freeform(TextDocument document) =>
        document.Objects.All.Single(item => item.Content is EmbeddedContent { Value: FreeformObject });

    private static string StoryText(TextDocument document) =>
        string.Concat(Freeform(document).Content.Story!.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text));

    private TextDocument ThroughDocx(TextDocument document)
    {
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        return _composer.Reader(DocxFlavour.Document).Read(stream).Value!;
    }

    private static TextDocument ThroughOdt(TextDocument document)
    {
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        return new OdtReader().Read(stream).Value!;
    }

    private static MemoryStream Docx(string text)
    {
        var box = text.Length == 0 ? "" : $"<wps:txbx><w:txbxContent><w:p><w:r><w:t>{text}</w:t></w:r></w:p></w:txbxContent></wps:txbx>";
        var shape = "<wps:wsp><wps:cNvSpPr txBox=\"1\"/><wps:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"914400\" cy=\"914400\"/></a:xfrm><a:custGeom><a:pathLst><a:path w=\"100\" h=\"100\"><a:moveTo><a:pt x=\"0\" y=\"0\"/></a:moveTo><a:lnTo><a:pt x=\"100\" y=\"50\"/></a:lnTo><a:lnTo><a:pt x=\"0\" y=\"100\"/></a:lnTo><a:close/></a:path></a:pathLst></a:custGeom></wps:spPr>" + box + "<wps:bodyPr/></wps:wsp>";
        var drawing = "<w:drawing><wp:anchor distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\" simplePos=\"0\" relativeHeight=\"1\" behindDoc=\"0\" locked=\"0\" layoutInCell=\"1\" allowOverlap=\"1\"><wp:simplePos x=\"0\" y=\"0\"/><wp:positionH relativeFrom=\"column\"><wp:posOffset>0</wp:posOffset></wp:positionH><wp:positionV relativeFrom=\"paragraph\"><wp:posOffset>0</wp:posOffset></wp:positionV><wp:extent cx=\"914400\" cy=\"914400\"/><wp:wrapNone/><wp:docPr id=\"1\" name=\"Forma\"/><wp:cNvGraphicFramePr/><a:graphic><a:graphicData uri=\"http://schemas.microsoft.com/office/word/2010/wordprocessingShape\">" + shape + "</a:graphicData></a:graphic></wp:anchor></w:drawing>";
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document($"<w:document {Namespaces}><w:body><w:p><w:r>{drawing}</w:r></w:p></w:body></w:document>");
        }

        stream.Position = 0;
        return stream;
    }
}

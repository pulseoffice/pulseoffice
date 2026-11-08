using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxAnnotationAnchorTests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private MemoryStream Package(string body, string? comments = null, string? footnotes = null)
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document($"<w:document {Namespace}><w:body>{body}</w:body></w:document>");
            if (comments is not null)
                main.AddNewPart<WordprocessingCommentsPart>().Comments = new W.Comments($"<w:comments {Namespace}>{comments}</w:comments>");
            if (footnotes is not null)
                main.AddNewPart<FootnotesPart>().Footnotes = new W.Footnotes($"<w:footnotes {Namespace}>{footnotes}</w:footnotes>");
        }

        stream.Position = 0;
        return stream;
    }

    private TextDocument Read(Stream stream)
    {
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        Assert.True(read.Succeeded);
        return read.Value!;
    }

    private TextDocument Through(TextDocument document, ITextDocumentWriter writer, ITextDocumentReader reader)
    {
        using var stream = new MemoryStream();
        Assert.True(writer.Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = reader.Read(stream);
        Assert.True(read.Succeeded);
        return read.Value!;
    }

    private IEnumerable<InlineMark> AllMarks(TextDocument document)
    {
        var tree = new BlockTree();
        return tree.Paragraphs(document.Body)
            .Concat(document.Changes.All.OfType<Deletion>().SelectMany(deletion => tree.Paragraphs(deletion.Removed)))
            .SelectMany(paragraph => paragraph.Marks);
    }

    [Fact]
    public void UnCommentoConIlSoloRiferimentoSenzaIntervalloSopravviveInOdt()
    {
        using var stream = Package(
            "<w:p><w:r><w:t>A</w:t></w:r><w:r><w:commentReference w:id=\"0\"/></w:r></w:p>",
            "<w:comment w:id=\"0\" w:author=\"Ada\"><w:p><w:r><w:t>nota</w:t></w:r></w:p></w:comment>");
        var document = Read(stream);

        Assert.Single(document.Comments.All);
        Assert.Single(AllMarks(document).OfType<CommentMark>());
        var odt = Through(document, new OdtWriter(), new OdtReader());
        Assert.Single(odt.Comments.All);
    }

    [Fact]
    public void UnaNotaEUnCommentoNelTestoCancellatoSopravvivonoAiDueFormati()
    {
        using var stream = Package(
            "<w:p><w:r><w:t>foo</w:t></w:r><w:del w:id=\"9\" w:author=\"Ada\"><w:r><w:delText>ba</w:delText></w:r><w:r><w:footnoteReference w:id=\"1\"/></w:r><w:r><w:commentReference w:id=\"0\"/></w:r><w:r><w:delText>r</w:delText></w:r></w:del></w:p>",
            "<w:comment w:id=\"0\" w:author=\"Ada\"><w:p><w:r><w:t>nota</w:t></w:r></w:p></w:comment>",
            "<w:footnote w:id=\"1\"><w:p><w:r><w:t>piede</w:t></w:r></w:p></w:footnote>");
        var document = Read(stream);

        var deletion = Assert.Single(document.Changes.All.OfType<Deletion>());
        Assert.Equal("bar", string.Concat(new BlockTree().Paragraphs(deletion.Removed).Select(paragraph => paragraph.Text)));
        foreach (var copy in new[]
                 {
                     Through(document, new OdtWriter(), new OdtReader()),
                     Through(document, _composer.Writer(DocxFlavour.Document), _composer.Reader(DocxFlavour.Document)),
                 })
        {
            Assert.Single(copy.Notes.All);
            Assert.Single(copy.Comments.All);
            Assert.Single(AllMarks(copy).OfType<NoteMark>());
        }
    }

    [Fact]
    public void ICommentiELeNoteSenzaAncoraggioNonSiImportanoEDichiaranoLaPerdita()
    {
        using var stream = Package(
            "<w:p><w:r><w:t>testo</w:t></w:r></w:p>",
            "<w:comment w:id=\"0\" w:author=\"Ada\"><w:p><w:r><w:t>orfano</w:t></w:r></w:p></w:comment>",
            "<w:footnote w:id=\"1\"><w:p><w:r><w:t>orfana</w:t></w:r></w:p></w:footnote>");
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);

        Assert.True(read.Succeeded);
        Assert.Empty(read.Value!.Comments.All);
        Assert.Empty(read.Value.Notes.All);
        Assert.Contains(read.Diagnostics, diagnostic => diagnostic.Code == "DOCX113");
    }

    [Fact]
    public void UnaSezioneContinuaNonCreaOggettiOrfaniDalleIntestazioniCheRilegge()
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            var header = main.AddNewPart<HeaderPart>();
            var image = header.AddImagePart(ImagePartType.Png);
            image.FeedData(new MemoryStream(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==")));
            const string drawing = "xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:pic=\"http://schemas.openxmlformats.org/drawingml/2006/picture\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"";
            header.Header = new W.Header($"<w:hdr {Namespace} {drawing}><w:p><w:r><w:drawing><wp:inline><wp:extent cx=\"914400\" cy=\"914400\"/><wp:docPr id=\"1\" name=\"logo\"/><a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><pic:pic><pic:nvPicPr><pic:cNvPr id=\"1\" name=\"logo\"/><pic:cNvPicPr/></pic:nvPicPr><pic:blipFill><a:blip r:embed=\"{header.GetIdOfPart(image)}\"/></pic:blipFill><pic:spPr/></pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p></w:hdr>");
            var id = main.GetIdOfPart(header);
            var sections = $"<w:headerReference xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" w:type=\"default\" r:id=\"{id}\"/>";
            main.Document = new W.Document($"<w:document {Namespace}><w:body><w:p><w:pPr><w:sectPr>{sections}</w:sectPr></w:pPr><w:r><w:t>uno</w:t></w:r></w:p><w:p><w:r><w:t>due</w:t></w:r></w:p><w:sectPr><w:type w:val=\"continuous\"/>{sections}<w:cols w:num=\"2\"/></w:sectPr></w:body></w:document>");
        }

        stream.Position = 0;
        var document = Read(stream);

        var stories = new PulseOffice.Documents.Editing.StoryCatalog(new BlockTree());
        var paragraphs = stories.Paragraphs(document).Select(paragraph => paragraph.Id).ToHashSet();
        var item = Assert.Single(document.Objects.All);
        Assert.Contains(Assert.IsAssignableFrom<PulseOffice.Documents.Objects.ObjectAnchor>(item.Anchor) switch
        {
            PulseOffice.Documents.Objects.AsCharacterAnchor anchor => anchor.ParagraphId,
            PulseOffice.Documents.Objects.CharacterAnchor anchor => anchor.ParagraphId,
            _ => string.Empty,
        }, paragraphs);
    }

    [Fact]
    public void ISegnalibriNascostiRiferitiDaUnCampoOEsternoSiConservano()
    {
        using var stream = Package(
            "<w:p><w:bookmarkStart w:id=\"1\" w:name=\"_Ref42\"/><w:bookmarkStart w:id=\"2\" w:name=\"_Nascosto\"/><w:bookmarkStart w:id=\"3\" w:name=\"__RefHeading__7_1\"/><w:bookmarkStart w:id=\"4\" w:name=\"_GoBack\"/><w:bookmarkStart w:id=\"5\" w:name=\"__DdeLink__12_345\"/><w:bookmarkStart w:id=\"6\" w:name=\"_Toc99\"/>"
            + "<w:r><w:t>Titolo</w:t></w:r><w:bookmarkEnd w:id=\"1\"/><w:bookmarkEnd w:id=\"2\"/><w:bookmarkEnd w:id=\"3\"/><w:bookmarkEnd w:id=\"4\"/><w:bookmarkEnd w:id=\"5\"/><w:bookmarkEnd w:id=\"6\"/></w:p>"
            + "<w:p><w:fldSimple w:instr=\" PAGEREF _Nascosto \\h \"><w:r><w:t>1</w:t></w:r></w:fldSimple></w:p>");
        var document = Read(stream);

        var names = AllMarks(document).OfType<BookmarkMark>().Select(mark => mark.Name).ToHashSet();
        Assert.Contains("_Ref42", names);
        Assert.Contains("_Nascosto", names);
        Assert.Contains("__RefHeading__7_1", names);
        Assert.DoesNotContain("_Toc99", names);
        foreach (var kept in new[] { document, Through(document, new OdtWriter(), new OdtReader()), Through(document, _composer.Writer(DocxFlavour.Document), _composer.Reader(DocxFlavour.Document)) })
        {
            var keptNames = AllMarks(kept).OfType<BookmarkMark>().Select(mark => mark.Name).ToHashSet();
            Assert.Contains("_GoBack", keptNames);
            Assert.Contains("__DdeLink__12_345", keptNames);
        }
    }
}

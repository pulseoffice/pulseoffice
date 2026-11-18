using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round196DocxMovedParagraphMarkTests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private const string Stamp = "w:author=\"a\" w:date=\"2026-01-01T00:00:00Z\"";
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private MemoryStream MovedParagraph()
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespace}><w:body>"
                + $"<w:p><w:pPr><w:rPr><w:moveFrom w:id=\"3\" {Stamp}/></w:rPr></w:pPr><w:moveFromRangeStart w:id=\"1\" w:name=\"move1\" {Stamp}/>"
                + $"<w:moveFrom w:id=\"4\" {Stamp}><w:r><w:t>moved</w:t></w:r></w:moveFrom><w:moveFromRangeEnd w:id=\"1\"/></w:p>"
                + "<w:p><w:r><w:t>stays</w:t></w:r></w:p>"
                + $"<w:p><w:pPr><w:rPr><w:moveTo w:id=\"5\" {Stamp}/></w:rPr></w:pPr><w:moveToRangeStart w:id=\"2\" w:name=\"move1\" {Stamp}/>"
                + $"<w:moveTo w:id=\"6\" {Stamp}><w:r><w:t>moved</w:t></w:r></w:moveTo><w:moveToRangeEnd w:id=\"2\"/></w:p>"
                + "<w:p><w:r><w:t>end</w:t></w:r></w:p>"
                + "</w:body></w:document>");
        }
        stream.Position = 0;
        return stream;
    }

    private static void AssertOneMove(TextDocument document)
    {
        var changes = document.Changes.All.Where(change => change is Insertion or Deletion).ToList();
        Assert.Contains(changes, change => change is Deletion);
        Assert.Contains(changes, change => change is Insertion);
        var moves = changes.Select(change => change is Insertion insertion ? insertion.MoveId : ((Deletion)change).MoveId).Distinct().ToList();
        Assert.NotNull(Assert.Single(moves));
    }

    [Fact]
    public void A_moved_paragraph_mark_joins_the_move_of_its_text()
    {
        using var stream = MovedParagraph();

        var read = _composer.Reader(DocxFlavour.Document).Read(stream);

        Assert.True(read.Succeeded);
        AssertOneMove(read.Value!);
    }

    [Fact]
    public void A_moved_paragraph_mark_is_written_back_as_a_word_move_mark()
    {
        using var source = MovedParagraph();
        var document = _composer.Reader(DocxFlavour.Document).Read(source).Value!;

        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        using (var package = WordprocessingDocument.Open(stream, false))
        {
            var body = package.MainDocumentPart!.Document!.Body!;
            Assert.NotEmpty(body.Descendants<W.ParagraphMarkRunProperties>().SelectMany(mark => mark.Elements<W.MoveFrom>()));
            Assert.NotEmpty(body.Descendants<W.ParagraphMarkRunProperties>().SelectMany(mark => mark.Elements<W.MoveTo>()));
            Assert.Empty(body.Descendants<W.ParagraphMarkRunProperties>().SelectMany(mark => mark.Elements<W.Deleted>()));
            Assert.Empty(body.Descendants<W.ParagraphMarkRunProperties>().SelectMany(mark => mark.Elements<W.Inserted>()));
        }
        stream.Position = 0;
        AssertOneMove(_composer.Reader(DocxFlavour.Document).Read(stream).Value!);
    }
}

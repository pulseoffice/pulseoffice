using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round162DocxMoveTests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void Moved_text_is_read_as_deletion_and_insertion_and_the_loss_is_declared()
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespace}><w:body>"
                + "<w:p><w:moveFrom w:id=\"1\" w:author=\"a\"><w:r><w:t>moved</w:t></w:r></w:moveFrom><w:r><w:t>stays</w:t></w:r></w:p>"
                + "<w:p><w:r><w:t>end</w:t></w:r><w:moveTo w:id=\"2\" w:author=\"a\"><w:r><w:t>moved</w:t></w:r></w:moveTo></w:p>"
                + "</w:body></w:document>");
        }

        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);

        Assert.True(read.Succeeded);
        Assert.Contains(read.Diagnostics, diagnostic => diagnostic.Code == "DOCX224");
        Assert.Contains(read.Value!.Changes.All, change => change is Deletion);
        Assert.Contains(read.Value.Changes.All, change => change is Insertion);
    }

    [Fact]
    public void A_list_removed_with_tracking_keeps_its_numbering_in_the_previous_paragraph()
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document($"<w:document {Namespace}><w:body><w:p><w:pPr>"
                + "<w:pPrChange w:id=\"1\" w:author=\"a\"><w:pPr><w:numPr><w:ilvl w:val=\"0\"/><w:numId w:val=\"1\"/></w:numPr></w:pPr></w:pPrChange>"
                + "</w:pPr><w:r><w:t>item</w:t></w:r></w:p></w:body></w:document>");
            main.AddNewPart<NumberingDefinitionsPart>().Numbering = new W.Numbering($"<w:numbering {Namespace}>"
                + "<w:abstractNum w:abstractNumId=\"0\"><w:lvl w:ilvl=\"0\"><w:start w:val=\"1\"/><w:numFmt w:val=\"decimal\"/><w:lvlText w:val=\"%1.\"/></w:lvl></w:abstractNum>"
                + "<w:num w:numId=\"1\"><w:abstractNumId w:val=\"0\"/></w:num></w:numbering>");
        }

        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);

        Assert.True(read.Succeeded);
        var change = Assert.Single(read.Value!.Changes.All.OfType<ParagraphFormatChange>());
        Assert.NotNull(change.Previous.ListStyleName);
        Assert.True(change.Previous.Numbered);
        Assert.True(change.Previous.IsListItem);
    }
}

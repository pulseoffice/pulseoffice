using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round223DocxNotePlacementTests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Theory]
    [InlineData(NotePlacement.PageBottom, NotePlacement.DocumentEnd)]
    [InlineData(NotePlacement.BelowText, NotePlacement.SectionEnd)]
    [InlineData(NotePlacement.SectionEnd, NotePlacement.DocumentEnd)]
    [InlineData(NotePlacement.DocumentEnd, NotePlacement.SectionEnd)]
    public void Note_placements_cross_docx(NotePlacement footnotes, NotePlacement endnotes)
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Notes.FootnoteSettings = document.Notes.FootnoteSettings with { Placement = footnotes };
        document.Notes.EndnoteSettings = document.Notes.EndnoteSettings with { Placement = endnotes };

        var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);

        Assert.Equal(footnotes, read.Value!.Notes.FootnoteSettings.Placement);
        Assert.Equal(endnotes, read.Value.Notes.EndnoteSettings.Placement);
        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "DOCX272");
    }

    [Fact]
    public void A_section_placement_overrides_the_document_wide_one()
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.AddNewPart<DocumentSettingsPart>().Settings = new W.Settings($"<w:settings {Namespace}><w:footnotePr><w:pos w:val=\"pageBottom\"/></w:footnotePr></w:settings>");
            main.Document = new W.Document($"<w:document {Namespace}><w:body><w:p/><w:sectPr><w:footnotePr><w:pos w:val=\"beneathText\"/></w:footnotePr><w:endnotePr><w:pos w:val=\"sectEnd\"/></w:endnotePr></w:sectPr></w:body></w:document>");
        }
        stream.Position = 0;

        var read = _composer.Reader(DocxFlavour.Document).Read(stream).Value!;

        Assert.Equal(NotePlacement.BelowText, read.Notes.FootnoteSettings.Placement);
        Assert.Equal(NotePlacement.SectionEnd, read.Notes.EndnoteSettings.Placement);
    }
}

using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Styles;
using PulseOffice.Documents.Annotations;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round219DocxNoteSettingsTests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private TextDocument Read(string settings, string section)
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.AddNewPart<DocumentSettingsPart>().Settings = new W.Settings($"<w:settings {Namespace}>{settings}</w:settings>");
            main.Document = new W.Document($"<w:document {Namespace}><w:body><w:p/><w:sectPr>{section}</w:sectPr></w:body></w:document>");
        }
        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        Assert.True(read.Succeeded);
        return read.Value!;
    }

    [Fact]
    public void Note_numbering_settings_cross_docx()
    {
        var document = Read(
            "<w:footnotePr><w:numFmt w:val=\"lowerLetter\"/><w:numStart w:val=\"3\"/><w:numRestart w:val=\"eachPage\"/></w:footnotePr>"
            + "<w:endnotePr><w:numFmt w:val=\"upperRoman\"/><w:numStart w:val=\"2\"/><w:numRestart w:val=\"eachSect\"/></w:endnotePr>", string.Empty);
        Assert.Equal(new NoteSettings { FormatName = "a", StartAt = 3, Restart = NoteNumbering.Page }, document.Notes.FootnoteSettings with { ParagraphStyleName = null, CitationStyleName = null });
        Assert.Equal(new NoteSettings { FormatName = "I", StartAt = 2, Restart = NoteNumbering.Chapter }, document.Notes.EndnoteSettings with { ParagraphStyleName = null, CitationStyleName = null });

        var written = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, written).Succeeded);
        written.Position = 0;
        using var reopened = WordprocessingDocument.Open(written, false);
        var settings = reopened.MainDocumentPart!.DocumentSettingsPart!.Settings!;
        var footnote = settings.GetFirstChild<W.FootnoteDocumentWideProperties>()!;
        Assert.Equal(W.NumberFormatValues.LowerLetter, footnote.GetFirstChild<W.NumberingFormat>()!.Val!.Value);
        Assert.Equal(3, footnote.GetFirstChild<W.NumberingStart>()!.Val!.Value);
        Assert.Equal(W.RestartNumberValues.EachPage, footnote.GetFirstChild<W.NumberingRestart>()!.Val!.Value);
        var endnote = settings.GetFirstChild<W.EndnoteDocumentWideProperties>()!;
        Assert.Equal(W.NumberFormatValues.UpperRoman, endnote.GetFirstChild<W.NumberingFormat>()!.Val!.Value);
        Assert.Equal(W.RestartNumberValues.EachSection, endnote.GetFirstChild<W.NumberingRestart>()!.Val!.Value);
    }

    [Fact]
    public void Section_note_settings_override_the_document_wide_ones()
    {
        var document = Read("<w:footnotePr><w:numFmt w:val=\"lowerLetter\"/></w:footnotePr>",
            "<w:footnotePr><w:numFmt w:val=\"upperLetter\"/><w:numStart w:val=\"5\"/></w:footnotePr>");

        Assert.Equal("A", document.Notes.FootnoteSettings.FormatName);
        Assert.Equal(5, document.Notes.FootnoteSettings.StartAt);
    }

    [Fact]
    public void Note_formats_word_cannot_number_are_declared()
    {
        var document = Read(string.Empty, string.Empty);
        document.Notes.Add(new Note("en1", NoteKind.Endnote, new PulseOffice.Documents.Blocks.BlockContainer()));
        var writer = _composer.Writer(DocxFlavour.Document);
        Assert.DoesNotContain(writer.CheckFidelity(document).Losses, loss => loss.Code == "DOCX271");

        document.Notes.EndnoteSettings = document.Notes.EndnoteSettings with { Restart = NoteNumbering.Page };

        Assert.Contains(writer.CheckFidelity(document).Losses, loss => loss.Code == "DOCX271");
    }

    [Fact]
    public void The_forward_continuation_notice_crosses_docx_as_the_continuation_notice_note()
    {
        var document = Read(string.Empty, string.Empty);
        document.Notes.Add(new Note("fn1", NoteKind.Footnote, new PulseOffice.Documents.Blocks.BlockContainer([new PulseOffice.Documents.Blocks.ParagraphBlock("fnp", "text")])));
        var body = (PulseOffice.Documents.Blocks.ParagraphBlock)document.Body[0];
        body.Marks = [new PulseOffice.Documents.Marks.NoteMark(0, "fn1")];
        document.Notes.FootnoteSettings = document.Notes.FootnoteSettings with { ContinuationForward = "Continued overleaf" };

        var written = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, written).Succeeded);
        written.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(written).Value!;

        Assert.Equal("Continued overleaf", read.Notes.FootnoteSettings.ContinuationForward);
        Assert.Single(read.Notes.All);
    }

    [Theory]
    [InlineData("<w:footnotePr><w:pos w:val=\"beneathText\"/></w:footnotePr>", "", true)]
    [InlineData("", "<w:endnotePr><w:pos w:val=\"sectEnd\"/></w:endnotePr>", true)]
    [InlineData("<w:footnotePr><w:pos w:val=\"pageBottom\"/></w:footnotePr>", "", false)]
    public void Note_placements_other_than_page_bottom_are_declared(string settings, string section, bool declared)
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.AddNewPart<DocumentSettingsPart>().Settings = new W.Settings($"<w:settings {Namespace}>{settings}</w:settings>");
            main.Document = new W.Document($"<w:document {Namespace}><w:body><w:p/><w:sectPr>{section}</w:sectPr></w:body></w:document>");
        }
        stream.Position = 0;

        var read = _composer.Reader(DocxFlavour.Document).Read(stream);

        Assert.Equal(declared, read.Diagnostics.Any(diagnostic => diagnostic.Code == "DOCX272"));
    }
}

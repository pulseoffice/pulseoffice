using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxDatePictureTests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Theory]
    [InlineData("h:mm tt", "h:mm AM/PM")]
    [InlineData("D", "dddd, dd MMMM yyyy")]
    [InlineData("dd/MM/yyyy", "dd/MM/yyyy")]
    public void TheDateFormatIsWrittenAsAWordPictureAndReadBack(string format, string picture)
    {
        var document = Document(TextFieldKind.Time, format);

        var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        Assert.Contains($"\\@ \"{picture}\"", Instructions(stream));
        stream.Position = 0;

        var again = Field(_composer.Reader(DocxFlavour.Document).Read(stream).Value!);
        var expected = format.Length == 1 ? picture : format;
        Assert.Equal(expected, again.FieldDetail?.Format);
    }

    [Theory]
    [InlineData("h:mm am/pm", "h:mm tt")]
    [InlineData("d", "%d")]
    [InlineData("DD 'de' MMMM", "dd 'de' MMMM")]
    public void AWordPictureBecomesAFormatThatShowsTheSame(string picture, string expected)
    {
        Assert.Equal(expected, Field(Read($"<w:p><w:fldSimple w:instr=\" TIME \\@ &quot;{picture}&quot; \"><w:r><w:t>x</w:t></w:r></w:fldSimple></w:p>")).FieldDetail?.Format);
    }

    [Theory]
    [InlineData(" SEQ Figure \\* ARABIC ", TextFieldKind.Sequence)]
    [InlineData(" SEQ Figure \\* ARABIC \\* MERGEFORMAT ", TextFieldKind.Sequence)]
    [InlineData(" SEQ Figure \\* ROMAN ", TextFieldKind.Preserved)]
    [InlineData(" SEQ Figure \\c ", TextFieldKind.Preserved)]
    [InlineData(" SEQ Figure \\h ", TextFieldKind.Preserved)]
    [InlineData(" SEQ Figure \\s 1 ", TextFieldKind.Preserved)]
    [InlineData(" SEQ Figure \\r 5 ", TextFieldKind.Preserved)]
    public void ASequenceWithSwitchesTheModelCannotNumberIsKeptVerbatim(string instruction, TextFieldKind expected)
    {
        var document = Read($"<w:p><w:fldSimple w:instr=\"{instruction}\"><w:r><w:t>IV</w:t></w:r></w:fldSimple></w:p>");
        var field = Field(document);
        Assert.Equal(expected, field.Field);
        if (expected != TextFieldKind.Preserved) return;

        var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        Assert.Contains(instruction, Instructions(stream));
        stream.Position = 0;
        Assert.Equal("IV", Field(_composer.Reader(DocxFlavour.Document).Read(stream).Value!).Text);
    }

    private TextDocument Read(string body)
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            package.AddMainDocumentPart().Document = new W.Document($"<w:document {Namespace}><w:body>{body}</w:body></w:document>");
        stream.Position = 0;
        return _composer.Reader(DocxFlavour.Document).Read(stream).Value!;
    }

    private TextDocument Document(TextFieldKind kind, string format)
    {
        var document = Read("<w:p><w:r><w:t>x</w:t></w:r></w:p>");
        var paragraph = document.Body.Blocks.OfType<ParagraphBlock>().First();
        paragraph.State = paragraph.State with
        {
            Content = paragraph.Content.WithRuns([new TextRun("10:00") { Field = kind, FieldDetail = new FieldDetail { Format = format } }]),
        };
        return document;
    }

    private TextRun Field(TextDocument document) =>
        document.Body.Blocks.OfType<ParagraphBlock>().SelectMany(paragraph => paragraph.Content.Runs).Single(run => run.Field != TextFieldKind.None);

    private string Instructions(Stream stream)
    {
        using var package = WordprocessingDocument.Open(stream, false);
        var document = package.MainDocumentPart!.Document!;
        return string.Join("|", document.Descendants<W.SimpleField>().Select(field => field.Instruction?.Value)
            .Concat(document.Descendants<W.FieldCode>().Select(code => code.Text)));
    }
}

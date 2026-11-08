using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxFourteenthAuditTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private TextDocument WithDeleted(TextRun run)
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Replace([new ParagraphBlock("p1", "Corpo")]);
        var removed = new TextParagraph(string.Empty).WithRuns([run]);
        document.Changes.Add(new Deletion("ct1", "Anna", DateTimeOffset.UnixEpoch, new BlockContainer([new ParagraphBlock("px", removed)])));
        return document;
    }

    [Fact]
    public void Un_campo_nascosto_dentro_una_cancellazione_si_dichiara_in_DOCX()
    {
        var document = WithDeleted(new TextRun(string.Empty) { Field = TextFieldKind.HiddenText, FieldArgument = "Suite" });

        Assert.Contains(_composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses, loss => loss.Code == "DOCX195");
    }

    [Fact]
    public void Il_testo_semplice_dentro_una_cancellazione_non_dichiara_campi()
    {
        var document = WithDeleted(new TextRun("testo"));

        Assert.DoesNotContain(_composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses, loss => loss.Code == "DOCX195");
    }

    private const string IndentedHeader = "/Volumes/Dati/develop/libreoffice-ref/sw/qa/extras/ooxmlexport/data/no-numlevel-but-indented.odt";

    [Fact]
    public void Una_voce_di_elenco_senza_numero_conserva_il_rientro_in_DOCX()
    {
        Assert.SkipWhen(!File.Exists(IndentedHeader), "Reference corpus not available.");
        TextDocument document;
        using (var input = File.OpenRead(IndentedHeader)) document = new PulseOffice.Formats.Odf.Odt.OdtReader().Read(input).Value!;
        var writer = _composer.Writer(DocxFlavour.Document);
        Assert.DoesNotContain(writer.CheckFidelity(document).Losses, loss => loss.Code == "DOCX016");
        using var buffer = new MemoryStream();
        Assert.True(writer.Write(document, buffer).Succeeded);
        buffer.Position = 0;
        using var package = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(buffer, false);
        var properties = package.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>().First().ParagraphProperties!;
        Assert.Equal(0, properties.NumberingProperties?.NumberingId?.Val?.Value);
        Assert.Equal("1418", properties.Indentation?.Left?.Value);
        buffer.Position = 0;
        var back = _composer.Reader(DocxFlavour.Document).Read(buffer).Value!;
        var original = document.Body.Blocks.OfType<ParagraphBlock>().First().Content;
        var header = back.Body.Blocks.OfType<ParagraphBlock>().First().Content;
        Assert.True(header.ListHeader);
        Assert.Equal(original.ListStyleName, header.ListStyleName);
        Assert.Equal(original.BulletLevel, header.BulletLevel);
        Assert.Equal(original.MarginLeft, header.MarginLeft);
    }
}

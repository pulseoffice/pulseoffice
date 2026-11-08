using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class MergeParityTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private static TextDocument Letter()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        var runs = new[]
        {
            new TextRun("Gentile "),
            new TextRun("«Nome»") { Field = TextFieldKind.Database, FieldArgument = "Nome" },
            new TextRun(" "),
            new TextRun("«Cognome di famiglia»") { Field = TextFieldKind.Database, FieldArgument = "Cognome di famiglia" },
            new TextRun(","),
        };
        document.Body.Replace([new ParagraphBlock(document.NextId("p"), new TextParagraph(string.Concat(runs.Select(run => run.Text))) { Runs = runs })]);
        document.MergeSource = new MailMergeSource("Clienti", "Anagrafica", MergeCommandType.Table) { ConnectionId = "c-1" };
        return document;
    }

    private TextDocument Through(TextDocument document, ITextDocumentWriter writer, ITextDocumentReader reader)
    {
        using var stream = new MemoryStream();
        Assert.True(writer.Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = reader.Read(stream);
        Assert.True(read.Succeeded, string.Join("; ", read.Diagnostics.Select(d => d.Message)));
        return read.Value!;
    }

    private string Fields(TextDocument document) =>
        string.Join("|", document.Body.Blocks.OfType<ParagraphBlock>().SelectMany(paragraph => paragraph.Content.Runs)
            .Where(run => run.Field == TextFieldKind.Database).Select(run => run.FieldArgument + "=" + run.Text));

    [Fact]
    public void ICampiDiUnioneAttraversanoOdtEDocx()
    {
        var odt = Through(Letter(), new OdtWriter(), new OdtReader());
        var docx = Through(Letter(), _composer.Writer(DocxFlavour.Document), _composer.Reader(DocxFlavour.Document));

        Assert.Equal("Nome=«Nome»|Cognome di famiglia=«Cognome di famiglia»", Fields(odt));
        if (Environment.GetEnvironmentVariable("MERGE_OUT") is { Length: > 0 } folder)
        {
            using (var file = File.Create(Path.Combine(folder, "unione.odt"))) new OdtWriter().Write(Letter(), file);
            using (var file = File.Create(Path.Combine(folder, "unione.docx"))) _composer.Writer(DocxFlavour.Document).Write(Letter(), file);
        }
        Assert.Equal(Fields(odt), Fields(docx));
    }

    [Fact]
    public void LOrigineDeiDatiRestaInOdtESiDichiaraPersaInDocx()
    {
        var odt = Through(Letter(), new OdtWriter(), new OdtReader());
        var report = _composer.Writer(DocxFlavour.Document).CheckFidelity(Letter());

        Assert.Equal(new MailMergeSource("Clienti", "Anagrafica", MergeCommandType.Table) { ConnectionId = "c-1" }, odt.MergeSource);
        Assert.Contains(report.Losses, loss => loss.Code == "DOCX028");
        Assert.DoesNotContain(new OdtWriter().CheckFidelity(Letter()).Losses, loss => loss.Code.Contains("028", StringComparison.Ordinal));
    }
}

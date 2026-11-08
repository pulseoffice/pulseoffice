using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxSenderFieldTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void ICampiMittenteDiventanoTestoInDocxEDichiaranoLaPerdita()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Replace([new ParagraphBlock("p", new TextParagraph(string.Empty).WithRuns(
            [new TextRun("Da "), new TextRun("Pisa") { Field = TextFieldKind.Sender, FieldArgument = "city" }]))]);
        var writer = _composer.Writer(DocxFlavour.Document);

        Assert.Contains(writer.CheckFidelity(document).Losses, loss => loss.Code == "DOCX128");
        using var stream = new MemoryStream();
        Assert.True(writer.Write(document, stream).Succeeded);
        stream.Position = 0;
        Assert.Equal("Da Pisa", Assert.IsType<ParagraphBlock>(_composer.Reader(DocxFlavour.Document).Read(stream).Value!.Body[0]).Text);
    }
}

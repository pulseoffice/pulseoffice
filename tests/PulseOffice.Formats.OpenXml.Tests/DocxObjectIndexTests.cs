using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Indexes;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxObjectIndexTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void LIndiceDegliOggettiDiventaTestoInDocxEDichiaraLaPerdita()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Replace(
        [
            new SectionBlock("ix", "Table of Objects1", new BlockContainer([new ParagraphBlock("e", "Grafico1\t1", DefaultStyleNames.ObjectIndex1)]))
            {
                Index = new IndexDefinition(IndexKind.Objects),
            },
            new ParagraphBlock("p", "Testo"),
        ]);
        var writer = _composer.Writer(DocxFlavour.Document);

        Assert.Contains(writer.CheckFidelity(document).Losses, loss => loss.Code == "DOCX127");
        using var stream = new MemoryStream();
        Assert.True(writer.Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream).Value!;

        Assert.Contains(new BlockTree().Paragraphs(read.Body), paragraph => paragraph.Text == "Grafico1\t1");
    }
}

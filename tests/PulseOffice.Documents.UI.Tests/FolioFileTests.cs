using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using PulseOffice.Documents.UI.Fonts;
using PulseOffice.Formats;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class FolioFileTests
{
    [Fact]
    public void IlRaccoglitoreTrovaICaratteriDiStiliEPorzioni()
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Body.Replace([new ParagraphBlock("p1", new TextParagraph("Ciao") { Runs = [new TextRun("Ciao") { Font = "Pacifico" }] })]);

        var families = new FolioFontCollector(new StoryCatalog(new BlockTree())).Families(document);

        Assert.Contains("Pacifico", families);
        Assert.Contains("Liberation Serif", families);
        Assert.Contains("Liberation Sans", families);
    }

    [Fact]
    public void IlRegistroDeiTestiScriveERileggeUnOdt()
    {
        var registry = new TextDocumentFormatRegistry([new OdtReader()], [new OdtWriter()], new PulseOffice.Formats.Intake.DocumentIntake());
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Body.Replace([new ParagraphBlock("p1", "Salvato da Folio", DefaultStyleNames.TextBody)]);

        using var stream = new MemoryStream();
        Assert.True(registry.WriterFor("a.odt")!.Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = registry.Read("a.odt", stream);

        Assert.Equal("Salvato da Folio", Assert.IsType<ParagraphBlock>(Assert.Single(read.Value!.Body.Blocks)).Text);
    }
}

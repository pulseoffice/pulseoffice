using PulseOffice.Document.Forms;
using PulseOffice.Documents;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Presentations;
using PulseOffice.Sheets;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class FormDesignModeOoxmlTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private TextDocument RoundTrip(TextDocument document)
    {
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        return _composer.Reader(DocxFlavour.Document).Read(stream).Value!;
    }

    [Fact]
    public void InDocxLApriInModalitaStrutturaDiventaFormsDesign()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create())
        {
            FormSettings = new FormDocumentSettings(OpenInDesignMode: true),
        };

        Assert.DoesNotContain(_composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses, loss => loss.Code == "DOCX013");
        Assert.True(RoundTrip(document).FormSettings.OpenInDesignMode);
        Assert.False(RoundTrip(new TextDocument(new DefaultStyleSheetFactory().Create())).FormSettings.OpenInDesignMode);
    }

    [Fact]
    public void InDocxIlFuocoAutomaticoSiDichiaraPerso()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create())
        {
            FormSettings = new FormDocumentSettings(AutomaticFocus: true),
        };

        Assert.Contains(_composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses, loss => loss.Code == "DOCX013");
    }

    [Fact]
    public void InPptxLeImpostazioniDeiModuliSiDichiaranoPerse()
    {
        var themes = new ThemeCatalog();
        var theme = themes.Find("Light")!;
        var master = new MasterFactory().CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, FormSettings = new FormDocumentSettings(OpenInDesignMode: true) };

        Assert.Contains(new PptxWriter().CheckFidelity(deck).Losses, loss => loss.Code == "PPTX027");
        Assert.DoesNotContain(new PptxWriter().CheckFidelity(new Presentation { Theme = theme, Master = master }).Losses, loss => loss.Code == "PPTX027");
    }

    [Fact]
    public void InXlsxLeImpostazioniDeiModuliSiDichiaranoPerse()
    {
        var workbook = new Workbook { FormSettings = new FormDocumentSettings(OpenInDesignMode: true) };
        workbook.AddSheet("Foglio1");

        Assert.Contains(new XlsxSpreadsheetWriter().CheckFidelity(workbook).Losses, loss => loss.Code == "XLSX026");
    }
}

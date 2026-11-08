using PulseOffice.Document.Forms;
using PulseOffice.Documents;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Presentations;
using PulseOffice.Sheets;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class FormDesignModeRoundTripTests
{
    private readonly FormDocumentSettings _settings = new(OpenInDesignMode: true, AutomaticFocus: true);

    [Fact]
    public void UnFoglioSenzaControlliConservaLApriInModalitaStruttura()
    {
        var workbook = new Workbook { FormSettings = _settings };
        workbook.AddSheet("Foglio1");

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;

        Assert.Equal(_settings, new OdsReader().Read(stream).Value!.FormSettings);
    }

    [Fact]
    public void UnaPresentazioneSenzaControlliConservaLApriInModalitaStruttura()
    {
        var themes = new ThemeCatalog();
        var masters = new MasterFactory();
        var theme = themes.Find("Light")!;
        var master = masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, FormSettings = _settings };
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), master, SlideLayout.TitleSlide));
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank));

        using var stream = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, stream).Succeeded);
        stream.Position = 0;

        Assert.Equal(_settings, new OdpReader(themes, masters).Read(stream).Value!.FormSettings);
    }

    [Fact]
    public void UnTestoSenzaControlliConservaLApriInModalitaStruttura()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create()) { FormSettings = _settings };

        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;

        Assert.Equal(_settings, new OdtReader().Read(stream).Value!.FormSettings);
    }

    [Fact]
    public void LeImpostazioniPredefiniteNonScrivonoModuliVuoti()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Foglio1");

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;

        using var archive = new System.IO.Compression.ZipArchive(stream);
        using var reader = new StreamReader(archive.GetEntry("content.xml")!.Open());
        Assert.DoesNotContain("office:forms", reader.ReadToEnd(), StringComparison.Ordinal);
    }
}

using PulseOffice.Presentations;
using PulseOffice.Sheets;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class DocumentPropertiesRoundTripTests
{
    [Fact]
    public void LeProprietaSopravvivonoAlGiro()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Properties.Title = "Rendiconto 2026";
        workbook.Properties.Author = "Alice";
        workbook.Properties.Keywords.Add("vendite");
        workbook.Properties.Keywords.Add("2026");

        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);

        stream.Position = 0;
        var risultato = new XlsxReader().Read(stream);

        Assert.True(risultato.Succeeded);

        var proprieta = risultato.Value!.Properties;

        Assert.Equal("Rendiconto 2026", proprieta.Title);
        Assert.Equal("Alice", proprieta.Author);
        Assert.Equal(["vendite", "2026"], proprieta.Keywords);
    }

    [Fact]
    public void ICampiPersonalizzatiSopravvivonoAncheInXlsx()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Properties.Custom["Reparto"] = "Vendite";
        workbook.Properties.Custom["Revisione"] = "3";

        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;

        var custom = new XlsxReader().Read(stream).Value!.Properties.Custom;

        Assert.Equal("Vendite", custom["Reparto"]);
        Assert.Equal("3", custom["Revisione"]);
    }

    [Fact]
    public void LeProprietaDiUnaPresentazioneSopravvivonoInPptx()
    {
        var themes = new ThemeCatalog();
        var masters = new MasterFactory();
        var theme = themes.Find("Light")!;
        var master = masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), master, SlideLayout.TitleSlide));
        deck.Properties.Title = "Relazione trimestrale";
        deck.Properties.Language = "de-DE";
        deck.Properties.Author = "Alice";
        deck.Properties.Keywords.Add("vendite");
        deck.Properties.Custom["Reparto"] = "Vendite";

        using var stream = new MemoryStream();
        new PptxWriter().Write(deck, stream);
        stream.Position = 0;

        var back = new PptxReader(themes, masters).Read(stream).Value!.Properties;

        Assert.Equal("de-DE", back.Language);

        Assert.Equal("Relazione trimestrale", back.Title);
        Assert.Equal("Alice", back.Author);
        Assert.Equal(["vendite"], back.Keywords);
        Assert.Equal("Vendite", back.Custom["Reparto"]);
    }

    [Fact]
    public void LeVersioniDiUnaPresentazioneSonoUnaPerditaDichiarataInPptx()
    {
        var themes = new ThemeCatalog();
        var masters = new MasterFactory();
        var theme = themes.Find("Light")!;
        var deck = new Presentation { Theme = theme, Master = masters.CreateStandard(theme) };
        deck.Versions.Add([1, 2, 3], "Alice", "Bozza");

        var report = new PptxWriter().CheckFidelity(deck);

        Assert.Contains(report.Losses, loss => loss.Code == "PPTX014");
    }
}

using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdpPropertiesRoundTripTests
{
    [Fact]
    public void LeProprietaDiUnaPresentazioneSopravvivonoInOdp()
    {
        var themes = new ThemeCatalog();
        var masters = new MasterFactory();
        var theme = themes.Find("Light")!;
        var master = masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        deck.AddExisting(new SlideFactory().Create(deck.NextId(), master, SlideLayout.TitleSlide));
        deck.Properties.Title = "Relazione trimestrale";
        deck.Properties.Subject = "Vendite";
        deck.Properties.Author = "Alice";
        deck.Properties.Keywords.Add("vendite");
        deck.Properties.Keywords.Add("2026");
        deck.Properties.Custom["Reparto"] = "Vendite";
        deck.Properties.Language = "de-DE";

        using var stream = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, stream).Succeeded);
        stream.Position = 0;

        var back = new OdpReader(themes, masters).Read(stream).Value!.Properties;

        Assert.Equal("Relazione trimestrale", back.Title);
        Assert.Equal("Vendite", back.Subject);
        Assert.Equal("Alice", back.Author);
        Assert.Equal(["vendite", "2026"], back.Keywords);
        Assert.Equal("Vendite", back.Custom["Reparto"]);
        Assert.Equal("de-DE", back.Language);
    }
}

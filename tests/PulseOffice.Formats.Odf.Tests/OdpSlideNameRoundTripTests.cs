using PulseOffice.Presentations;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdpSlideNameRoundTripTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private Presentation RoundTrip(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        var result = new OdpReader(_themes, _masters).Read(buffer);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private Presentation Deck(int count)
    {
        var theme = _themes.Find("Light")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        for (var i = 0; i < count; i++)
            deck.AddExisting(_slides.Create(deck.NextId(), master, SlideLayout.TitleAndContent));
        return deck;
    }

    [Fact]
    public void IlNomeDellaDiapositivaSopravvive()
    {
        var deck = Deck(2);
        deck.Slides[1].Name = "Conclusioni";

        var back = RoundTrip(deck);

        Assert.Null(back.Slides[0].Name);
        Assert.Equal("Conclusioni", back.Slides[1].Name);
    }

    [Fact]
    public void IRiferimentiAllaDiapositivaRinominataRestanoValidi()
    {
        var deck = Deck(3);
        var renamed = deck.Slides[2];
        renamed.Name = "Domande";

        deck.AddCustomShow(new CustomShow("Breve", [deck.Slides[0].Id, renamed.Id]));
        var title = deck.Slides[0].Placeholder(PlaceholderRole.Title)!;
        deck.Slides[0].SetInteraction(new InteractiveAction
        {
            TargetId = title.Id,
            Kind = InteractionKind.GoToSlide,
            Reference = renamed.Id,
        });
        deck.Setup = deck.Setup with { FromSlide = 3 };

        var back = RoundTrip(deck);
        var target = back.Slides[2];

        Assert.Equal("Domande", target.Name);
        Assert.Contains(target.Id, back.CustomShows.Single().SlideIds);
        Assert.Equal(target.Id, back.Slides[0].Interactions.Single().Reference);
        Assert.Equal(3, back.Setup.FromSlide);
    }
}

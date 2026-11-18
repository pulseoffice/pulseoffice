
using PulseOffice.Formats.Odf;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round231OdpSectionTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    [Fact]
    public void Empty_sections_and_unsectioned_leading_slides_survive_a_round_trip()
    {
        var master = _masters.CreateStandard(_themes.Default);
        var deck = new Presentation { Master = master, Theme = _themes.Default };
        for (var i = 0; i < 2; i++) deck.AddExisting(new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank));
        deck.Slides[1].SectionName = "Body";
        deck.EmptySections.Add(new EmptySection("Before body", null, deck.Slides[1].Id));
        deck.EmptySections.Add(new EmptySection("Appendix", null, null));

        using var stream = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, stream).Succeeded);
        stream.Position = 0;
        var read = new OdpReader(_themes, _masters).Read(stream).Value!;
        Assert.Null(read.Slides[0].SectionName);
        Assert.Equal("Body", read.Slides[1].SectionName);
        Assert.Equal(new[] { "Before body", "Appendix" }, read.EmptySections.Select(section => section.Name));
        Assert.Equal(read.Slides[1].Id, read.EmptySections[0].BeforeSlideId);
        Assert.Null(read.EmptySections[1].BeforeSlideId);
    }
}

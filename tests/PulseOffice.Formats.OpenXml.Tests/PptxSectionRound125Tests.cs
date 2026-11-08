using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class PptxSectionRound125Tests
{
    private static readonly XNamespace P14 = "http://schemas.microsoft.com/office/powerpoint/2010/main";
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    private Presentation Deck()
    {
        var theme = _themes.Default;
        var deck = new Presentation { Theme = theme, Master = _masters.CreateStandard(theme) };
        for (var index = 0; index < 3; index++) deck.AddExisting(new SlideFactory().Create(deck.NextId(), deck.Master, SlideLayout.Blank));
        deck.Slides[0].SectionName = "Intro";
        deck.Slides[2].SectionName = "Intro";
        return deck;
    }

    private static List<string> SectionIds(Presentation deck)
    {
        using var stream = new MemoryStream();
        new PptxWriter().Write(deck, stream);
        stream.Position = 0;
        using var package = PresentationDocument.Open(stream, false);
        var root = XDocument.Parse(package.PresentationPart!.Presentation!.OuterXml).Root!;
        return [.. root.Descendants(P14 + "section").Select(section => (string)section.Attribute("id")!)];
    }

    [Fact]
    public void Two_saves_of_the_same_deck_write_the_same_section_ids()
    {
        var deck = Deck();

        var first = SectionIds(deck);
        var second = SectionIds(deck);

        Assert.Equal(2, first.Count);
        Assert.Equal(first, second);
        Assert.Equal(2, first.Distinct().Count());
        Assert.All(first, id => Assert.True(Guid.TryParse(id, out _)));
    }

    [Fact]
    public void A_section_id_read_from_a_file_is_written_back()
    {
        var deck = Deck();
        deck.Slides[0].SectionId = "{0F1E2D3C-4B5A-6978-8796-A5B4C3D2E1F0}";
        using var stream = new MemoryStream();
        new PptxWriter().Write(deck, stream);
        stream.Position = 0;
        var read = new PptxReader(new ThemeCatalog(), new MasterFactory()).Read(stream);
        Assert.True(read.Succeeded);

        Assert.Equal("{0F1E2D3C-4B5A-6978-8796-A5B4C3D2E1F0}", SectionIds(read.Value!)[0]);
    }
}

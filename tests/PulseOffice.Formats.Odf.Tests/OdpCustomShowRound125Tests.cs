using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdpCustomShowRound125Tests
{
    private static readonly XNamespace Presentation = "urn:oasis:names:tc:opendocument:xmlns:presentation:1.0";
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private PulseOffice.Presentations.Presentation Deck()
    {
        var theme = _themes.Default;
        var deck = new PulseOffice.Presentations.Presentation { Theme = theme, Master = _masters.CreateStandard(theme) };
        foreach (var name in new[] { "A,B", "A", "B", "C" })
        {
            var slide = _slides.Create(deck.NextId(), deck.Master, SlideLayout.Blank);
            slide.Name = name;
            deck.AddExisting(slide);
        }
        deck.AddCustomShow(new CustomShow("Short", [deck.Slides[0].Id, deck.Slides[3].Id]));
        return deck;
    }

    [Fact]
    public void A_slide_whose_name_contains_a_comma_stays_in_its_custom_show()
    {
        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(Deck(), buffer).Succeeded);
        buffer.Position = 0;
        var back = new OdpReader(_themes, _masters).Read(buffer).Value!;

        Assert.Equal([back.Slides[0].Id, back.Slides[3].Id], back.CustomShows.Single().SlideIds);
    }

    [Fact]
    public void The_standard_page_list_never_names_other_slides_by_splitting_a_comma()
    {
        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(Deck(), buffer).Succeeded);
        buffer.Position = 0;
        using var archive = new ZipArchive(buffer, ZipArchiveMode.Read);
        using var content = archive.GetEntry("content.xml")!.Open();
        var pages = (string?)XDocument.Load(content).Descendants(Presentation + "show").Single().Attribute(Presentation + "pages");

        Assert.Equal(["C"], pages!.Split(','));
    }

    [Fact]
    public void A_custom_show_slide_whose_name_contains_a_comma_is_declared()
    {
        Assert.Contains("ODF286", new OdpWriter().CheckFidelity(Deck()).Losses.Select(loss => loss.Code));
    }
}

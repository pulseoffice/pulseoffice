using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Drawing.Scenes;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class PptxPresentationRound123Tests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    private Presentation Deck(int slides)
    {
        var theme = _themes.Default;
        var deck = new Presentation { Theme = theme, Master = _masters.CreateStandard(theme) };
        for (var index = 0; index < slides; index++) deck.AddExisting(new SlideFactory().Create(deck.NextId(), deck.Master, SlideLayout.Blank));
        return deck;
    }

    [Fact]
    public void A_three_dimensional_scene_is_not_declared_as_a_dropped_form_control()
    {
        var deck = Deck(1);
        deck.Slides[0].Add(new ObjectElement
        {
            Id = "scene",
            Bounds = new DocumentRect(new Twip(100), new Twip(100), new Twip(2000), new Twip(2000)),
            Content = new Scene3DObject { Id = "scene", Markup = "<dr3d:scene xmlns:dr3d=\"urn:oasis:names:tc:opendocument:xmlns:dr3d:1.0\"/>", Styles = [] },
        });

        var codes = new PptxWriter().CheckFidelity(deck).Losses.Select(loss => loss.Code).ToList();

        Assert.Contains("ODP102", codes);
        Assert.DoesNotContain("PPTX004", codes);
    }

    [Theory]
    [InlineData(5, 0)]
    [InlineData(3, 1)]
    [InlineData(0, 9)]
    public void The_show_range_written_is_always_a_valid_range_of_existing_slides(int from, int to)
    {
        var deck = Deck(2);
        deck.Setup = deck.Setup with { FromSlide = from, ToSlide = to };

        using var stream = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, stream).Succeeded);
        stream.Position = 0;
        using var document = PresentationDocument.Open(stream, false);
        var show = document.PresentationPart!.PresentationPropertiesPart!.PresentationProperties!.GetFirstChild<P.ShowProperties>()!;

        if (show.GetFirstChild<P.SlideRange>() is { } range)
        {
            Assert.InRange(range.Start!.Value, 1u, 2u);
            Assert.InRange(range.End!.Value, range.Start!.Value, 2u);
        }
        else Assert.NotNull(show.GetFirstChild<P.SlideAll>());
    }
}

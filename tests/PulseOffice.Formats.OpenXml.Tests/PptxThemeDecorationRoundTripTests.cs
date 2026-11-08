using PulseOffice.Drawing.Shapes;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class PptxThemeDecorationRoundTripTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private Presentation RoundTrip(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        buffer.Position = 0;
        var result = new PptxReader(_themes, _masters).Read(buffer);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private Presentation Deck(string name, Func<SlideMaster, SlideMaster>? edit = null)
    {
        var theme = _themes.Find(name)!;
        var master = _masters.CreateStandard(theme);
        if (edit is not null) master = edit(master);
        var deck = new Presentation { Theme = theme, Master = master };
        deck.AddExisting(_slides.Create(deck.NextId(), master, SlideLayout.TitleSlide));
        deck.AddExisting(_slides.Create(deck.NextId(), master, SlideLayout.TitleAndContent));
        deck.AddExisting(_slides.Create(deck.NextId(), master, SlideLayout.SectionHeader));
        return deck;
    }

    [Theory]
    [InlineData("Meridian")]
    [InlineData("Prism")]
    [InlineData("Aurora")]
    [InlineData("Ember")]
    [InlineData("Horizon")]
    [InlineData("Mosaic")]
    public void LaGraficaDelMasterEDeiLayoutTorna(string name)
    {
        var deck = Deck(name);
        var back = RoundTrip(deck);

        AssertSame(deck.Master.Objects, back.Master.Objects);
        foreach (var kind in new[] { SlideLayout.TitleSlide, SlideLayout.SectionHeader })
        {
            Assert.True(back.Master.Template(kind).HideMasterObjects);
            AssertSame(deck.Master.Template(kind).Objects, back.Master.Template(kind).Objects);
        }

        Assert.Empty(back.Master.Template(SlideLayout.TitleAndContent).Objects);
        Assert.Single(back.Masters);
        Assert.All(back.Slides, slide => Assert.Null(slide.MasterId));
        Assert.Equal(name, back.Theme.Name);
    }

    [Fact]
    public void LaGraficaModificataSiLeggeDalFileENonSiRigenera()
    {
        var deck = Deck("Meridian", master => master with { Objects = [master.Objects[0]] });
        var back = RoundTrip(deck);
        Assert.Single(back.Master.Objects);
    }

    [Fact]
    public void UnTemaSenzaGraficaNonNeAggiunge()
    {
        var back = RoundTrip(Deck("Light"));
        Assert.Empty(back.Master.Objects);
        Assert.All(back.Master.Layouts, layout => Assert.Empty(layout.Objects));
    }

    private void AssertSame(IReadOnlyList<SlideElement> expected, IReadOnlyList<SlideElement> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index++)
        {
            var want = (ShapeObject)((ObjectElement)expected[index]).Content;
            var got = (ShapeObject)((ObjectElement)actual[index]).Content;
            Assert.Equal(want.Shape, got.Shape);
            Assert.Equal(want.Style.Fill.Kind, got.Style.Fill.Kind);
            if (want.Style.Fill.Kind == ShapeFillKind.Gradient)
            {
                Assert.Equal(want.Style.Fill.Gradient.StartHex, got.Style.Fill.Gradient.StartHex, ignoreCase: true);
                Assert.Equal(want.Style.Fill.Gradient.EndHex, got.Style.Fill.Gradient.EndHex, ignoreCase: true);
            }
            else
            {
                Assert.Equal(want.Style.Fill.ColorHex, got.Style.Fill.ColorHex, ignoreCase: true);
            }

            Assert.Equal(want.Style.Fill.Opacity, got.Style.Fill.Opacity, 0.02);
            Assert.Equal(want.Style.Line.IsVisible, got.Style.Line.IsVisible);
            Assert.Equal(want.FlipHorizontal, got.FlipHorizontal);
            Assert.Equal(want.FlipVertical, got.FlipVertical);
            Assert.Equal((double)expected[index].Bounds.Left.Value, actual[index].Bounds.Left.Value, tolerance: 20);
            Assert.Equal((double)expected[index].Bounds.Top.Value, actual[index].Bounds.Top.Value, tolerance: 20);
            Assert.Equal((double)expected[index].Bounds.Width.Value, actual[index].Bounds.Width.Value, tolerance: 20);
            Assert.Equal((double)expected[index].Bounds.Height.Value, actual[index].Bounds.Height.Value, tolerance: 20);
        }
    }

    [Fact]
    public void DecorationsOfASecondaryMasterSurvive()
    {
        var deck = Deck("Light");
        var second = _masters.CreateStandard(_themes.Find("Meridian")!) with { Id = "master2", Name = "Secondo" };
        deck.AddMaster(second);
        var titled = _slides.Create(deck.NextId(), second, SlideLayout.TitleSlide);
        titled.MasterId = "master2";
        deck.AddExisting(titled);
        var plain = _slides.Create(deck.NextId(), second, SlideLayout.TitleAndContent);
        plain.MasterId = "master2";
        deck.AddExisting(plain);

        var back = RoundTrip(deck);
        Assert.Equal(2, back.Masters.Count);
        var master = back.MasterOf(back.Slides[3]);
        Assert.Same(master, back.MasterOf(back.Slides[4]));
        Assert.Equal("Meridian", master.Theme.Name);
        Assert.Equal(second.Objects.Count, master.Objects.Count);
        var layout = master.Template(SlideLayout.TitleSlide);
        var expected = second.Template(SlideLayout.TitleSlide);
        Assert.Equal(expected.HideMasterObjects, layout.HideMasterObjects);
        Assert.Equal(expected.Objects.Count, layout.Objects.Count);
        Assert.Empty(back.Master.Objects);
    }
}

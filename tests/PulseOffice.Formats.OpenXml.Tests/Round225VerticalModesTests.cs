using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round225VerticalModesTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();

    private Presentation Deck(TextFlow flow, string text)
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.Blank);
        var style = ShapeStyle.Default with { Text = ShapeTextStyle.Default with { Layout = new TextLayout { Flow = flow } } };
        slide.Add(new ObjectElement { Id = "a", Bounds = new DocumentRect(new Twip(1000), new Twip(1000), new Twip(2000), new Twip(1000)), Content = new ShapeObject { Id = "a", Shape = ShapeKind.Rectangle, Text = text, Style = style } });
        deck.AddExisting(slide);
        return deck;
    }

    private TextFlow Flow(Presentation deck) =>
        deck.Slides[0].Elements.OfType<ObjectElement>().Select(element => element.Content).OfType<ShapeObject>().Single().Style.Text.Layout.Flow;

    [Theory]
    [InlineData(TextFlow.EastAsianVertical)]
    [InlineData(TextFlow.MongolianVertical)]
    [InlineData(TextFlow.StackedRightToLeft)]
    public void Vertical_modes_survive_PPTX_and_ODP(TextFlow flow)
    {
        using var pptx = new MemoryStream();
        Assert.True(new PptxWriter().Write(Deck(flow, "x"), pptx).Succeeded);
        pptx.Position = 0;
        var fromPptx = new PptxReader(_themes, _masters).Read(pptx);
        Assert.Equal(flow, Flow(fromPptx.Value!));
        Assert.DoesNotContain(fromPptx.Diagnostics, diagnostic => diagnostic.Code == "PPTX226");

        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(Deck(flow, "x"), odp).Succeeded);
        odp.Position = 0;
        var fromOdp = new OdpReader(_themes, _masters).Read(odp);
        Assert.Equal(flow, Flow(fromOdp.Value!));
        Assert.DoesNotContain(fromOdp.Diagnostics, diagnostic => diagnostic.Code == "ODP217");
    }

    [Fact]
    public void Only_East_Asian_characters_in_East_Asian_vertical_text_are_declared()
    {
        using var pptx = new MemoryStream();
        Assert.True(new PptxWriter().Write(Deck(TextFlow.EastAsianVertical, "縦書き"), pptx).Succeeded);
        pptx.Position = 0;
        Assert.Contains(new PptxReader(_themes, _masters).Read(pptx).Diagnostics, diagnostic => diagnostic.Code == "PPTX226");

        using var odp = new MemoryStream();
        Assert.True(new OdpWriter().Write(Deck(TextFlow.EastAsianVertical, "縦書き"), odp).Succeeded);
        odp.Position = 0;
        Assert.Contains(new OdpReader(_themes, _masters).Read(odp).Diagnostics, diagnostic => diagnostic.Code == "ODP217");
    }

    [Fact]
    public void East_Asian_detection_ignores_Latin_and_Mongolian()
    {
        var upright = new EastAsianUprightText();
        Assert.True(upright.Contains("abc 漢字"));
        Assert.True(upright.Contains("한국어"));
        Assert.False(upright.Contains("abc"));
        Assert.False(upright.Contains("ᠮᠣᠩᠭᠣᠯ"));
    }
}

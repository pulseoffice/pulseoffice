using PulseOffice.Formats;
using PulseOffice.Formats.Odf;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Presentations;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class OdpPptxParityTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private Presentation Sample()
    {
        var theme = _themes.Find("Sand")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Standard };

        var s1 = _slides.Create(deck.NextId(), master, SlideLayout.TitleSlide);
        var t1 = s1.Placeholder(PlaceholderRole.Title)!;
        s1.Replace(t1 with { Text = t1.Text.WithPlainText("Piano annuale") });
        deck.AddExisting(s1);

        var s2 = _slides.Create(deck.NextId(), master, SlideLayout.TitleAndContent);
        var t2 = s2.Placeholder(PlaceholderRole.Title)!;
        s2.Replace(t2 with { Text = t2.Text.WithPlainText("Obiettivi") });
        var b2 = s2.Placeholder(PlaceholderRole.Body)!;
        s2.Replace(b2 with { Text = b2.Text.WithPlainText("Crescita\nQualità\nSostenibilità") });
        deck.AddExisting(s2);

        return deck;
    }

    private Presentation ViaOdp(Presentation deck)
    {
        using var buffer = new MemoryStream();
        new OdpWriter().Write(deck, buffer);
        buffer.Position = 0;
        return new OdpReader(_themes, _masters).Read(buffer).Value!;
    }

    private Presentation ViaPptx(Presentation deck)
    {
        using var buffer = new MemoryStream();
        new PptxWriter().Write(deck, buffer);
        buffer.Position = 0;
        return new PptxReader(_themes, _masters).Read(buffer).Value!;
    }

    [Fact]
    public void BothFormatsPreserveSlideTextAndStructure()
    {
        var deck = Sample();
        var odp = ViaOdp(deck);
        var pptx = ViaPptx(deck);

        foreach (var back in new[] { odp, pptx })
        {
            Assert.Equal(2, back.Slides.Count);
            Assert.Equal("Piano annuale", back.Slides[0].Placeholder(PlaceholderRole.Title)!.Text.PlainText);
            Assert.Equal("Obiettivi", back.Slides[1].Placeholder(PlaceholderRole.Title)!.Text.PlainText);

            var body = back.Slides[1].Placeholder(PlaceholderRole.Body)!.Text;
            Assert.Equal(3, body.Paragraphs.Count);
            Assert.Equal("Sostenibilità", body.Paragraphs[2].Text);
        }
    }

    [Fact]
    public void BothFormatsPreserveSlideSize()
    {
        var deck = Sample();

        Assert.Equal(SlideSize.Standard.Width.Value, ViaOdp(deck).Size.Width.Value, tolerance: 20);
        Assert.Equal(SlideSize.Standard.Width.Value, ViaPptx(deck).Size.Width.Value, tolerance: 20);
    }

    [Fact]
    public void BothFormatsPreserveTitleFormatting()
    {
        var deck = Sample();

        foreach (var back in new[] { ViaOdp(deck), ViaPptx(deck) })
        {
            var title = back.Slides[0].Placeholder(PlaceholderRole.Title)!.Text.Paragraphs[0];
            Assert.True(title.Bold);
            Assert.Equal(40, title.FontSize);
        }
    }
}

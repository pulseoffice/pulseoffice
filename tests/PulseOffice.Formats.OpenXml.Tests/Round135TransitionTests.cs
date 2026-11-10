using System.IO.Compression;
using PulseOffice.Formats.Odf;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round135TransitionTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private Presentation Deck(SlideTransition transition)
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        deck.AddExisting(_slides.Create(deck.NextId(), master, SlideLayout.TitleAndContent));
        deck.Slides[0].Transition = transition;
        return deck;
    }

    private byte[] Pptx(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        return buffer.ToArray();
    }

    private string SlideXml(byte[] pptx)
    {
        using var zip = new ZipArchive(new MemoryStream(pptx));
        using var reader = new StreamReader(zip.GetEntry("ppt/slides/slide1.xml")!.Open());
        return reader.ReadToEnd();
    }

    private string Fallback(string xml) => xml[xml.IndexOf("Fallback", StringComparison.Ordinal)..];

    private Presentation ReadPptx(byte[] bytes) => new PptxReader(_themes, _masters).Read(new MemoryStream(bytes)).Value!;

    private Presentation RoundTripOdp(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        return new OdpReader(_themes, _masters).Read(new MemoryStream(buffer.ToArray())).Value!;
    }

    [Fact]
    public void MorphIsWrittenAsTheNativeExtensionWithAPlainFadeFallback()
    {
        var xml = SlideXml(Pptx(Deck(new SlideTransition { Kind = TransitionKind.Morph, DurationMs = 2000 })));
        Assert.Contains("Requires=\"p159\"", xml);
        Assert.Contains("p159:morph", xml);
        Assert.Contains("p:fade", Fallback(xml));
        Assert.DoesNotContain("thruBlk", Fallback(xml));
        Assert.DoesNotContain("morph", Fallback(xml));
    }

    [Fact]
    public void MorphSurvivesPptx()
    {
        var read = ReadPptx(Pptx(Deck(new SlideTransition { Kind = TransitionKind.Morph, DurationMs = 2000 }))).Slides[0].Transition;
        Assert.Equal(TransitionKind.Morph, read.Kind);
        Assert.Equal(2000, read.DurationMs);
    }

    [Theory]
    [InlineData(TransitionKind.Cut)]
    [InlineData(TransitionKind.Zoom)]
    [InlineData(TransitionKind.Morph)]
    public void KindsWithoutSmilCounterpartSurviveOdp(TransitionKind kind) =>
        Assert.Equal(kind, RoundTripOdp(Deck(new SlideTransition { Kind = kind })).Slides[0].Transition.Kind);

    [Fact]
    public void NonPresentationNativeEffectsStayOutOfTheFallback()
    {
        var native = "<p15:prstTrans xmlns:p15=\"http://schemas.microsoft.com/office/powerpoint/2012/main\" prst=\"curtains\"/>";
        var transition = new SlideTransition
        {
            Kind = TransitionKind.Fade,
            Native = new NativeTransition(NativeTransition.Ooxml, native, TransitionKind.Fade, TransitionDirection.None),
        };
        var xml = SlideXml(Pptx(Deck(transition)));
        Assert.Contains("Requires=\"p15\"", xml);
        Assert.DoesNotContain("prstTrans", Fallback(xml));
    }
}

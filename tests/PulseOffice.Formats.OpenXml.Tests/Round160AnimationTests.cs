using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Formats.Odf;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round160AnimationTests
{
    private static readonly XNamespace Anim = "urn:oasis:names:tc:opendocument:xmlns:animation:1.0";
    private static readonly XNamespace Draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
    private static readonly XNamespace Xlink = "http://www.w3.org/1999/xlink";

    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private Presentation Deck()
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        deck.AddExisting(_slides.Create(deck.NextId(), master, SlideLayout.TitleAndContent));
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

    private Presentation ReadPptx(byte[] bytes) => new PptxReader(_themes, _masters).Read(new MemoryStream(bytes)).Value!;

    [Fact]
    public void SamePresetTwiceOnOneShapeInOneClickSurvivesPptx()
    {
        var deck = Deck();
        var slide = deck.Slides[0];
        var target = slide.InZOrder().First().Id;
        slide.AddAnimation(new AnimationEffect { Id = "a1", TargetId = target, Preset = "fade" });
        slide.AddAnimation(new AnimationEffect { Id = "a2", TargetId = target, Preset = "fade", Trigger = AnimationTrigger.WithPrevious, DelayMs = 1000 });

        var read = ReadPptx(Pptx(deck)).Slides[0].Animations;

        Assert.Equal(2, read.Count);
        Assert.Equal(1000, read[1].DelayMs);
    }

    [Fact]
    public void ParagraphBuildIsDeclaredInThePptxBuildList()
    {
        var deck = Deck();
        var slide = deck.Slides[0];
        var target = slide.InZOrder().OfType<TextElement>().First().Id;
        slide.AddAnimation(new AnimationEffect { Id = "a1", TargetId = target, Paragraph = 0, Preset = "fade" });

        var xml = SlideXml(Pptx(deck));

        Assert.Contains("<p:bldLst><p:bldP", xml);
        Assert.Contains("build=\"p\"", xml);
    }

    [Fact]
    public void SoundAddedToANativelyCarriedOdfEffectIsWritten()
    {
        var mapper = new OdfAnimationMapper();
        var slide = Deck().Slides[0];
        var target = slide.InZOrder().First().Id;
        slide.AddAnimation(new AnimationEffect { Id = "a1", TargetId = target, Preset = "fade" });
        var read = mapper.Read(new XElement(Draw + "page", mapper.Build(slide, "page1")), slide.Id).Single();
        Assert.NotNull(read.Native);

        slide.RemoveAnimation("a1");
        slide.AddAnimation(read with { SoundPath = "Media/bell.wav" });
        var written = mapper.Build(slide, "page1")!;

        Assert.Equal("Media/bell.wav", (string?)written.Descendants(Anim + "audio").Single().Attribute(Xlink + "href"));
    }
}

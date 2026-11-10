using System.IO.Compression;
using PulseOffice.Formats.Odf;
using PulseOffice.Presentations;
using Xunit;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round137TriggerTests
{
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

    private Presentation Triggered(bool rewind = false)
    {
        var deck = Deck();
        var slide = deck.Slides[0];
        var ids = slide.InZOrder().Select(element => element.Id).ToList();
        slide.AddAnimation(new AnimationEffect { Id = "main1", TargetId = ids[0], Preset = "fade" });
        slide.AddAnimation(new AnimationEffect { Id = "trig1", TargetId = ids[1], Preset = "fade", TriggerShapeId = ids[0], Rewind = rewind, DurationMs = 700 });
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

    private Presentation RoundTripOdp(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, buffer).Succeeded);
        return new OdpReader(_themes, _masters).Read(new MemoryStream(buffer.ToArray())).Value!;
    }

    [Fact]
    public void TriggeredEffectIsWrittenAsAnInteractiveSequence()
    {
        var xml = SlideXml(Pptx(Triggered()));
        Assert.Contains("nodeType=\"interactiveSeq\"", xml);
        Assert.Contains("evt=\"onClick\"", xml);
        Assert.Contains("nodeType=\"mainSeq\"", xml);
    }

    [Fact]
    public void TriggerSurvivesPptx()
    {
        var deck = Triggered(rewind: true);
        var read = ReadPptx(Pptx(deck)).Slides[0].Animations;
        var trigger = deck.Slides[0].InZOrder().First().Id;
        Assert.Equal(2, read.Count);
        Assert.Null(read[0].TriggerShapeId);
        Assert.False(read[0].Rewind);
        Assert.NotNull(read[1].TriggerShapeId);
        Assert.Equal(read[0].TargetId, read[1].TriggerShapeId);
        Assert.True(read[1].Rewind);
        Assert.NotEqual(trigger, string.Empty);
    }

    [Fact]
    public void TriggerSurvivesOdp()
    {
        var deck = Triggered(rewind: true);
        var read = RoundTripOdp(deck).Slides[0].Animations;
        Assert.Equal(2, read.Count);
        Assert.Null(read[0].TriggerShapeId);
        Assert.Equal(read[0].TargetId, read[1].TriggerShapeId);
        Assert.True(read[1].Rewind);
        Assert.False(read[0].Rewind);
    }

    [Fact]
    public void TriggerSurvivesOdpThenPptx()
    {
        var read = ReadPptx(Pptx(RoundTripOdp(Triggered()))).Slides[0].Animations;
        Assert.Equal(read[0].TargetId, read[1].TriggerShapeId);
    }

    [Fact]
    public void InteractiveSequenceFromAnotherWriterIsReadWithItsTrigger()
    {
        const string xml = "<p:timing xmlns:p=\"http://schemas.openxmlformats.org/presentationml/2006/main\"><p:tnLst><p:par><p:cTn id=\"1\" dur=\"indefinite\" restart=\"never\" nodeType=\"tmRoot\"><p:childTnLst>"
            + "<p:seq concurrent=\"1\" nextAc=\"seek\"><p:cTn id=\"2\" restart=\"whenNotActive\" fill=\"hold\" evtFilter=\"cancelBubble\" nodeType=\"interactiveSeq\">"
            + "<p:stCondLst><p:cond evt=\"onClick\" delay=\"0\"><p:tgtEl><p:spTgt spid=\"3\"/></p:tgtEl></p:cond></p:stCondLst>"
            + "<p:childTnLst><p:par><p:cTn id=\"3\" fill=\"hold\"><p:stCondLst><p:cond delay=\"0\"/></p:stCondLst><p:childTnLst>"
            + "<p:par><p:cTn id=\"4\" fill=\"hold\"><p:stCondLst><p:cond delay=\"0\"/></p:stCondLst><p:childTnLst>"
            + "<p:par><p:cTn id=\"5\" presetID=\"10\" presetClass=\"entr\" presetSubtype=\"0\" fill=\"remove\" nodeType=\"clickEffect\"><p:stCondLst><p:cond delay=\"0\"/></p:stCondLst><p:childTnLst>"
            + "<p:set><p:cBhvr><p:cTn id=\"6\" dur=\"1\" fill=\"hold\"/><p:tgtEl><p:spTgt spid=\"4\"/></p:tgtEl><p:attrNameLst><p:attrName>style.visibility</p:attrName></p:attrNameLst></p:cBhvr><p:to><p:strVal val=\"visible\"/></p:to></p:set>"
            + "</p:childTnLst></p:cTn></p:par></p:childTnLst></p:cTn></p:par></p:childTnLst></p:cTn></p:par></p:childTnLst></p:cTn>"
            + "<p:nextCondLst><p:cond evt=\"onClick\" delay=\"0\"><p:tgtEl><p:spTgt spid=\"3\"/></p:tgtEl></p:cond></p:nextCondLst></p:seq>"
            + "</p:childTnLst></p:cTn></p:par></p:tnLst></p:timing>";
        var effects = new PptxAnimationMapper().FromTiming(new P.Timing(xml), spid => spid switch { 3 => ["button"], 4 => ["picture"], _ => [] }, "s1");
        var effect = Assert.Single(effects);
        Assert.Equal("picture", effect.TargetId);
        Assert.Equal("button", effect.TriggerShapeId);
        Assert.True(effect.Rewind);
    }

    [Theory]
    [InlineData(AdvanceMode.OnClick)]
    [InlineData(AdvanceMode.AfterTime)]
    [InlineData(AdvanceMode.OnClickOrAfterTime)]
    public void AdvanceModesSurviveBothFormats(AdvanceMode mode)
    {
        var deck = Deck();
        var transition = new SlideTransition { Kind = TransitionKind.Fade, Advance = mode, AdvanceAfterMs = mode == AdvanceMode.OnClick ? 0 : 4000 };
        deck.Slides[0].Transition = transition;
        var pptx = ReadPptx(Pptx(deck)).Slides[0].Transition;
        Assert.Equal(mode, pptx.Advance);
        var odp = RoundTripOdp(deck).Slides[0].Transition;
        Assert.Equal(mode, odp.Advance);
        if (mode != AdvanceMode.OnClick)
        {
            Assert.Equal(4000, pptx.AdvanceAfterMs);
            Assert.Equal(4000, odp.AdvanceAfterMs);
        }
    }

    [Fact]
    public void OdfDurationsAreCappedAtOneHourLikePptx()
    {
        var deck = Deck();
        var slide = deck.Slides[0];
        slide.AddAnimation(new AnimationEffect { Id = "long", TargetId = slide.InZOrder().First().Id, Preset = "fade", DurationMs = 7_200_000, DelayMs = 7_200_000 });
        var odp = RoundTripOdp(deck).Slides[0].Animations.Single();
        var pptx = ReadPptx(Pptx(deck)).Slides[0].Animations.Single();
        Assert.Equal(AnimationEffect.LongestTimingMs, odp.DelayMs);
        Assert.Equal(pptx.DelayMs, odp.DelayMs);
        Assert.True(odp.DurationMs <= AnimationEffect.LongestTimingMs);
        Assert.True(pptx.DurationMs <= AnimationEffect.LongestTimingMs);
    }
}

using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round158SequenceTimingTests
{
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";

    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private Presentation Deck(params AnimationTrigger[] triggers)
    {
        var theme = _themes.Default;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master };
        deck.AddExisting(_slides.Create(deck.NextId(), master, SlideLayout.TitleAndContent));
        var slide = deck.Slides[0];
        var ids = slide.InZOrder().Select(element => element.Id).ToList();
        for (var index = 0; index < triggers.Length; index++)
            slide.AddAnimation(new AnimationEffect { Id = $"e{index}", TargetId = ids[index], Preset = "fade", Trigger = triggers[index], DurationMs = 700, DelayMs = 100 * index });
        return deck;
    }

    private XElement MainSequence(Presentation deck)
    {
        using var buffer = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, buffer).Succeeded);
        using var zip = new ZipArchive(new MemoryStream(buffer.ToArray()));
        using var stream = zip.GetEntry("ppt/slides/slide1.xml")!.Open();
        return XDocument.Load(stream).Descendants(P + "cTn").Single(node => (string?)node.Attribute("nodeType") == "mainSeq");
    }

    private static IEnumerable<XElement> Steps(XElement clickPar) =>
        clickPar.Element(P + "cTn")!.Element(P + "childTnLst")!.Elements(P + "par");

    private static string? Delay(XElement par) =>
        (string?)par.Element(P + "cTn")!.Element(P + "stCondLst")!.Elements(P + "cond").First().Attribute("delay");

    [Fact]
    public void After_previous_effect_starts_in_its_own_step_after_the_previous_one_ends()
    {
        var main = MainSequence(Deck(AnimationTrigger.OnClick, AnimationTrigger.AfterPrevious));

        var click = main.Element(P + "childTnLst")!.Elements(P + "par").Single();
        var steps = Steps(click).ToList();

        Assert.Equal(2, steps.Count);
        Assert.Equal("0", Delay(steps[0]));
        Assert.Equal("700", Delay(steps[1]));
    }

    [Fact]
    public void Automatic_first_effect_starts_with_the_slide()
    {
        var main = MainSequence(Deck(AnimationTrigger.WithPrevious, AnimationTrigger.OnClick));

        var clicks = main.Element(P + "childTnLst")!.Elements(P + "par").ToList();
        var conditions = clicks[0].Element(P + "cTn")!.Element(P + "stCondLst")!.Elements(P + "cond").ToList();

        Assert.Contains(conditions, condition => (string?)condition.Attribute("evt") == "onBegin" && (string?)condition.Attribute("delay") == "0");
        Assert.Equal("indefinite", Delay(clicks[1]));
    }
}

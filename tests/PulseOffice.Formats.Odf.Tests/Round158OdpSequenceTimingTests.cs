using System.Xml.Linq;
using PulseOffice.Formats.Packaging;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round158OdpSequenceTimingTests
{
    private static readonly XNamespace Anim = OdfPackage.Namespaces.Anim;
    private static readonly XNamespace Smil = OdfPackage.Namespaces.Smil;
    private static readonly XNamespace Pres = OdfPackage.Namespaces.Presentation;

    private static XElement MainSequence(params AnimationTrigger[] triggers)
    {
        var themes = new ThemeCatalog();
        var master = new MasterFactory().CreateStandard(themes.Default);
        var deck = new Presentation { Theme = themes.Default, Master = master };
        var slide = new SlideFactory().Create(deck.NextId(), master, SlideLayout.TitleAndContent);
        var ids = slide.InZOrder().Select(element => element.Id).ToList();
        for (var index = 0; index < triggers.Length; index++)
            slide.AddAnimation(new AnimationEffect { Id = $"e{index}", TargetId = ids[index], Preset = "fade", Trigger = triggers[index], DurationMs = 700 });
        return new OdfAnimationMapper().Build(slide, "page1")!.Element(Anim + "seq")!;
    }

    [Fact]
    public void After_previous_effect_starts_in_its_own_step_after_the_previous_one_ends()
    {
        var click = MainSequence(AnimationTrigger.OnClick, AnimationTrigger.AfterPrevious).Elements(Anim + "par").Single();

        var steps = click.Elements(Anim + "par").ToList();

        Assert.Null(click.Attribute(Pres + "node-type"));
        Assert.Equal(2, steps.Count);
        Assert.Equal("0s", (string?)steps[0].Attribute(Smil + "begin"));
        Assert.Equal("0.7s", (string?)steps[1].Attribute(Smil + "begin"));
        Assert.All(steps, step => Assert.NotNull(step.Elements(Anim + "par").Single().Attribute(Pres + "preset-class")));
    }

    [Fact]
    public void Automatic_first_effect_starts_with_the_slide_and_clicks_wait()
    {
        var clicks = MainSequence(AnimationTrigger.WithPrevious, AnimationTrigger.OnClick).Elements(Anim + "par").ToList();

        Assert.Equal("0s", (string?)clicks[0].Attribute(Smil + "begin"));
        Assert.Equal("indefinite", (string?)clicks[1].Attribute(Smil + "begin"));
    }
}

using PulseOffice.Formats.Odf;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Presentations;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class AnimationBehaviourParityTests
{
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();
    private readonly IAnimationBehaviourLibrary _library = new AnimationBehaviourLibrary();
    private readonly IAnimationPresetRegistry _presets = new AnimationPresetRegistry();
    private readonly IAnimationBehaviourResolver _resolver = new AnimationBehaviourResolver();
    private readonly IAnimationFilterTable _filters = new AnimationFilterTable();

    public static TheoryData<string, string, string?> Presets()
    {
        var data = new TheoryData<string, string, string?>();
        foreach (var preset in new AnimationBehaviourLibrary().All.DistinctBy(preset => (preset.Category, preset.Preset, preset.Subtype)))
            data.Add(preset.Category.ToString(), preset.Preset, preset.Subtype);
        return data;
    }

    [Fact]
    public void LaLibreriaCopreTuttiIPresetDiImpress()
    {
        Assert.True(_library.All.Count >= 290);
        foreach (var category in Enum.GetValues<AnimationCategory>())
            Assert.Contains(_library.All, preset => preset.Category == category);
    }

    [Theory]
    [MemberData(nameof(Presets))]
    public void UnEffettoCreatoInDeckSiSalvaInOdpConIlSuoComportamento(string category, string odfPreset, string? subtype)
    {
        var effect = Effect(category, odfPreset, subtype);
        var expected = Signature(_resolver.Resolve(effect), false);

        var back = Single(ThroughOdp(Deck(effect)));

        Assert.Equal(effect.Preset, back.Preset);
        Assert.Equal(effect.Subtype, back.Subtype);
        Assert.Equal(expected, Signature(back.Native!.Behaviours!, false));
        if (_resolver.Resolve(effect).NaturalDuration > 0) Assert.Equal(effect.DurationMs, back.DurationMs);
    }

    [Theory]
    [MemberData(nameof(Presets))]
    public void UnEffettoCreatoInDeckSiSalvaInPptxConIlSuoComportamento(string category, string odfPreset, string? subtype)
    {
        var effect = Effect(category, odfPreset, subtype);
        var expected = Signature(_resolver.Resolve(effect), true);

        if (OnlyInOdf(effect)) return;

        var back = Single(ThroughPptx(Deck(effect)));

        Assert.Equal(expected, Signature(back.Native!.Behaviours!, true));
        if (_presets.ReachesOoxml(effect.Category, effect.Preset))
        {
            Assert.Equal(_presets.OdfPresetId(effect.Category, effect.Preset), _presets.OdfPresetId(back.Category, back.Preset));
            Assert.Equal(effect.Subtype, back.Subtype);
        }
        else
        {
            Assert.Contains("PPTX032", new PptxWriter().CheckFidelity(Deck(effect)).Losses.Select(loss => loss.Code));
        }

        if (_resolver.Resolve(effect).NaturalDuration > 0 && !Signature(_resolver.Resolve(effect), false).Contains("physics", StringComparison.Ordinal))
            Assert.Equal(effect.DurationMs, back.DurationMs);
    }

    [Theory]
    [MemberData(nameof(Presets))]
    public void IlComportamentoNativoSiTraduceFraIFormati(string category, string odfPreset, string? subtype)
    {
        var effect = Effect(category, odfPreset, subtype);
        var expected = Signature(_resolver.Resolve(effect), true);
        if (OnlyInOdf(effect)) return;

        var fromOdp = Single(ThroughPptx(ThroughOdp(Deck(effect))));
        var fromPptx = Single(ThroughOdp(ThroughPptx(Deck(effect))));

        Assert.Equal(expected, Signature(fromOdp.Native!.Behaviours!, true));
        Assert.Equal(expected, Signature(fromPptx.Native!.Behaviours!, true));
    }

    [Fact]
    public void EntrareVolandoNonSiScriveComeDissolvenza()
    {
        var effect = Effect("Entrance", "ooo-entrance-fly-in", "from-left");
        var odp = Single(ThroughOdp(Deck(effect))).Native!.Xml;
        var pptx = Single(ThroughPptx(Deck(effect))).Native!.Xml;

        Assert.DoesNotContain("crossfade", odp, StringComparison.Ordinal);
        Assert.Contains("0-width/2", odp, StringComparison.Ordinal);
        Assert.DoesNotContain("filter=\"fade\"", pptx, StringComparison.Ordinal);
        Assert.Contains("0-#ppt_w/2", pptx, StringComparison.Ordinal);
    }

    [Fact]
    public void UnEffettoModificatoNellaDurataConservaIlComportamentoNativoInScala()
    {
        var effect = Effect("Emphasis", "ooo-emphasis-spin", null);
        var read = Single(ThroughPptx(Deck(effect)));
        var longer = read with { DurationMs = read.DurationMs * 2 };

        var back = Single(ThroughOdp(Deck(longer)));

        Assert.Equal(longer.DurationMs, back.DurationMs);
        Assert.Contains("animateTransform", back.Native!.Xml, StringComparison.Ordinal);
    }

    private bool OnlyInOdf(AnimationEffect effect)
    {
        if (Signature(_resolver.Resolve(effect), true) != "|") return false;
        Assert.Contains("PPTX032", new PptxWriter().CheckFidelity(Deck(effect)).Losses.Select(loss => loss.Code));
        Assert.NotEmpty(ThroughPptx(Deck(effect)).Slides[0].Animations);
        return true;
    }

    private AnimationEffect Effect(string category, string odfPreset, string? subtype)
    {
        var parsed = Enum.Parse<AnimationCategory>(category);
        return new AnimationEffect
        {
            Id = "a1",
            TargetId = "placeholder",
            Category = parsed,
            Preset = _presets.FromOdf(parsed, odfPreset),
            Subtype = subtype,
            DurationMs = 1200,
        };
    }

    private Presentation Deck(AnimationEffect effect)
    {
        var master = _masters.CreateStandard(_themes.Default);
        var deck = new Presentation { Master = master, Theme = _themes.Default };
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.TitleAndContent);
        var title = slide.Placeholder(PlaceholderRole.Title)!;
        slide.AddAnimation(effect with { TargetId = title.Id });
        deck.AddExisting(slide);
        return deck;
    }

    private AnimationEffect Single(Presentation deck) => Assert.Single(deck.Slides[0].Animations);

    private Presentation ThroughOdp(Presentation deck)
    {
        using var stream = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, stream).Succeeded);
        stream.Position = 0;
        return new OdpReader(_themes, _masters).Read(stream).Value!;
    }

    private Presentation ThroughPptx(Presentation deck)
    {
        using var stream = new MemoryStream();
        Assert.True(new PptxWriter().Write(deck, stream).Succeeded);
        stream.Position = 0;
        return new PptxReader(_themes, _masters).Read(stream).Value!;
    }

    private string Signature(AnimationBehaviours behaviours, bool ooxml) =>
        $"{behaviours.IterateType}|{string.Join(' ', behaviours.Nodes.Select(node => Signature(node, ooxml)).OfType<string>())}";

    private string? Signature(AnimationBehaviour node, bool ooxml) => node switch
    {
        GroupBehaviour group => $"{(group.Sequential ? "seq" : "par")}[{string.Join(' ', group.Children.Select(child => Signature(child, ooxml)).OfType<string>())}]",
        SetBehaviour set => $"set:{set.Attribute}={set.To}",
        AnimateBehaviour animate => $"animate:{animate.Attribute}:{string.Join(';', animate.Values ?? [])}:{animate.To}:{animate.By}",
        ColorBehaviour color => $"color:{color.Attribute}:{color.To}",
        TransformBehaviour transform => $"transform:{transform.TransformType}:{transform.To}:{transform.By}",
        MotionBehaviour => "motion",
        FilterBehaviour filter => ooxml
            ? $"filter:{_filters.ToOoxml(filter)}:{filter.Entering}"
            : $"filter:{filter.Type}:{filter.Subtype}:{filter.Forward}:{filter.Entering}",
        CommandBehaviour command => $"command:{command.Command}",
        PhysicsBehaviour => ooxml ? null : "physics",
        _ => "?",
    };
}

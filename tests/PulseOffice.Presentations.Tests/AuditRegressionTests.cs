using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class AuditRegressionTests
{
    private readonly IAnimationScheduler _scheduler = new AnimationScheduler();

    private static AnimationEffect Effect(string target, AnimationTrigger trigger, AnimationCategory category,
        string preset = "", int duration = 400) =>
        new() { Id = target + "-" + category, TargetId = target, Trigger = trigger, DurationMs = duration,
            Category = category, Preset = preset };

    [Fact]
    public void UnUscitaNonAncoraIniziataNonCancellaLEntrataGiaFinita()
    {
        var schedule = _scheduler.Schedule([
            Effect("a", AnimationTrigger.OnClick, AnimationCategory.Entrance, "fade"),
            Effect("a", AnimationTrigger.OnClick, AnimationCategory.Exit, "fade"),
        ]);

        Assert.Equal(1, _scheduler.VisualAt("a", schedule, schedule[0].EndMs + 10).Opacity, 3);
    }

    [Fact]
    public void UnEntrataNonAncoraIniziataNascondeLElemento()
    {
        var schedule = _scheduler.Schedule([
            Effect("a", AnimationTrigger.OnClick, AnimationCategory.Emphasis, "grow"),
            Effect("a", AnimationTrigger.OnClick, AnimationCategory.Entrance, "fade"),
        ]);

        Assert.Equal(0, _scheduler.VisualAt("a", schedule, schedule[0].EndMs + 10).Opacity, 3);
    }

    [Fact]
    public void IlPercorsoPartedaZeroECresce()
    {
        var schedule = _scheduler.Schedule([Effect("a", AnimationTrigger.OnClick, AnimationCategory.MotionPath)]);

        Assert.Equal(0, _scheduler.VisualAt("a", schedule, schedule[0].StartMs).OffsetX, 6);
        Assert.NotEqual(0, _scheduler.VisualAt("a", schedule, schedule[0].EndMs).OffsetX);
    }

    [Fact]
    public void UnGruppoInizialeAutomaticoNonEUnPassoAlClic()
    {
        var schedule = _scheduler.Schedule([
            Effect("a", AnimationTrigger.AfterPrevious, AnimationCategory.Entrance, "fade"),
            Effect("b", AnimationTrigger.OnClick, AnimationCategory.Entrance, "fade"),
        ]);

        Assert.Single(_scheduler.ClickGroups(schedule));
        Assert.NotNull(_scheduler.EntryGroup(schedule));
    }

    [Fact]
    public void IlGruppoAutomaticoSiVedeAppenaEntratiNellaDiapositiva()
    {
        var slide = new Slide("s1", SlideLayout.Blank);
        slide.AddAnimation(Effect("a", AnimationTrigger.AfterPrevious, AnimationCategory.Entrance, "fade"));
        var controller = new PresentationController([slide], _scheduler);

        Assert.Equal(0, controller.CurrentStepCount());
        Assert.True(controller.FrozenTimeMs() > 0);
    }

    [Fact]
    public void IlGruppoAutomaticoSiAnimaEntrandoNellaDiapositiva()
    {
        var slide = new Slide("s1", SlideLayout.Blank);
        slide.AddAnimation(Effect("a", AnimationTrigger.AfterPrevious, AnimationCategory.Entrance, "fade"));
        var controller = new PresentationController([slide], _scheduler);

        var window = controller.EntryWindow();

        Assert.NotNull(window);
        Assert.True(window!.Value.EndMs > window.Value.StartMs);
        Assert.Equal(window.Value.EndMs, controller.FrozenTimeMs());
    }

    [Fact]
    public void UnaDiapositivaCheIniziaAlClicNonHaAnimazioneDIngresso()
    {
        var slide = new Slide("s1", SlideLayout.Blank);
        slide.AddAnimation(Effect("a", AnimationTrigger.OnClick, AnimationCategory.Entrance, "fade"));
        var controller = new PresentationController([slide], _scheduler);

        Assert.Null(controller.EntryWindow());
    }

    [Fact]
    public void UnaScalettaVuotaNonFaCadere()
    {
        var controller = new PresentationController(Array.Empty<Slide>(), _scheduler);

        Assert.True(controller.IsEmpty);
        Assert.True(controller.AtEnd);
        controller.GoTo(3);
        Assert.Equal(AdvanceResult.None, controller.Advance());
    }

    [Fact]
    public void SaltareAUnaDiapositivaRipartedalPrimoPasso()
    {
        var first = new Slide("s1", SlideLayout.Blank);
        var second = new Slide("s2", SlideLayout.Blank);
        second.AddAnimation(Effect("a", AnimationTrigger.OnClick, AnimationCategory.Entrance, "fade"));
        var controller = new PresentationController([first, second], _scheduler);

        controller.GoTo(1);

        Assert.Equal(-1, controller.StepIndex);
        Assert.Equal(AdvanceResult.RevealedStep, controller.Advance());
    }

    [Fact]
    public void PresentareDaUnaDiapositivaNascostaPartedallaVisibileSuccessiva()
    {
        var slides = new[] { new Slide("s1", SlideLayout.Blank), new Slide("s2", SlideLayout.Blank) { Hidden = true }, new Slide("s3", SlideLayout.Blank) };
        var playlist = slides.Where(s => !s.Hidden).ToList();

        Assert.Equal(1, new ShowStart().PositionFor(slides, playlist, 1));
    }

    [Fact]
    public void UnNomeAutomaticoAltruiNonESceglibile()
    {
        var deck = new Presentation();
        var slide = new Slide("s1", SlideLayout.Blank);
        deck.AddExisting(slide);

        Assert.False(SlideNames.IsAvailable(deck, slide, "slide7"));
        Assert.True(SlideNames.IsAvailable(deck, slide, "Introduzione"));
    }

    [Fact]
    public void UnTracciatoPiattoSiRiadattaConAltezzaMinima()
    {
        var path = new FreeformPath([new PathFigure([new PathNode(new ShapePoint(0, 0.5)), new PathNode(new ShapePoint(1, 0.5))])]);
        var freeform = new FreeformObject { Id = "f", Path = FreeformPath.Empty };
        var element = new ObjectElement { Id = "f", Content = freeform, Bounds = new DocumentRect(new Twip(0), new Twip(0), new Twip(2000), new Twip(1000)) };

        var refitted = new FreeformRefit().Refit(element, freeform, path);

        Assert.Equal(1, refitted.Bounds.Height.Value);
        Assert.Equal(500, refitted.Bounds.Top.Value);
    }
}

using PulseOffice.Presentations;
using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.Tests;

public sealed class PresentationControllerTests
{
    private readonly IAnimationScheduler _scheduler = new AnimationScheduler();
    private readonly ISlideFactory _slides = new SlideFactory();
    private readonly SlideMaster _master = new MasterFactory().CreateStandard(new ThemeCatalog().Default);

    private Presentation Deck(int slideCount, int animationsOnSecond = 0)
    {
        var deck = new Presentation { Master = _master, Theme = new ThemeCatalog().Default };
        for (var i = 0; i < slideCount; i++)
        {
            var slide = _slides.Create(deck.NextId(), _master, SlideLayout.TitleAndContent);
            if (i == 1)
                for (var a = 0; a < animationsOnSecond; a++)
                    slide.AddAnimation(new AnimationEffect
                    {
                        Id = $"a{a}", TargetId = slide.Placeholder(PlaceholderRole.Title)!.Id,
                        Trigger = AnimationTrigger.OnClick,
                    });
            deck.AddExisting(slide);
        }
        return deck;
    }

    [Fact]
    public void AdvanceMovesThroughSlides()
    {
        var c = new PresentationController(Deck(3), _scheduler);

        Assert.Equal(AdvanceResult.ChangedSlide, c.Advance());
        Assert.Equal(1, c.SlideIndex);
        Assert.Equal(AdvanceResult.ChangedSlide, c.Advance());
        Assert.Equal(2, c.SlideIndex);
        Assert.Equal(AdvanceResult.Finished, c.Advance());
    }

    [Fact]
    public void AdvanceRevealsStepsBeforeChangingSlide()
    {
        var c = new PresentationController(Deck(3, animationsOnSecond: 2), _scheduler);

        Assert.Equal(AdvanceResult.ChangedSlide, c.Advance()); // to slide 1
        Assert.Equal(AdvanceResult.RevealedStep, c.Advance()); // step 0
        Assert.Equal(0, c.StepIndex);
        Assert.Equal(AdvanceResult.RevealedStep, c.Advance()); // step 1
        Assert.Equal(1, c.StepIndex);
        Assert.Equal(AdvanceResult.ChangedSlide, c.Advance()); // to slide 2
        Assert.Equal(2, c.SlideIndex);
    }

    [Fact]
    public void BackReversesStepsAndSlides()
    {
        var c = new PresentationController(Deck(2, animationsOnSecond: 1), _scheduler);
        c.Advance(); c.Advance(); // slide 1, step 0

        Assert.Equal(AdvanceResult.RevealedStep, c.Back()); // step -1
        Assert.Equal(-1, c.StepIndex);
        Assert.Equal(AdvanceResult.ChangedSlide, c.Back()); // slide 0
        Assert.Equal(0, c.SlideIndex);
    }

    [Fact]
    public void AtEndWhenLastSlideFullyRevealed()
    {
        var c = new PresentationController(Deck(2), _scheduler);
        Assert.False(c.AtEnd);
        c.Advance();
        Assert.True(c.AtEnd);
    }

    [Fact]
    public void SenzaAnimazioniUnPassoIndietroTornaSubitoAllaDiapositivaPrecedente()
    {
        var slides = new[] { new Slide("a", SlideLayout.Blank), new Slide("b", SlideLayout.Blank) };
        var controller = new PresentationController(slides, new AnimationScheduler(), ignoreSteps: true);

        controller.Advance();
        Assert.Equal(1, controller.SlideIndex);

        Assert.Equal(AdvanceResult.ChangedSlide, controller.Back());
        Assert.Equal(0, controller.SlideIndex);
    }
}

using PulseOffice.Platform;
using PulseOffice.Presentations;
using PulseOffice.Presentations.UI;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round189VideoMotionTests
{
    private readonly SlideVideoTimeline _timeline = new(new AnimationScheduler());

    private static Slide Moving(string id)
    {
        var slide = new Slide(id, SlideLayout.Blank)
        {
            Transition = new SlideTransition { Kind = TransitionKind.Fade, DurationMs = 660 },
        };
        slide.InsertAnimation(0, new AnimationEffect
        {
            Id = slide.NextAnimationId(),
            TargetId = "shape",
            Category = AnimationCategory.Entrance,
            Preset = "fade",
            Trigger = AnimationTrigger.AfterPrevious,
            DurationMs = 660,
        });
        return slide;
    }

    [Fact]
    public void A_still_slide_is_one_frame_and_a_moving_slide_is_drawn_frame_by_frame()
    {
        var shots = _timeline.Shots([new Slide("a", SlideLayout.Blank), Moving("b")], _ => 3000);

        Assert.Single(shots, shot => shot.SlideIndex == 0);
        var moving = shots.Where(shot => shot.SlideIndex == 1).ToList();
        Assert.Equal(10, moving.Count(shot => shot.TransitionProgress is not null));
        Assert.Equal(10, moving.Count(shot => shot.TransitionProgress is null && shot.AnimationTimeMs < 660));
        Assert.Equal(3000, moving.Sum(shot => shot.DurationMs));
        Assert.Equal(660, moving[^1].AnimationTimeMs);
    }

    [Fact]
    public void A_long_animation_keeps_a_bounded_number_of_frames()
    {
        var slide = Moving("c");
        slide.InsertAnimation(1, new AnimationEffect
        {
            Id = slide.NextAnimationId(),
            TargetId = "shape",
            Category = AnimationCategory.Emphasis,
            Preset = "spin",
            Trigger = AnimationTrigger.AfterPrevious,
            DurationMs = 590_000,
        });

        Assert.True(_timeline.Shots([slide], _ => 600_000).Count <= SlideVideoTimeline.MostShotsPerPhase + 1);
    }

    [Fact]
    public async Task Transition_frames_ask_for_both_slides()
    {
        var renderer = new RecordingRenderer();
        var source = await new DeckVideoFrameSourceFactory().CreateAsync(
            [new Slide("a", SlideLayout.Blank), Moving("b")], _ => 3000, renderer, TestContext.Current.CancellationToken);

        await source.RenderAsync(1, TestContext.Current.CancellationToken);

        var request = renderer.Requests[^1];
        Assert.Equal("a", request.Outgoing?.Id);
        Assert.Equal("b", request.Slide.Id);
        Assert.NotNull(request.Transition);
        Assert.NotNull(request.Visuals);
    }

    private sealed class RecordingRenderer : ISlideFrameRenderer
    {
        public List<SlideFrameRequest> Requests { get; } = [];

        public Task<RasterSlide> RenderAsync(SlideFrameRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(new RasterSlide(8, 4, new byte[8 * 4 * 4]));
        }
    }
}

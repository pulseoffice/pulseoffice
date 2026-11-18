using PulseOffice.Platform;

namespace PulseOffice.Presentations.UI;

public sealed record SlideFrameRequest(Slide Slide, Func<string, ElementVisual>? Visuals)
{
    public Slide? Outgoing { get; init; }

    public Func<string, ElementVisual>? OutgoingVisuals { get; init; }

    public TransitionFrame? Transition { get; init; }
}

public interface ISlideFrameRenderer
{
    Task<RasterSlide> RenderAsync(SlideFrameRequest request, CancellationToken ct);
}

public interface IDeckVideoFrameSourceFactory
{
    Task<IVideoFrameSource> CreateAsync(
        IReadOnlyList<Slide> slides, Func<Slide, int> durationOf, ISlideFrameRenderer renderer, CancellationToken ct);
}

public sealed class DeckVideoFrameSourceFactory(IAnimationScheduler scheduler, ISlideVideoTimeline timeline) : IDeckVideoFrameSourceFactory
{
    public DeckVideoFrameSourceFactory() : this(new AnimationScheduler())
    {
    }

    public DeckVideoFrameSourceFactory(IAnimationScheduler scheduler) : this(scheduler, new SlideVideoTimeline(scheduler))
    {
    }

    public async Task<IVideoFrameSource> CreateAsync(
        IReadOnlyList<Slide> slides, Func<Slide, int> durationOf, ISlideFrameRenderer renderer, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(slides);
        ArgumentNullException.ThrowIfNull(durationOf);
        ArgumentNullException.ThrowIfNull(renderer);
        if (slides.Count == 0) throw new ArgumentException("At least one slide is required.", nameof(slides));

        var guarded = new GuardedSlideFrameRenderer(renderer);
        var shots = timeline.Shots(slides, durationOf);
        var source = new DeckVideoFrameSource(shots, new DeckVideoShotRequests(slides, scheduler, timeline), guarded);
        ct.ThrowIfCancellationRequested();
        await source.PrimeAsync(ct).ConfigureAwait(false);
        return source;
    }
}

public sealed class DeckVideoShotRequests(IReadOnlyList<Slide> slides, IAnimationScheduler scheduler, ISlideVideoTimeline timeline)
{
    private readonly Dictionary<int, SlideAnimationPlan> _plans = [];
    private readonly TransitionStage _transitions = new();

    public SlideFrameRequest For(SlideVideoShot shot)
    {
        ArgumentNullException.ThrowIfNull(shot);
        var slide = slides[shot.SlideIndex];
        var request = new SlideFrameRequest(slide, Visuals(shot.SlideIndex, shot.AnimationTimeMs));
        if (shot.TransitionProgress is not { } progress || shot.SlideIndex == 0) return request;

        var previous = shot.SlideIndex - 1;
        return request with
        {
            Outgoing = slides[previous],
            OutgoingVisuals = Visuals(previous, timeline.AnimatedMs(slides[previous])),
            Transition = _transitions.EffectFor(slide.Transition).At(Math.Clamp(progress, 0, 1)),
        };
    }

    private Func<string, ElementVisual>? Visuals(int index, int timeMs)
    {
        var slide = slides[index];
        if (slide.Animations.Count == 0) return null;
        if (!_plans.TryGetValue(index, out var plan))
        {
            plan = new SlideAnimationPlan(slide, scheduler);
            _plans[index] = plan;
        }

        return id => plan.VisualAt(id, timeMs, _ => -1);
    }
}

public sealed class DeckVideoFrameSource(
    IReadOnlyList<SlideVideoShot> shots, DeckVideoShotRequests requests, ISlideFrameRenderer renderer) : IVideoFrameSource
{
    private RasterSlide? _first;

    public int Count => shots.Count;

    public int Width { get; private set; }

    public int Height { get; private set; }

    public async Task PrimeAsync(CancellationToken ct)
    {
        var first = await renderer.RenderAsync(requests.For(shots[0]), ct).ConfigureAwait(false);
        _first = first;
        Width = first.Width;
        Height = first.Height;
    }

    public async ValueTask<VideoFrame> RenderAsync(int index, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var shot = shots[index];
        RasterSlide raster;

        if (index == 0 && _first is { } cached)
        {
            raster = cached;
            _first = null;
        }
        else
        {
            raster = await renderer.RenderAsync(requests.For(shot), ct).ConfigureAwait(false);
        }

        return new VideoFrame(raster.Rgb, shot.DurationMs);
    }
}

public sealed class GuardedSlideFrameRenderer(ISlideFrameRenderer inner) : ISlideFrameRenderer
{
    public async Task<RasterSlide> RenderAsync(SlideFrameRequest request, CancellationToken ct)
    {
        try
        {
            return await inner.RenderAsync(request, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not VideoFrameException)
        {
            throw new VideoFrameException("VIDEO006", ex.Message, ex);
        }
    }
}

public sealed class DispatchedSlideFrameRenderer(ISlideImageRenderer renderer, Presentation deck, int pixelWidth) : ISlideFrameRenderer
{
    public Task<RasterSlide> RenderAsync(SlideFrameRequest request, CancellationToken ct) =>
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(
            () => renderer.RenderBgra(deck, request, pixelWidth),
            Avalonia.Threading.DispatcherPriority.Background, ct).GetTask();
}

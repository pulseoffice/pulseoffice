using PulseOffice.Platform;

namespace PulseOffice.Presentations.UI;

public interface ISlideFrameRenderer
{
    Task<RasterSlide> RenderAsync(Slide slide, CancellationToken ct);
}

public interface IDeckVideoFrameSourceFactory
{
    Task<IVideoFrameSource> CreateAsync(
        IReadOnlyList<Slide> slides, Func<Slide, int> durationOf, ISlideFrameRenderer renderer, CancellationToken ct);
}

public sealed class DeckVideoFrameSourceFactory : IDeckVideoFrameSourceFactory
{
    public async Task<IVideoFrameSource> CreateAsync(
        IReadOnlyList<Slide> slides, Func<Slide, int> durationOf, ISlideFrameRenderer renderer, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(slides);
        ArgumentNullException.ThrowIfNull(durationOf);
        ArgumentNullException.ThrowIfNull(renderer);
        if (slides.Count == 0) throw new ArgumentException("At least one slide is required.", nameof(slides));

        var guarded = new GuardedSlideFrameRenderer(renderer);
        ct.ThrowIfCancellationRequested();
        var first = await guarded.RenderAsync(slides[0], ct).ConfigureAwait(false);
        return new DeckVideoFrameSource(slides, durationOf, guarded, first);
    }
}

public sealed class DeckVideoFrameSource(
    IReadOnlyList<Slide> slides, Func<Slide, int> durationOf, ISlideFrameRenderer renderer, RasterSlide first) : IVideoFrameSource
{
    private RasterSlide? _first = first;

    public int Count => slides.Count;

    public int Width { get; } = first.Width;

    public int Height { get; } = first.Height;

    public async ValueTask<VideoFrame> RenderAsync(int index, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var slide = slides[index];
        RasterSlide raster;

        if (index == 0 && _first is { } cached)
        {
            raster = cached;
            _first = null;
        }
        else
        {
            raster = await renderer.RenderAsync(slide, ct).ConfigureAwait(false);
        }

        return new VideoFrame(raster.Rgb, durationOf(slide));
    }
}

public sealed class GuardedSlideFrameRenderer(ISlideFrameRenderer inner) : ISlideFrameRenderer
{
    public async Task<RasterSlide> RenderAsync(Slide slide, CancellationToken ct)
    {
        try
        {
            return await inner.RenderAsync(slide, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not VideoFrameException)
        {
            throw new VideoFrameException("VIDEO006", ex.Message, ex);
        }
    }
}

public sealed class DispatchedSlideFrameRenderer(ISlideImageRenderer renderer, Presentation deck, int pixelWidth) : ISlideFrameRenderer
{
    public Task<RasterSlide> RenderAsync(Slide slide, CancellationToken ct) =>
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(
            () => renderer.RenderBgra(deck, slide, pixelWidth),
            Avalonia.Threading.DispatcherPriority.Background, ct).GetTask();
}

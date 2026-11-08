using PulseOffice.Platform;
using PulseOffice.Presentations;
using PulseOffice.Presentations.UI;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class DeckVideoFrameSourceTests
{
    private readonly DeckVideoFrameSourceFactory _factory = new();

    [Fact]
    public async Task LeDiapositiveSiDisegnanoSoloQuandoIlCodificatoreLeChiede()
    {
        var slides = Enumerable.Range(0, 100).Select(i => new Slide("s" + i, SlideLayout.TitleSlide)).ToList();
        var renderer = new CountingRenderer();

        var source = await _factory.CreateAsync(slides, _ => 1000, renderer, TestContext.Current.CancellationToken);

        Assert.Equal(1, renderer.Calls);
        Assert.Equal(100, source.Count);
        Assert.Equal(8, source.Width);

        await source.RenderAsync(0, TestContext.Current.CancellationToken);
        Assert.Equal(1, renderer.Calls);

        await source.RenderAsync(1, TestContext.Current.CancellationToken);
        Assert.Equal(2, renderer.Calls);
    }

    [Fact]
    public async Task UnErroreDiDisegnoDiventaVideo006()
    {
        var slides = new List<Slide> { new("a", SlideLayout.TitleSlide), new("b", SlideLayout.TitleSlide) };
        var source = await _factory.CreateAsync(slides, _ => 1000, new CountingRenderer(failFrom: 2), TestContext.Current.CancellationToken);

        var error = await Assert.ThrowsAsync<VideoFrameException>(() => source.RenderAsync(1, TestContext.Current.CancellationToken).AsTask());

        Assert.Equal("VIDEO006", error.Code);
        Assert.Equal("out of memory", error.Message);
    }

    [Fact]
    public async Task UnaCancellazioneNonDisegnaAltro()
    {
        var slides = new List<Slide> { new("a", SlideLayout.TitleSlide), new("b", SlideLayout.TitleSlide) };
        var renderer = new CountingRenderer();
        var source = await _factory.CreateAsync(slides, _ => 1000, renderer, TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.RenderAsync(1, new CancellationToken(true)).AsTask());

        Assert.Equal(1, renderer.Calls);
    }

    private sealed class CountingRenderer(int failFrom = int.MaxValue) : ISlideFrameRenderer
    {
        public int Calls { get; private set; }

        public Task<RasterSlide> RenderAsync(Slide slide, CancellationToken ct)
        {
            Calls++;
            if (Calls >= failFrom) throw new InvalidOperationException("out of memory");
            return Task.FromResult(new RasterSlide(8, 4, new byte[8 * 4 * 4]));
        }
    }
}

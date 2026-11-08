using PulseOffice.Platform;
using PulseOffice.Platform.Mac;
using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class VideoEncodingRegressionTests
{
    private readonly VideoFrameFitter _fitter = new();
    private readonly SampleTiming _timing = new();

    [Fact]
    public async Task LeDimensioniDispariSiArrotondanoAlPari()
    {
        var frame = new VideoFrame(new byte[5 * 3 * 4], 1000);

        var fitted = _fitter.Fit(new ListVideoFrameSource([frame], 5, 3));

        Assert.NotNull(fitted);
        Assert.Equal(4, fitted!.Width);
        Assert.Equal(2, fitted.Height);
        Assert.Equal(4 * 2 * 4, (await fitted.RenderAsync(0, TestContext.Current.CancellationToken)).Bgra.Length);
    }

    [Fact]
    public async Task UnFotogrammaDellaLunghezzaSbagliataVieneRifiutato()
    {
        var fitted = _fitter.Fit(new ListVideoFrameSource([new VideoFrame(new byte[10], 1000)], 4, 4));

        var error = await Assert.ThrowsAsync<VideoFrameException>(() => fitted!.RenderAsync(0, TestContext.Current.CancellationToken).AsTask());
        Assert.Equal("VIDEO004", error.Code);
    }

    [Fact]
    public void UnaMisuraTroppoPiccolaVieneRifiutata()
    {
        Assert.Null(_fitter.Fit(new ListVideoFrameSource([new VideoFrame(new byte[4], 1000)], 1, 1)));
    }

    [Fact]
    public void LeDurateSiRicavanoDaiCampioniPresenti()
    {
        var durations = _timing.Durations([0, 2], [100, 200, 300, 400]);

        Assert.Equal([300L, 700L], durations);
    }

    [Fact]
    public void SenzaIlPrimoCampioneLaTempisticaFallisce()
    {
        Assert.Null(_timing.Durations([1, 2], [100, 200, 300]));
    }

    [Fact]
    public void IlFileMp4HaLaDimensioneDelMdatGiusta()
    {
        var writer = new Mp4Writer(4, 2, 600, [0x67, 0x42, 0, 0x1e], [0x68, 0xce]);
        var bytes = writer.Build([[1, 2, 3], [4, 5]], [600, 600]);

        var ftypSize = (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
        var mdatSize = (bytes[ftypSize] << 24) | (bytes[ftypSize + 1] << 16) | (bytes[ftypSize + 2] << 8) | bytes[ftypSize + 3];

        Assert.Equal(8 + 5, mdatSize);
        Assert.Equal((byte)'m', bytes[ftypSize + 4]);
    }

    [Fact]
    public async Task UnaDestinazioneNonScrivibileDaUnErroreENonUnEccezione()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var encoder = new VideoToolboxVideoEncoder();
        var frames = new ListVideoFrameSource([new VideoFrame(new byte[64 * 64 * 4], 500)], 64, 64);
        var path = Path.Combine(Path.GetTempPath(), "pulseoffice-assente-" + Guid.NewGuid().ToString("N"), "video.mp4");

        var result = await encoder.EncodeAsync(frames, 25, path, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task VideoToolboxNonTrattieneIFotogrammiGiaCodificati()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var source = new TrackingFrameSource(40, 64, 64);
        var path = Path.Combine(Path.GetTempPath(), "pulseoffice-video-" + Guid.NewGuid().ToString("N") + ".mp4");

        try
        {
            var result = await new VideoToolboxVideoEncoder().EncodeAsync(source, 25, path, TestContext.Current.CancellationToken);

            Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
            Assert.Equal(40, source.Rendered);
            Assert.True(source.MostAlive <= 3, $"{source.MostAlive} frames were alive at once");
            Assert.Equal("ftyp", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(path), 4, 4));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task LaCancellazioneFermaIlRenderingDeiFotogrammi()
    {
        if (!OperatingSystem.IsMacOS()) return;

        using var cancellation = new CancellationTokenSource();
        var source = new TrackingFrameSource(100, 64, 64, cancelAt: 5, cancellation);
        var path = Path.Combine(Path.GetTempPath(), "pulseoffice-video-" + Guid.NewGuid().ToString("N") + ".mp4");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new VideoToolboxVideoEncoder().EncodeAsync(source, 25, path, cancellation.Token));

        Assert.True(source.Rendered <= 6, $"{source.Rendered} frames rendered after cancellation");
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task UnErroreDiDisegnoDiventaUnaDiagnosticaConIlSuoCodice()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var source = new TrackingFrameSource(10, 64, 64, failAt: 3);
        var path = Path.Combine(Path.GetTempPath(), "pulseoffice-video-" + Guid.NewGuid().ToString("N") + ".mp4");

        var result = await new VideoToolboxVideoEncoder().EncodeAsync(source, 25, path, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        var error = Assert.Single(result.Diagnostics);
        Assert.Equal("VIDEO006", error.Code);
        Assert.Equal("broken slide", error.Arguments[0]);
        Assert.False(File.Exists(path));
    }

    private sealed class TrackingFrameSource(
        int count, int width, int height, int cancelAt = -1, CancellationTokenSource? cancellation = null, int failAt = -1) : IVideoFrameSource
    {
        private readonly List<WeakReference<byte[]>> _issued = [];

        public int Count => count;

        public int Width => width;

        public int Height => height;

        public int Rendered { get; private set; }

        public int MostAlive { get; private set; }

        public ValueTask<VideoFrame> RenderAsync(int index, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (index == failAt) throw new VideoFrameException("VIDEO006", "broken slide");

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            MostAlive = Math.Max(MostAlive, _issued.Count(reference => reference.TryGetTarget(out _)) + 1);

            Rendered++;
            if (index == cancelAt) cancellation?.Cancel();

            var bgra = new byte[width * height * 4];
            _issued.Add(new WeakReference<byte[]>(bgra));
            return ValueTask.FromResult(new VideoFrame(bgra, 200));
        }
    }
}

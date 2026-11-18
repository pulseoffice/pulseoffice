using System.Buffers.Binary;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace PulseOffice.Platform.Media;

public sealed record PcmFormat(int SampleRate, int Channels)
{
    public int BytesPerSecond => SampleRate * Channels * 2;

    public static PcmFormat Standard { get; } = new(48_000, 2);
}

public interface IMediaAudioStream : IDisposable
{
    int Read(Span<byte> pcm);
}

public interface IMediaVideoStream : IDisposable
{
    MediaFrame? Next();
}

public interface IMediaDecoder
{
    bool IsAvailable { get; }

    Task<MediaInfo?> ProbeAsync(string path, CancellationToken ct = default);

    IMediaAudioStream? OpenAudio(string path, TimeSpan start, PcmFormat format);

    IMediaVideoStream? OpenVideo(string path, MediaInfo info, TimeSpan start);
}

public interface IAudioOutput : IDisposable
{
    TimeSpan Played { get; }

    void Write(ReadOnlySpan<byte> pcm);
}

public interface IAudioOutputFactory
{
    bool IsAvailable { get; }

    IAudioOutput? Open(PcmFormat format);
}

public sealed class DecodedMediaPlaybackFactory(IMediaDecoder decoder, IAudioOutputFactory audio, ITemporaryFiles temporary, ILogger<DecodedMediaPlayback> logger) : IMediaPlaybackFactory
{
    public bool IsAvailable => decoder.IsAvailable;

    public IMediaPlayback Create() => new DecodedMediaPlayback(decoder, audio, temporary, logger);
}

public sealed class DecodedMediaPlayback(IMediaDecoder decoder, IAudioOutputFactory audio, ITemporaryFiles temporary, ILogger logger) : IMediaPlayback
{
    private const int AudioChunkBytes = 4800 * 4;
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(40);

    private readonly Lock _gate = new();
    private readonly Stopwatch _clock = new();
    private readonly MediaTimeline _timeline = new();
    private string? _path;
    private TimeSpan _origin;
    private CancellationTokenSource? _run;
    private Timer? _ticker;
    private IAudioOutput? _output;
    private IAudioOutput? _audible;
    private double _volume = 1;
    private bool _ended;

    public MediaInfo? Info { get; private set; }

    public TimeSpan Duration => Info?.Duration ?? TimeSpan.Zero;

    public TimeSpan Position
    {
        get
        {
            var position = _clock.IsRunning ? _timeline.Position(_clock.Elapsed, Volatile.Read(ref _audible)?.Played) : _origin;
            return Duration > TimeSpan.Zero && position > Duration ? Duration : position;
        }
    }

    public bool IsPlaying => _clock.IsRunning;

    public double Volume
    {
        get => Volatile.Read(ref _volume);
        set => Volatile.Write(ref _volume, Math.Clamp(value, 0, 1));
    }

    public event EventHandler<TimeSpan>? PositionChanged;

    public event EventHandler? Ended;

    public event EventHandler<MediaFrame>? FrameReady;

    public async Task<bool> OpenAsync(MediaSource source, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        _path = source switch
        {
            FileMediaSource file => file.Path,
            StreamMediaSource stream => await Spill(stream, ct).ConfigureAwait(false),
            _ => null,
        };
        if (_path is null || !File.Exists(_path)) return false;
        Info = await decoder.ProbeAsync(_path, ct).ConfigureAwait(false);
        return Info is { } info && info.Duration > TimeSpan.Zero && (info.HasAudio || info.HasVideo);
    }

    public void Play()
    {
        lock (_gate)
        {
            if (_path is null || Info is null || _clock.IsRunning) return;
            _ended = false;
            StartStreams();
            _clock.Start();
            _ticker ??= new Timer(_ => Report(), null, Tick, Tick);
        }
    }

    public void Pause()
    {
        lock (_gate)
        {
            if (!_clock.IsRunning) return;
            _origin = Position;
            _clock.Reset();
            StopStreams();
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _clock.Reset();
            _origin = TimeSpan.Zero;
            StopStreams();
        }
    }

    public void Seek(TimeSpan position)
    {
        lock (_gate)
        {
            var running = _clock.IsRunning;
            StopStreams();
            _clock.Reset();
            _origin = position < TimeSpan.Zero ? TimeSpan.Zero : position > Duration ? Duration : position;
            _ended = false;
            if (!running) return;
            StartStreams();
            _clock.Start();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _clock.Reset();
            StopStreams();
            _ticker?.Dispose();
            _ticker = null;
        }
    }

    private async Task<string> Spill(StreamMediaSource source, CancellationToken ct)
    {
        var extension = source.Extension.StartsWith('.') ? source.Extension : $".{source.Extension}";
        var path = Path.Combine(temporary.NewFolder("media-"), $"media{extension}");
        await using var file = File.Create(path);
        await source.Content.CopyToAsync(file, ct).ConfigureAwait(false);
        return path;
    }

    private void Report()
    {
        if (!IsPlaying) return;
        var position = Position;
        PositionChanged?.Invoke(this, position);
        if (position < Duration || _ended) return;
        _ended = true;
        Pause();
        Ended?.Invoke(this, EventArgs.Empty);
    }

    private void StartStreams()
    {
        var path = _path!;
        var info = Info!;
        var run = new CancellationTokenSource();
        _run = run;
        var start = _origin;
        _timeline.Restart(start);
        if (info.HasAudio && audio.IsAvailable)
        {
            _output = audio.Open(PcmFormat.Standard);
            var output = _output;
            if (output is not null) _ = Task.Run(() => PumpAudio(path, start, output, run.Token));
        }

        if (info.HasVideo) _ = Task.Run(() => PumpVideo(path, info, start, run.Token));
    }

    private void StopStreams()
    {
        _run?.Cancel();
        _run?.Dispose();
        _run = null;
        Volatile.Write(ref _audible, null);
        _output?.Dispose();
        _output = null;
    }

    private void PumpAudio(string path, TimeSpan start, IAudioOutput output, CancellationToken ct)
    {
        try
        {
            using var stream = decoder.OpenAudio(path, start, PcmFormat.Standard);
            if (stream is null) return;
            var buffer = new byte[AudioChunkBytes];
            while (!ct.IsCancellationRequested)
            {
                var read = stream.Read(buffer);
                if (read <= 0) return;
                var chunk = buffer.AsSpan(0, read - read % 2);
                Scale(chunk, Volume);
                output.Write(chunk);
                if (!ct.IsCancellationRequested) Interlocked.CompareExchange(ref _audible, output, null);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            logger.LogWarning("Media audio stopped: {Reason}", ex.GetType().Name);
        }
        finally
        {
            Interlocked.CompareExchange(ref _audible, null, output);
        }
    }

    private void PumpVideo(string path, MediaInfo info, TimeSpan start, CancellationToken ct)
    {
        try
        {
            using var stream = decoder.OpenVideo(path, info, start);
            if (stream is null) return;
            while (!ct.IsCancellationRequested && stream.Next() is { } frame)
            {
                var wait = frame.Time - Position;
                if (wait > TimeSpan.Zero && ct.WaitHandle.WaitOne(wait)) return;
                if (!ct.IsCancellationRequested) FrameReady?.Invoke(this, frame);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            logger.LogWarning("Media video stopped: {Reason}", ex.GetType().Name);
        }
    }

    private static void Scale(Span<byte> pcm, double gain)
    {
        if (gain >= 0.9995) return;
        for (var offset = 0; offset + 1 < pcm.Length; offset += 2)
        {
            var sample = BinaryPrimitives.ReadInt16LittleEndian(pcm[offset..]);
            BinaryPrimitives.WriteInt16LittleEndian(pcm[offset..], (short)Math.Round(sample * gain));
        }
    }
}

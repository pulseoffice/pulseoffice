namespace PulseOffice.Platform.Media;

public abstract record MediaSource;

public sealed record FileMediaSource(string Path) : MediaSource;

public sealed record StreamMediaSource(Stream Content, string Extension) : MediaSource;

public sealed record MediaInfo(TimeSpan Duration, bool HasAudio, bool HasVideo, int Width, int Height);

public sealed class MediaFrame(int width, int height, byte[] bgra, TimeSpan time)
{
    public int Width { get; } = width;

    public int Height { get; } = height;

    public int Stride => Width * 4;

    public byte[] Bgra { get; } = bgra;

    public TimeSpan Time { get; } = time;
}

public interface IMediaPlayback : IDisposable
{
    Task<bool> OpenAsync(MediaSource source, CancellationToken ct = default);

    MediaInfo? Info { get; }

    TimeSpan Duration { get; }

    TimeSpan Position { get; }

    bool IsPlaying { get; }

    double Volume { get; set; }

    void Play();

    void Pause();

    void Stop();

    void Seek(TimeSpan position);

    event EventHandler<TimeSpan>? PositionChanged;

    event EventHandler? Ended;

    event EventHandler<MediaFrame>? FrameReady;
}

public interface IMediaPlaybackFactory
{
    bool IsAvailable { get; }

    IMediaPlayback Create();
}

public sealed class UnavailableMediaPlaybackFactory : IMediaPlaybackFactory
{
    public bool IsAvailable => false;

    public IMediaPlayback Create() => throw new InvalidOperationException("No media playback backend is available.");
}

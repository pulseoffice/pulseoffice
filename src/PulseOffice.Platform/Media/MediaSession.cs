namespace PulseOffice.Platform.Media;

public sealed record MediaPlaybackSettings
{
    public bool Loop { get; init; }

    public double Volume { get; init; } = 1;

    public bool Muted { get; init; }

    public TimeSpan TrimStart { get; init; }

    public TimeSpan TrimEnd { get; init; }

    public TimeSpan FadeIn { get; init; }

    public TimeSpan FadeOut { get; init; }
}

public readonly record struct MediaWindow(TimeSpan Start, TimeSpan End)
{
    public TimeSpan Length => End - Start;
}

public sealed class MediaVolumeCurve
{
    public MediaWindow Window(MediaPlaybackSettings settings, TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var start = Clamp(settings.TrimStart, TimeSpan.Zero, duration);
        var end = Clamp(duration - settings.TrimEnd, TimeSpan.Zero, duration);
        return end > start ? new MediaWindow(start, end) : new MediaWindow(TimeSpan.Zero, duration);
    }

    public double GainAt(MediaPlaybackSettings settings, MediaWindow window, TimeSpan position)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.Muted) return 0;
        var level = Math.Clamp(settings.Volume, 0, 1);
        var intoWindow = position - window.Start;
        var beforeEnd = window.End - position;
        return level * Ramp(intoWindow, settings.FadeIn) * Ramp(beforeEnd, settings.FadeOut);
    }

    private static double Ramp(TimeSpan elapsed, TimeSpan fade) =>
        fade <= TimeSpan.Zero ? 1 : Math.Clamp(elapsed / fade, 0, 1);

    private static TimeSpan Clamp(TimeSpan value, TimeSpan low, TimeSpan high) =>
        value < low ? low : value > high ? high : value;
}

public sealed class MediaSession : IDisposable
{
    private readonly IMediaPlayback _playback;
    private readonly MediaVolumeCurve _curve;
    private MediaPlaybackSettings _settings;
    private bool _finished;

    public MediaSession(IMediaPlayback playback, MediaPlaybackSettings settings, MediaVolumeCurve curve)
    {
        ArgumentNullException.ThrowIfNull(playback);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(curve);
        _playback = playback;
        _settings = settings;
        _curve = curve;
        _playback.PositionChanged += OnPositionChanged;
        _playback.Ended += OnEnded;
    }

    public IMediaPlayback Playback => _playback;

    public MediaPlaybackSettings Settings => _settings;

    public MediaWindow Window => _curve.Window(_settings, _playback.Duration);

    public bool IsPlaying => _playback.IsPlaying;

    public TimeSpan Elapsed => Clamp(_playback.Position - Window.Start, Window.Length);

    public event EventHandler<TimeSpan>? Progress;

    public event EventHandler? Finished;

    public void Start()
    {
        _finished = false;
        var window = Window;
        _playback.Seek(window.Start);
        _playback.Volume = _curve.GainAt(_settings, window, window.Start);
        _playback.Play();
    }

    public void Toggle()
    {
        if (_playback.IsPlaying) _playback.Pause();
        else if (_finished) Start();
        else _playback.Play();
    }

    public void Pause() => _playback.Pause();

    public void Stop()
    {
        _playback.Stop();
        _finished = true;
    }

    public void SeekTo(double fraction)
    {
        var window = Window;
        var target = window.Start + window.Length * Math.Clamp(fraction, 0, 1);
        _finished = false;
        _playback.Seek(target);
        _playback.Volume = _curve.GainAt(_settings, window, target);
    }

    public void Update(MediaPlaybackSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
        _playback.Volume = _curve.GainAt(_settings, Window, _playback.Position);
    }

    public void Dispose()
    {
        _playback.PositionChanged -= OnPositionChanged;
        _playback.Ended -= OnEnded;
        _playback.Dispose();
    }

    private void OnPositionChanged(object? sender, TimeSpan position)
    {
        if (_finished) return;
        var window = Window;
        if (position >= window.End)
        {
            Reached(window);
            return;
        }

        _playback.Volume = _curve.GainAt(_settings, window, position);
        Progress?.Invoke(this, Clamp(position - window.Start, window.Length));
    }

    private void OnEnded(object? sender, EventArgs e)
    {
        if (!_finished) Reached(Window);
    }

    private void Reached(MediaWindow window)
    {
        if (_settings.Loop)
        {
            _playback.Seek(window.Start);
            _playback.Volume = _curve.GainAt(_settings, window, window.Start);
            if (!_playback.IsPlaying) _playback.Play();
            Progress?.Invoke(this, TimeSpan.Zero);
            return;
        }

        _playback.Pause();
        _playback.Volume = 0;
        _finished = true;
        Progress?.Invoke(this, window.Length);
        Finished?.Invoke(this, EventArgs.Empty);
    }

    private static TimeSpan Clamp(TimeSpan value, TimeSpan length) =>
        value < TimeSpan.Zero ? TimeSpan.Zero : value > length ? length : value;
}

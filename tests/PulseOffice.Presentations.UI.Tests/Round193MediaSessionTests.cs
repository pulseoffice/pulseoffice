using PulseOffice.Foundation.Network;
using PulseOffice.Platform;
using PulseOffice.Platform.Media;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round193MediaSessionTests
{
    private static readonly TimeSpan Ten = TimeSpan.FromSeconds(10);

    private sealed class FakePlayback(TimeSpan duration, bool opens = true) : IMediaPlayback
    {
        public List<TimeSpan> Seeks { get; } = [];
        public MediaSource? Opened { get; private set; }
        public bool Disposed { get; private set; }
        public MediaInfo? Info => new(duration, true, false, 0, 0);
        public TimeSpan Duration => duration;
        public TimeSpan Position { get; set; }
        public bool IsPlaying { get; private set; }
        public double Volume { get; set; } = 1;

        public event EventHandler<TimeSpan>? PositionChanged;
        public event EventHandler? Ended;
        public event EventHandler<MediaFrame>? FrameReady { add { } remove { } }

        public Task<bool> OpenAsync(MediaSource source, CancellationToken ct = default)
        {
            Opened = source;
            return Task.FromResult(opens);
        }

        public void Play() => IsPlaying = true;
        public void Pause() => IsPlaying = false;

        public void Stop()
        {
            IsPlaying = false;
            Position = TimeSpan.Zero;
        }

        public void Seek(TimeSpan position)
        {
            Seeks.Add(position);
            Position = position;
        }

        public void Dispose() => Disposed = true;

        public void Reach(TimeSpan position)
        {
            Position = position;
            PositionChanged?.Invoke(this, position);
        }

        public void End() => Ended?.Invoke(this, EventArgs.Empty);
    }

    private sealed class Factory(FakePlayback playback, bool available = true) : IMediaPlaybackFactory
    {
        public bool IsAvailable => available;
        public IMediaPlayback Create() => playback;
    }

    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    private static (MediaSession Session, FakePlayback Playback) Session(MediaPlaybackSettings settings)
    {
        var playback = new FakePlayback(Ten);
        return (new MediaSession(playback, settings, new MediaVolumeCurve()), playback);
    }

    [Fact]
    public void Trim_sets_the_window_and_an_impossible_trim_keeps_the_whole_medium()
    {
        var curve = new MediaVolumeCurve();

        Assert.Equal(new MediaWindow(S(2), S(7)), curve.Window(new MediaPlaybackSettings { TrimStart = S(2), TrimEnd = S(3) }, Ten));
        Assert.Equal(new MediaWindow(TimeSpan.Zero, Ten), curve.Window(new MediaPlaybackSettings { TrimStart = S(6), TrimEnd = S(6) }, Ten));
    }

    [Fact]
    public void Fades_are_a_volume_ramp_inside_the_trimmed_window()
    {
        var curve = new MediaVolumeCurve();
        var settings = new MediaPlaybackSettings { Volume = 0.8, TrimStart = S(1), TrimEnd = S(1), FadeIn = S(2), FadeOut = S(4) };
        var window = curve.Window(settings, Ten);

        Assert.Equal(0, curve.GainAt(settings, window, S(1)), 6);
        Assert.Equal(0.4, curve.GainAt(settings, window, S(2)), 6);
        Assert.Equal(0.8, curve.GainAt(settings, window, S(4)), 6);
        Assert.Equal(0.4, curve.GainAt(settings, window, S(7)), 6);
        Assert.Equal(0, curve.GainAt(settings, window, S(9)), 6);
        Assert.Equal(0, curve.GainAt(settings with { Muted = true }, window, S(4)), 6);
    }

    [Fact]
    public void Start_seeks_to_the_trim_start_and_plays_at_the_faded_in_volume()
    {
        var (session, playback) = Session(new MediaPlaybackSettings { TrimStart = S(3), FadeIn = S(1) });

        session.Start();

        Assert.Equal([S(3)], playback.Seeks);
        Assert.True(playback.IsPlaying);
        Assert.Equal(0, playback.Volume, 6);

        playback.Reach(S(3.5));
        Assert.Equal(0.5, playback.Volume, 6);
    }

    [Fact]
    public void Without_loop_the_trim_end_stops_the_medium_once()
    {
        var (session, playback) = Session(new MediaPlaybackSettings { TrimEnd = S(4) });
        var finished = 0;
        session.Finished += (_, _) => finished++;
        session.Start();

        playback.Reach(S(6));
        playback.End();

        Assert.False(playback.IsPlaying);
        Assert.Equal(1, finished);
    }

    [Fact]
    public void With_loop_the_trim_end_seeks_back_to_the_trim_start()
    {
        var (session, playback) = Session(new MediaPlaybackSettings { Loop = true, TrimStart = S(1), TrimEnd = S(2) });
        session.Start();

        playback.Reach(S(8));
        playback.End();

        Assert.Equal([S(1), S(1), S(1)], playback.Seeks);
        Assert.True(playback.IsPlaying);
    }

    [Fact]
    public void Toggle_after_the_end_starts_again_from_the_trim_start()
    {
        var (session, playback) = Session(new MediaPlaybackSettings { TrimStart = S(2) });
        session.Start();
        playback.End();

        session.Toggle();

        Assert.Equal([S(2), S(2)], playback.Seeks);
        Assert.True(playback.IsPlaying);
    }

    [Fact]
    public void Seeking_maps_the_bar_onto_the_trimmed_window()
    {
        var (session, playback) = Session(new MediaPlaybackSettings { TrimStart = S(2), TrimEnd = S(4) });

        session.SeekTo(0.5);

        Assert.Equal(S(4), playback.Seeks.Single());
    }

    [Fact]
    public async Task The_show_opens_an_embedded_medium_with_its_options_in_the_built_in_player()
    {
        var playback = new FakePlayback(Ten);
        var sessions = new ShowMediaSessions(
            new DeckMediaFiles(new ExecutableFileTypes(), new NetworkSharePaths()), new Factory(playback), new MediaVolumeCurve());
        var media = new MediaElement
        {
            Id = "m",
            Kind = MediaKind.Audio,
            Data = [1, 2, 3],
            MimeType = "audio/mpeg",
            Loop = true,
            Volume = 0.5,
            FadeIn = S(1),
        };

        using var session = await sessions.OpenAsync(media, null);

        Assert.NotNull(session);
        Assert.Equal(".mp3", Assert.IsType<StreamMediaSource>(playback.Opened).Extension);
        Assert.True(session.Settings.Loop);
        Assert.Equal(0.5, session.Settings.Volume);
        Assert.Equal(S(1), session.Settings.FadeIn);
    }

    [Fact]
    public async Task Round274_the_show_opens_an_embedded_transition_sound_with_its_loop()
    {
        var playback = new FakePlayback(Ten);
        var sessions = new ShowMediaSessions(
            new DeckMediaFiles(new ExecutableFileTypes(), new NetworkSharePaths()), new Factory(playback), new MediaVolumeCurve());

        using var session = await sessions.OpenSoundAsync(new EmbeddedSound("Media/chime.ogg", "audio/ogg", [1, 2]), loop: true);

        Assert.NotNull(session);
        Assert.Equal(".ogg", Assert.IsType<StreamMediaSource>(playback.Opened).Extension);
        Assert.True(session.Settings.Loop);
    }

    [Fact]
    public async Task Round274_an_empty_sound_opens_nothing() =>
        Assert.Null(await new ShowMediaSessions(new DeckMediaFiles(new ExecutableFileTypes(), new NetworkSharePaths()),
            new Factory(new FakePlayback(Ten)), new MediaVolumeCurve()).OpenSoundAsync(new EmbeddedSound("a.wav", "audio/wav", []), loop: false));

    [Fact]
    public async Task Without_a_backend_or_a_readable_medium_the_show_falls_back_to_the_system_player()
    {
        var media = new MediaElement { Id = "m", Kind = MediaKind.Video, Data = [1], MimeType = "video/mp4" };
        var files = new DeckMediaFiles(new ExecutableFileTypes(), new NetworkSharePaths());
        var refused = new FakePlayback(Ten, opens: false);

        Assert.Null(await new ShowMediaSessions(files, new Factory(new FakePlayback(Ten), available: false), new MediaVolumeCurve()).OpenAsync(media, null));
        Assert.Null(await new ShowMediaSessions(files, new Factory(refused), new MediaVolumeCurve()).OpenAsync(media, null));
        Assert.True(refused.Disposed);
    }
}

using PulseOffice.Platform.Media;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round194MediaTimelineTests
{
    private static TimeSpan Ms(int value) => TimeSpan.FromMilliseconds(value);

    [Fact]
    public void Without_audio_the_wall_clock_drives_the_picture()
    {
        var timeline = new MediaTimeline();
        timeline.Restart(Ms(2000));

        Assert.Equal(Ms(2300), timeline.Position(Ms(300), null));
    }

    [Fact]
    public void With_audio_the_picture_follows_the_samples_the_output_has_played()
    {
        var timeline = new MediaTimeline();
        timeline.Restart(Ms(1000));

        Assert.Equal(Ms(1000), timeline.Position(Ms(200), TimeSpan.Zero));
        Assert.Equal(Ms(1100), timeline.Position(Ms(400), Ms(100)));
        Assert.Equal(Ms(1150), timeline.Position(Ms(450), Ms(100)));
        Assert.Equal(Ms(1150), timeline.Position(Ms(460), Ms(120)));
        Assert.Equal(Ms(1700), timeline.Position(Ms(1000), Ms(700)));
    }

    [Fact]
    public void The_position_never_runs_backwards_when_the_audio_lags_the_interpolation()
    {
        var timeline = new MediaTimeline();
        timeline.Restart(TimeSpan.Zero);
        timeline.Position(Ms(100), Ms(100));
        var ahead = timeline.Position(Ms(300), Ms(100));

        Assert.Equal(ahead, timeline.Position(Ms(310), Ms(150)));
    }

    [Fact]
    public void Silent_start_waits_for_sound_then_the_end_of_audio_keeps_the_last_anchor()
    {
        var timeline = new MediaTimeline();
        timeline.Restart(TimeSpan.Zero);

        Assert.Equal(TimeSpan.Zero, timeline.Position(Ms(400), TimeSpan.Zero));
        Assert.Equal(Ms(100), timeline.Position(Ms(600), TimeSpan.Zero));

        var fresh = new MediaTimeline();
        fresh.Restart(TimeSpan.Zero);
        fresh.Position(Ms(1000), Ms(900));
        Assert.Equal(Ms(1400), fresh.Position(Ms(1500), null));
    }

    [Fact]
    public void A_pipe_player_counts_what_left_the_pipe_and_its_buffer()
    {
        var format = PcmFormat.Standard;
        var estimate = new AudioPlayedEstimate(format, Ms(100));

        estimate.Wrote(AudioPlayedEstimate.PipeCapacityBytes);
        Assert.Equal(TimeSpan.Zero, estimate.Played);

        estimate.Wrote(format.BytesPerSecond);
        Assert.Equal(Ms(900), estimate.Played);
    }
}

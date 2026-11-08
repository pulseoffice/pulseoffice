using PulseOffice.Presentations;
using PulseOffice.Presentations.UI;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class VideoSlideDurationTests
{
    private readonly VideoSlideDuration _durations = new();

    [Theory]
    [InlineData(int.MaxValue, VideoSlideDuration.LongestSlideMs)]
    [InlineData(4000, 4000)]
    [InlineData(0, VideoSlideDuration.DefaultSlideMs)]
    [InlineData(-10, VideoSlideDuration.DefaultSlideMs)]
    public void AutomaticTimingsAreBoundedSoTheVideoCannotRunForDays(int advanceAfterMs, int expected)
    {
        var slide = new Slide("s1", SlideLayout.Blank);
        slide.Transition = new SlideTransition { Advance = AdvanceMode.AfterTime, AdvanceAfterMs = advanceAfterMs };

        Assert.Equal(expected, _durations.Of(slide));
    }

    [Fact]
    public void ASlideAdvancedByClickLastsTheDefaultTime()
    {
        var slide = new Slide("s1", SlideLayout.Blank);

        Assert.Equal(VideoSlideDuration.DefaultSlideMs, _durations.Of(slide));
    }
}

using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class RehearsalTimelineTests
{
    [Fact]
    public void RecordsPerSlideAndSumsTotal()
    {
        var timeline = new RehearsalTimeline();
        timeline.Record("slide1", 3000);
        timeline.Record("slide2", 5000);

        Assert.True(timeline.HasTimings);
        Assert.Equal(2, timeline.Times.Count);
        Assert.Equal(3000, timeline.Times["slide1"]);
        Assert.Equal(8000, timeline.TotalMs);
    }

    [Fact]
    public void LastRecordWinsForTheSameSlide()
    {
        var timeline = new RehearsalTimeline();
        timeline.Record("slide1", 3000);
        timeline.Record("slide1", 1200);

        Assert.Equal(1200, timeline.Times["slide1"]);
        Assert.Equal(1200, timeline.TotalMs);
    }

    [Fact]
    public void NegativeElapsedIsClampedToZero()
    {
        var timeline = new RehearsalTimeline();
        timeline.Record("slide1", -50);

        Assert.Equal(0, timeline.Times["slide1"]);
    }
}

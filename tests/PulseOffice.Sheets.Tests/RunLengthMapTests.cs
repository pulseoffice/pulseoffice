using PulseOffice.Sheets;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public class RunLengthMapTests
{
    [Fact]
    public void AdjacentEqualValuesBecomeOneRun()
    {
        var map = new RunLengthMap<int>();
        map.Set(0, 4, 1);
        map.Set(5, 9, 1);
        map.Set(10, 10, 2);

        Assert.Equal([new IndexRun<int>(0, 9, 1), new IndexRun<int>(10, 10, 2)], map.Runs);
        Assert.Equal(11, map.Count);
    }

    [Fact]
    public void RemovingTheMiddleSplitsTheRun()
    {
        var map = new RunLengthMap<int>();
        map.Set(0, 9, 1);
        map.Remove(3, 5);

        Assert.Equal([new IndexRun<int>(0, 2, 1), new IndexRun<int>(6, 9, 1)], map.Runs);
        Assert.False(map.ContainsKey(4));
        Assert.True(map.TryGetValue(6, out var value) && value == 1);
    }

    [Fact]
    public void InsertingInsideARunOpensAGap()
    {
        var map = new RunLengthMap<int>();
        map.Set(0, 9, 1);
        map.Insert(4, 3, 100);

        Assert.Equal([new IndexRun<int>(0, 3, 1), new IndexRun<int>(7, 12, 1)], map.Runs);
    }

    [Fact]
    public void InsertingDropsWhatFallsBeyondTheLimit()
    {
        var map = new RunLengthMap<int>();
        map.Set(5, 9, 1);
        map.Insert(0, 3, 10);

        Assert.Equal([new IndexRun<int>(8, 10, 1)], map.Runs);
    }

    [Fact]
    public void DeletingClosesTheGapAndJoinsEqualNeighbours()
    {
        var map = new RunLengthMap<int>();
        map.Set(0, 2, 1);
        map.Set(3, 4, 2);
        map.Set(5, 7, 1);
        map.Delete(3, 2);

        Assert.Equal([new IndexRun<int>(0, 5, 1)], map.Runs);
    }

    [Fact]
    public void SegmentsCoverGapsAndRuns()
    {
        var map = new RunLengthMap<int>();
        map.Set(2, 3, 7);

        var segments = map.Segments(0, 5).Select(segment => (segment.First, segment.Last, segment.Present)).ToList();

        Assert.Equal([(0, 1, false), (2, 3, true), (4, 5, false)], segments);
    }
}

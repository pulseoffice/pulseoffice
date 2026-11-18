using System.Diagnostics;
using Xunit;

namespace PulseOffice.Testing;

public sealed class GrowthCheck
{
    private readonly TimeSpan _floor;
    private readonly double _slack;

    public GrowthCheck() : this(TimeSpan.FromMilliseconds(10), 4)
    {
    }

    public GrowthCheck(TimeSpan floor, double slack)
    {
        _floor = floor;
        _slack = slack;
    }

    public TimeSpan Time(Action action)
    {
        var clock = Stopwatch.StartNew();
        action();
        return clock.Elapsed;
    }

    public void Linear(Func<int, Action> arrange, int largeSize, int factor = 10)
    {
        var smallSize = Math.Max(1, largeSize / factor);
        Time(arrange(smallSize));
        var small = Time(arrange(smallSize));
        var large = Time(arrange(largeSize));
        Within(small, large, (double)largeSize / smallSize, $"size {smallSize}: {small}, size {largeSize}: {large}");
    }

    public void Flat(Func<int, Action> arrange, int smallSize, int largeSize)
    {
        Time(arrange(smallSize));
        var small = Time(arrange(smallSize));
        var large = Time(arrange(largeSize));
        Within(small, large, 1, $"extent {smallSize}: {small}, extent {largeSize}: {large}");
    }

    public void Bounded(TimeSpan small, TimeSpan large, double expectedRatio, string label) =>
        Within(small, large, expectedRatio, label);

    private void Within(TimeSpan small, TimeSpan large, double expectedRatio, string label)
    {
        var reference = small > _floor ? small : _floor;
        Assert.True(large.TotalMilliseconds <= reference.TotalMilliseconds * expectedRatio * _slack, label);
    }
}

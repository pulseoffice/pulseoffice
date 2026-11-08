namespace PulseOffice.Platform;

public interface IActiveDisplayProbe
{
    bool HasActiveDisplay();
}

public sealed class AlwaysActiveDisplayProbe : IActiveDisplayProbe
{
    public bool HasActiveDisplay() => true;
}

public interface IDisplayWaitClock
{
    TimeSpan Elapsed { get; }

    void Pause(TimeSpan interval);
}

public sealed class SystemDisplayWaitClock : IDisplayWaitClock
{
    private readonly System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

    public TimeSpan Elapsed => stopwatch.Elapsed;

    public void Pause(TimeSpan interval) => Thread.Sleep(interval);
}

public enum DisplayReadinessOutcome
{
    ReadyImmediately,
    ReadyAfterWaiting,
    TimedOut,
}

public sealed class DisplayReadinessGate(IActiveDisplayProbe probe, IDisplayWaitClock clock)
{
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(500);

    public DisplayReadinessOutcome WaitForActiveDisplay(TimeSpan timeout)
    {
        if (probe.HasActiveDisplay()) return DisplayReadinessOutcome.ReadyImmediately;
        while (clock.Elapsed < timeout)
        {
            clock.Pause(PollInterval);
            if (probe.HasActiveDisplay()) return DisplayReadinessOutcome.ReadyAfterWaiting;
        }
        return DisplayReadinessOutcome.TimedOut;
    }
}

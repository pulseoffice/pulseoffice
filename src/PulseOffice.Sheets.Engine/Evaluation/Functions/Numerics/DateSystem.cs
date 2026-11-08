namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;

public interface IDateSystem
{
    DateOnly Epoch { get; }

    double ToSerial(DateTime value);
    DateTime FromSerial(double serial);

    double TimeToFraction(int hours, int minutes, double seconds);
}

public sealed class StandardDateSystem : IDateSystem
{
    public DateOnly Epoch { get; } = new(1899, 12, 30);

    public double ToSerial(DateTime value)
    {
        var days = (value.Date - Epoch.ToDateTime(TimeOnly.MinValue)).TotalDays;
        return days + value.TimeOfDay.TotalDays;
    }

    public DateTime FromSerial(double serial)
    {
        var whole = System.Math.Floor(serial);
        var fraction = serial - whole;
        return Epoch.ToDateTime(TimeOnly.MinValue)
                    .AddDays(whole)

                    .AddMilliseconds(System.Math.Round(fraction * 86_400_000));
    }

    public double TimeToFraction(int hours, int minutes, double seconds) =>
        (hours * 3600.0 + minutes * 60.0 + seconds) / 86_400.0;
}

public interface ISystemClock
{
    DateTime Now { get; }
    DateOnly Today { get; }
}

public sealed class SystemClock(TimeProvider time) : ISystemClock
{
    public SystemClock() : this(TimeProvider.System) { }

    public DateTime Now => time.GetLocalNow().DateTime;

    public DateOnly Today => DateOnly.FromDateTime(Now);
}

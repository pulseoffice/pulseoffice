namespace PulseOffice.Sheets;

public interface IDateEpoch
{
    DateOnly Current { get; }

    IDisposable Enter(DateOnly epoch);

    DateTime ToDate(double serial) => Current.ToDateTime(TimeOnly.MinValue).AddDays(serial);
}

public interface IDefaultDateEpoch
{
    DateOnly Epoch { get; }
}

public sealed class StandardDefaultDateEpoch : IDefaultDateEpoch
{
    public DateOnly Epoch => CalculationSettings.StandardDateEpoch;
}

public sealed class AmbientDateEpoch(IDefaultDateEpoch defaults) : IDateEpoch
{
    private readonly AsyncLocal<DateOnly?> _entered = new();

    public AmbientDateEpoch() : this(new StandardDefaultDateEpoch())
    {
    }

    public DateOnly Current => _entered.Value ?? defaults.Epoch;

    public IDisposable Enter(DateOnly epoch)
    {
        var previous = _entered.Value;
        _entered.Value = epoch;
        return new Restore(this, previous);
    }

    private sealed class Restore(AmbientDateEpoch owner, DateOnly? previous) : IDisposable
    {
        public void Dispose() => owner._entered.Value = previous;
    }
}

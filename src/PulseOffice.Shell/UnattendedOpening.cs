namespace PulseOffice.Shell;

public interface IUnattendedOpening
{
    bool IsActive { get; }

    IReadOnlyDictionary<string, string> Options { get; }

    IDisposable Begin(IReadOnlyDictionary<string, string> options);
}

public sealed class UnattendedOpening : IUnattendedOpening
{
    private readonly AsyncLocal<IReadOnlyDictionary<string, string>?> _current = new();

    public bool IsActive => _current.Value is not null;

    public IReadOnlyDictionary<string, string> Options => _current.Value ?? new Dictionary<string, string>();

    public IDisposable Begin(IReadOnlyDictionary<string, string> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var previous = _current.Value;
        _current.Value = options;
        return new Scope(() => _current.Value = previous);
    }

    private sealed class Scope(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}

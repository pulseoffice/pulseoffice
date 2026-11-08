namespace PulseOffice.Scripting;

public interface IScriptOutput
{
    void Write(string message);

    IDisposable Capture(Action<string> sink);
}

public sealed class ScriptOutput : IScriptOutput
{
    private readonly Lock _gate = new();

    private readonly List<Release> _captures = [];

    public void Write(string message)
    {
        Action<string>? sink;

        lock (_gate) sink = _captures.Count == 0 ? null : _captures[^1].Sink;

        sink?.Invoke(message);
    }

    public IDisposable Capture(Action<string> sink)
    {
        ArgumentNullException.ThrowIfNull(sink);

        var capture = new Release(this, sink);

        lock (_gate) _captures.Add(capture);

        return capture;
    }

    private void Stop(Release capture)
    {
        lock (_gate) _captures.Remove(capture);
    }

    private sealed class Release(ScriptOutput output, Action<string> sink) : IDisposable
    {
        public Action<string> Sink { get; } = sink;

        public void Dispose() => output.Stop(this);
    }
}

public sealed class ScriptTranscript(int maximumCharacters = ScriptTranscript.DefaultMaximumCharacters)
{
    public const int DefaultMaximumCharacters = 1_000_000;

    private readonly Lock _gate = new();
    private readonly List<string> _lines = [];
    private int _length;
    private bool _truncated;

    public bool Truncated
    {
        get
        {
            lock (_gate) return _truncated;
        }
    }

    public IReadOnlyList<string> Lines
    {
        get
        {
            lock (_gate) return [.. _lines];
        }
    }

    public void Add(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        lock (_gate)
        {
            var room = maximumCharacters - _length;
            if (line.Length <= room)
            {
                _lines.Add(line);
                _length += line.Length;
                return;
            }

            _truncated = true;
            if (room <= 0) return;
            _lines.Add(line[..room]);
            _length = maximumCharacters;
        }
    }
}

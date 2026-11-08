using System.Globalization;
using System.Xml.Linq;

namespace PulseOffice.Formats.Odf.Odt;

internal sealed record OdtListThread(string Raw, bool Restarts);

internal sealed class OdtListContinuity : OdtXml
{
    private const string ReadPrefix = "odt-list-";
    private const string WrittenPrefix = "pulse-list-";

    private Scope _scope = new();
    private int _read;
    private int _written;

    public IDisposable Story()
    {
        var outer = _scope;
        _scope = new Scope();
        return new StoryScope(this, outer);
    }

    public OdtListThread Read(string? style, string? declared, bool continuesNumbering, string? continuesList)
    {
        var key = style ?? string.Empty;
        OdtListThread thread;
        if (continuesList is not null && _scope.Declared.TryGetValue(continuesList, out var named))
            thread = new OdtListThread(named, false);
        else if ((continuesNumbering || continuesList is not null) && _scope.Latest.TryGetValue(key, out var latest))
            thread = new OdtListThread(latest, false);
        else
            thread = new OdtListThread(ReadPrefix + (++_read).ToString(CultureInfo.InvariantCulture), style is not null && _scope.Latest.ContainsKey(key));
        _scope.Latest[key] = thread.Raw;
        if (declared is { Length: > 0 }) _scope.Declared[declared] = thread.Raw;
        return thread;
    }

    public void Declare(string? declared, string raw)
    {
        if (declared is { Length: > 0 }) _scope.Declared.TryAdd(declared, raw);
    }

    public void Write(XElement list, string style, string? thread)
    {
        var key = (style, thread ?? string.Empty);
        if (_scope.Written.TryGetValue(key, out var previous))
        {
            if (_scope.LatestWritten.TryGetValue(style, out var latest) && latest == key.Item2)
                list.Add(new XAttribute(Text + "continue-numbering", "true"));
            else
                list.Add(new XAttribute(Text + "continue-list", Identify(previous)));
        }
        _scope.Written[key] = list;
        _scope.LatestWritten[style] = key.Item2;
    }

    private string Identify(XElement list)
    {
        if ((string?)list.Attribute(Xml + "id") is { Length: > 0 } existing) return existing;
        var id = WrittenPrefix + (++_written).ToString(CultureInfo.InvariantCulture);
        list.SetAttributeValue(Xml + "id", id);
        return id;
    }

    private sealed class Scope
    {
        public Dictionary<string, string> Latest { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> Declared { get; } = new(StringComparer.Ordinal);

        public Dictionary<(string Style, string Thread), XElement> Written { get; } = [];

        public Dictionary<string, string> LatestWritten { get; } = new(StringComparer.Ordinal);
    }

    private sealed class StoryScope(OdtListContinuity owner, Scope outer) : IDisposable
    {
        public void Dispose() => owner._scope = outer;
    }
}

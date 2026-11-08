using System.Text.RegularExpressions;

namespace PulseOffice.Formats;

public abstract partial class FunctionNameTable
{
    private readonly Dictionary<string, string> _exported = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _imported = new(StringComparer.OrdinalIgnoreCase);

    private readonly IReadOnlyList<string> _prefixes;

    protected FunctionNameTable(IEnumerable<(string Internal, string External)> pairs, IReadOnlyList<string> prefixes)
    {
        _prefixes = prefixes;
        ArgumentNullException.ThrowIfNull(pairs);
        foreach (var (name, external) in pairs)
        {
            _exported.TryAdd(name, external);
            _imported.TryAdd(external, name);
        }
    }

    public string Export(string segment) => Rename(segment, _exported);

    public string Import(string segment) => Rename(segment, _imported, strip: true);

    private string Rename(string segment, Dictionary<string, string> names, bool strip = false) =>
        segment.Contains('(') ? Call().Replace(segment, match =>
        {
            var name = match.Groups["name"].Value;
            var renamed = Renamed(name, names, strip);
            return renamed is null ? match.Value : renamed + match.Groups["space"].Value + "(";
        }) : segment;

    private string? Renamed(string name, Dictionary<string, string> names, bool strip)
    {
        var current = name;
        while (true)
        {
            if (names.TryGetValue(current, out var renamed)) return renamed;
            if (!strip || Prefix(current) is not { } found) return ReferenceEquals(current, name) ? null : current;
            current = current[found.Length..];
        }
    }

    private string? Prefix(string name) =>
        _prefixes.FirstOrDefault(prefix => name.Length > prefix.Length && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex(@"(?<![A-Za-z0-9_.$\]])(?<name>[A-Za-z_][A-Za-z0-9_.]*)(?<space>\s*)\(", RegexOptions.CultureInvariant)]
    private static partial Regex Call();
}

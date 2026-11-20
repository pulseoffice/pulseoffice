using System.Xml.Linq;
using PulseOffice.Sheets.Addressing;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal sealed record StyleMapRegion(string Style, CellRange Range);

internal sealed class StyleMapConditionText
{
    private readonly IReadOnlyList<(string Prefix, string Replacement)> _forms =
    [
        ("cell-content-is-not-between(", "not-between("),
        ("cell-content-is-between(", "between("),
        ("is-true-formula(", "formula-is("),
        ("cell-content()", string.Empty),
    ];

    public string? ToConditionValue(string? condition)
    {
        var text = condition?.Trim() ?? string.Empty;
        if (text.StartsWith("of:", StringComparison.Ordinal)) text = text[3..];
        foreach (var (prefix, replacement) in _forms)
        {
            if (!text.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var rest = text[prefix.Length..];
            if (replacement == "formula-is(" && rest.StartsWith("of:", StringComparison.Ordinal)) rest = rest[3..].TrimStart('=');
            var value = replacement + rest.Trim();
            return value.Length == 0 ? null : value;
        }
        return null;
    }
}

internal sealed class OdsStyleMaps
{
    private readonly Dictionary<string, IReadOnlyList<XElement>> _maps = new(StringComparer.Ordinal);
    private readonly Dictionary<XElement, List<StyleMapRegion>> _regions = [];
    private readonly Dictionary<XElement, Dictionary<(string Style, int Left, int Right), int>> _open = [];
    private readonly TableCells _cells = new();
    private readonly XName _styleName = Namespaces.Table + "style-name";

    public void Load(params XDocument?[] documents)
    {
        foreach (var style in documents.OfType<XDocument>().SelectMany(document => document.Descendants(Namespaces.Style + "style")))
        {
            if ((string?)style.Attribute(Namespaces.Style + "family") != "table-cell" || (string?)style.Attribute(Namespaces.Style + "name") is not { } name) continue;
            var maps = style.Elements(Namespaces.Style + "map").ToList();
            if (maps.Count > 0) _maps[name] = maps;
        }
    }

    public IReadOnlyList<XElement> MapsOf(string style) => _maps.GetValueOrDefault(style) ?? [];

    public void Collect(XElement table, TableRow row)
    {
        if (_maps.Count == 0 || row.Index >= CellAddress.MaxRows) return;
        var bottom = (int)Math.Min((long)row.Index + row.Repeat - 1, CellAddress.MaxRows - 1);
        (string Style, int Left, int Right)? run = null;
        foreach (var cell in _cells.Of(row))
        {
            if (cell.Address.Column >= CellAddress.MaxColumns) break;
            var right = (int)Math.Min((long)cell.Address.Column + cell.Repeat - 1, CellAddress.MaxColumns - 1);
            var style = (string?)cell.Element.Attribute(_styleName);
            if (style is not null && _maps.ContainsKey(style) && run is { } current && current.Style == style && current.Right == cell.Address.Column - 1)
            {
                run = (current.Style, current.Left, right);
                continue;
            }
            if (run is { } finished) Add(table, finished, row.Index, bottom);
            run = style is not null && _maps.ContainsKey(style) ? (style, cell.Address.Column, right) : null;
        }
        if (run is { } last) Add(table, last, row.Index, bottom);
    }

    public IReadOnlyList<StyleMapRegion> RegionsOf(XElement table) => _regions.GetValueOrDefault(table) ?? [];

    private void Add(XElement table, (string Style, int Left, int Right) run, int top, int bottom)
    {
        if (!_regions.TryGetValue(table, out var regions)) _regions[table] = regions = [];
        if (!_open.TryGetValue(table, out var open)) _open[table] = open = [];
        if (open.TryGetValue(run, out var index) && regions[index].Range.End.Row == top - 1)
        {
            var range = regions[index].Range;
            regions[index] = regions[index] with { Range = new CellRange(range.Start, new CellAddress(bottom, range.End.Column)) };
            return;
        }
        open[run] = regions.Count;
        regions.Add(new StyleMapRegion(run.Style, new CellRange(new CellAddress(top, run.Left), new CellAddress(bottom, run.Right))));
    }
}

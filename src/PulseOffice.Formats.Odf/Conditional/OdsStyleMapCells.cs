using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Sheets.Addressing;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal sealed record StyleMapGroup(IReadOnlyList<CellRange> Ranges, IReadOnlyList<XElement> Maps);

internal sealed class OdsStyleMapCells
{
    private readonly Dictionary<(string Base, string Key), string> _derived = [];
    private readonly Dictionary<string, IReadOnlyList<XElement>> _mapsByKey = new(StringComparer.Ordinal);
    private readonly XName _styleName = Namespaces.Table + "style-name";
    private readonly XName _rowsRepeated = Namespaces.Table + "number-rows-repeated";
    private readonly XName _columnsRepeated = Namespaces.Table + "number-columns-repeated";
    private IReadOnlyList<StyleMapGroup> _groups = [];
    private Func<int, string?> _columnStyle = _ => null;

    public void Begin(IReadOnlyList<StyleMapGroup> groups, Func<int, string?> columnStyle)
    {
        _groups = groups;
        _columnStyle = columnStyle;
    }

    public IEnumerable<OdsRowSpan> Apply(IEnumerable<OdsRowSpan> spans)
    {
        foreach (var span in spans)
        {
            if (_groups.Count == 0)
            {
                yield return span;
                continue;
            }
            foreach (var piece in Split(span)) yield return Styled(piece);
        }
    }

    public IEnumerable<XElement> Declarations(IReadOnlyList<XElement> automaticStyles)
    {
        var automatic = automaticStyles
            .Where(style => style.Name == Namespaces.Style + "style" && (string?)style.Attribute(Namespaces.Style + "family") == "table-cell")
            .GroupBy(style => (string?)style.Attribute(Namespaces.Style + "name") ?? string.Empty, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (var ((baseName, key), name) in _derived)
        {
            XElement style;
            if (automatic.TryGetValue(baseName, out var source))
            {
                style = new XElement(source);
                style.Elements(Namespaces.Style + "map").Remove();
                style.SetAttributeValue(Namespaces.Style + "name", name);
            }
            else
            {
                style = new XElement(Namespaces.Style + "style",
                    new XAttribute(Namespaces.Style + "name", name),
                    new XAttribute(Namespaces.Style + "family", "table-cell"),
                    new XAttribute(Namespaces.Style + "parent-style-name", baseName));
            }
            style.Add(_mapsByKey[key].Select(map => new XElement(map)));
            yield return style;
        }
    }

    private IEnumerable<OdsRowSpan> Split(OdsRowSpan span)
    {
        var cuts = new SortedSet<int>();
        foreach (var range in _groups.SelectMany(group => group.Ranges))
        {
            if (range.Start.Row > span.First && range.Start.Row <= span.Last) cuts.Add(range.Start.Row);
            if (range.End.Row >= span.First && range.End.Row < span.Last) cuts.Add(range.End.Row + 1);
        }
        if (cuts.Count == 0)
        {
            yield return span;
            yield break;
        }
        var first = span.First;
        foreach (var cut in cuts.Append(span.Last + 1))
        {
            var element = new XElement(span.Element);
            element.Attribute(_rowsRepeated)?.Remove();
            if (cut - first > 1) element.Add(new XAttribute(_rowsRepeated, (cut - first).ToString(CultureInfo.InvariantCulture)));
            yield return new OdsRowSpan(first, cut - 1, element);
            first = cut;
        }
    }

    private OdsRowSpan Styled(OdsRowSpan span)
    {
        var groups = Enumerable.Range(0, _groups.Count)
            .Where(index => _groups[index].Ranges.Any(range => range.Start.Row <= span.First && range.End.Row >= span.First))
            .ToList();
        if (groups.Count == 0) return span;

        var cuts = new SortedSet<int>();
        var right = 0;
        foreach (var range in groups.SelectMany(index => _groups[index].Ranges).Where(range => range.Start.Row <= span.First && range.End.Row >= span.First))
        {
            cuts.Add(range.Start.Column);
            cuts.Add(range.End.Column + 1);
            right = Math.Max(right, range.End.Column + 1);
        }

        var cells = span.Element.Elements().Where(IsCell).ToList();
        var covered = cells.Sum(Repeat);
        if (covered < right)
        {
            var filler = new XElement(Namespaces.Table + "table-cell");
            if (right - covered > 1) filler.Add(new XAttribute(_columnsRepeated, (right - covered).ToString(CultureInfo.InvariantCulture)));
            span.Element.Add(filler);
            cells.Add(filler);
        }

        var column = 0;
        foreach (var cell in cells)
        {
            var first = column;
            var count = Repeat(cell);
            column = (int)Math.Min((long)first + count, int.MaxValue);
            var pieces = Pieces(cell, first, count, cuts);
            foreach (var (piece, start) in pieces) Map(piece, start, span.First, groups);
            if (pieces.Count > 1) cell.ReplaceWith(pieces.Select(piece => piece.Element));
        }
        return span;
    }

    private List<(XElement Element, int Start)> Pieces(XElement cell, int first, int count, SortedSet<int> cuts)
    {
        if (count <= 1) return [(cell, first)];
        var inside = cuts.GetViewBetween(first + 1, first + count - 1);
        if (inside.Count == 0) return [(cell, first)];
        var pieces = new List<(XElement, int)>();
        var start = first;
        foreach (var cut in inside.Append(first + count))
        {
            var piece = new XElement(cell);
            piece.Attribute(_columnsRepeated)?.Remove();
            if (cut - start > 1) piece.Add(new XAttribute(_columnsRepeated, (cut - start).ToString(CultureInfo.InvariantCulture)));
            pieces.Add((piece, start));
            start = cut;
        }
        return pieces;
    }

    private void Map(XElement cell, int column, int row, IReadOnlyList<int> groups)
    {
        var address = new CellAddress(row, column);
        var covering = groups.Where(index => _groups[index].Ranges.Any(range => range.Contains(address))).ToList();
        if (covering.Count == 0) return;
        var key = string.Join(',', covering);
        if (!_mapsByKey.ContainsKey(key)) _mapsByKey[key] = [.. covering.SelectMany(index => _groups[index].Maps)];
        var baseName = (string?)cell.Attribute(_styleName) ?? _columnStyle(column) ?? "Default";
        if (!_derived.TryGetValue((baseName, key), out var name))
            _derived[(baseName, key)] = name = $"cf{_derived.Count + 1}";
        cell.SetAttributeValue(_styleName, name);
    }

    private bool IsCell(XElement element) =>
        element.Name == Namespaces.Table + "table-cell" || element.Name == Namespaces.Table + "covered-table-cell";

    private int Repeat(XElement cell) =>
        int.TryParse((string?)cell.Attribute(_columnsRepeated), NumberStyles.Integer, CultureInfo.InvariantCulture, out var repeat) && repeat > 0 ? repeat : 1;
}

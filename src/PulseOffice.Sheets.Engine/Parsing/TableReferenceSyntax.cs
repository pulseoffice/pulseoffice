using System.Text;

namespace PulseOffice.Sheets.Engine.Parsing;

public sealed class TableReferenceSyntax
{
    private readonly IReadOnlyList<(string Text, TableArea Area)> _specials =
    [
        ("#All", TableArea.All),
        ("#Data", TableArea.Data),
        ("#Headers", TableArea.Headers),
        ("#Totals", TableArea.Totals),
        ("#This Row", TableArea.ThisRow),
    ];

    public TableReferenceNode Parse(string text, int position)
    {
        ArgumentNullException.ThrowIfNull(text);
        var open = text.IndexOf('[', StringComparison.Ordinal);
        if (open <= 0 || text[^1] != ']') throw new FormulaSyntaxException($"Invalid table reference '{text}'", position);

        var items = Items(text[(open + 1)..^1].Trim(), position);
        var area = (TableArea)0;
        var columns = new List<string>();
        foreach (var (item, special) in items)
        {
            if (!special)
            {
                columns.Add(item);
                continue;
            }
            var match = _specials.FirstOrDefault(entry => string.Equals(entry.Text, item, StringComparison.OrdinalIgnoreCase));
            if (match.Text is null) throw new FormulaSyntaxException($"Unknown table item '{item}'", position);
            area |= match.Area;
        }

        if (area.HasFlag(TableArea.ThisRow)) area = TableArea.ThisRow;
        else if (area == 0) area = TableArea.Data;
        return new TableReferenceNode(text[..open], area, columns.Count == 0 ? null : columns[0], columns.Count == 0 ? null : columns[^1]);
    }

    public string Print(TableReferenceNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var specials = Specials(node.Area).Select(special => $"[{special}]").ToList();
        var single = node.FirstColumn is { } first && (node.LastColumn is null || string.Equals(first, node.LastColumn, StringComparison.OrdinalIgnoreCase));

        var builder = new StringBuilder(node.Table).Append('[');
        if (node.FirstColumn is null && specials.Count == 0) builder.Append("#Data");
        else if (single && specials.Count == 0) builder.Append(Escaped(node.FirstColumn!));
        else
        {
            if (node.FirstColumn is { } column)
                specials.Add(single ? $"[{Escaped(column)}]" : $"[{Escaped(column)}]:[{Escaped(node.LastColumn!)}]");
            builder.Append(string.Join(",", specials));
        }
        return builder.Append(']').ToString();
    }

    public int End(string text, int open)
    {
        ArgumentNullException.ThrowIfNull(text);
        var depth = 0;
        for (var index = open; index < text.Length; index++)
        {
            if (text[index] == '\'') { index++; continue; }
            if (text[index] == '[') depth++;
            else if (text[index] == ']' && --depth == 0) return index + 1;
        }
        return text.Length;
    }

    private IEnumerable<string> Specials(TableArea area)
    {
        if (area == TableArea.Data) yield break;
        if (area.HasFlag(TableArea.ThisRow))
        {
            yield return "#This Row";
            yield break;
        }
        if (area == TableArea.All)
        {
            yield return "#All";
            yield break;
        }
        if (area.HasFlag(TableArea.Headers)) yield return "#Headers";
        if (area.HasFlag(TableArea.Data)) yield return "#Data";
        if (area.HasFlag(TableArea.Totals)) yield return "#Totals";
    }

    private List<(string Text, bool Special)> Items(string specifier, int position)
    {
        if (specifier.Length == 0) return [];
        if (specifier[0] == '@')
        {
            var rest = specifier[1..].Trim();
            if (rest.Length == 0) return [("#This Row", true)];
            if (rest[0] != '[') return [("#This Row", true), (Unescape(rest), false)];
            return [("#This Row", true), .. Bracketed(rest, position)];
        }
        if (specifier[0] != '[') return [(Unescape(specifier), specifier[0] == '#')];
        return Bracketed(specifier, position);
    }

    private List<(string Text, bool Special)> Bracketed(string specifier, int position)
    {
        var items = new List<(string, bool)>();
        var index = 0;
        while (index < specifier.Length)
        {
            var c = specifier[index];
            if (c is ',' or ';' or ':' or ' ') { index++; continue; }
            if (c != '[') throw new FormulaSyntaxException($"Invalid table reference item at '{specifier[index..]}'", position);
            var close = Closing(specifier, index) ?? throw new FormulaSyntaxException("Unterminated table reference item", position);
            var raw = specifier[(index + 1)..close];
            var text = Unescape(raw);
            items.Add((text, raw.StartsWith('#')));
            index = close + 1;
        }
        return items;
    }

    private int? Closing(string text, int open)
    {
        for (var index = open + 1; index < text.Length; index++)
        {
            if (text[index] == '\'') { index++; continue; }
            if (text[index] == ']') return index;
        }
        return null;
    }

    private string Unescape(string text)
    {
        var builder = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\'' && index + 1 < text.Length) index++;
            builder.Append(text[index]);
        }
        return builder.ToString();
    }

    private string Escaped(string column)
    {
        var builder = new StringBuilder(column.Length);
        foreach (var c in column)
        {
            if (c is '[' or ']' or '#' or '\'') builder.Append('\'');
            builder.Append(c);
        }
        return builder.ToString();
    }
}

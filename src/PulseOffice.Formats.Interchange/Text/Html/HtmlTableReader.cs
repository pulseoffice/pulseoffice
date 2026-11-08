using PulseOffice.Foundation.Colors;
using System.Globalization;
using System.Text;

namespace PulseOffice.Formats.Interchange.Text.Html;

public sealed record HtmlTableCell(string Text)
{
    public bool Bold { get; init; }

    public bool Italic { get; init; }

    public bool Underline { get; init; }

    public string? ColorHex { get; init; }

    public string? BackgroundHex { get; init; }

    public HtmlCellEdges? Borders { get; init; }

    public int ColumnSpan { get; init; } = 1;

    public int RowSpan { get; init; } = 1;

    public HtmlTextAlignment Alignment { get; init; }

    public HtmlVerticalAlignment VerticalAlignment { get; init; }
}

public enum HtmlVerticalAlignment
{
    Unspecified,
    Top,
    Middle,
    Bottom,
}

public enum HtmlTextAlignment
{
    Unspecified,
    Left,
    Center,
    Right,
    Justify,
}

public sealed record HtmlTable(IReadOnlyList<IReadOnlyList<HtmlTableCell?>> Rows)
{
    public string? Caption { get; init; }

    public int ColumnCount => Rows.Count == 0 ? 0 : Rows.Max(row => row.Count);
}

public interface IHtmlTableReader
{
    HtmlTable? FirstTable(string html);
}

public sealed class HtmlTableReader(IHtmlTokenizer tokenizer, ICssDeclarations css, IHtmlCellBorders borders) : IHtmlTableReader
{
    public const long MaximumCells = 1_000_000;
    private const int MaximumInlineDepth = 256;

    public HtmlTable? FirstTable(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;

        var builder = new TableBuilder(css, borders);
        var depth = 0;
        foreach (var token in tokenizer.Tokenize(html))
        {
            switch (token)
            {
                case HtmlStartTag { Name: var name } start when Is(name, "table"):
                    depth++;
                    if (depth == 1) builder.Open(start);
                    else builder.OpenNested();
                    break;
                case HtmlEndTag { Name: var name } when Is(name, "table"):
                    if (depth == 0) break;
                    depth--;
                    if (depth == 0) return builder.Build();
                    builder.CloseNested();
                    break;
                case HtmlStartTag start when depth == 1:
                    if (builder.Full) return builder.Build();
                    builder.Start(start);
                    break;
                case HtmlEndTag end when depth == 1:
                    builder.End(end.Name);
                    break;
                case HtmlStartTag start when depth > 1:
                    builder.StartNested(start);
                    break;
                case HtmlText text when depth >= 1:
                    builder.Text(text.Text);
                    break;
            }
        }

        return depth > 0 ? builder.Build() : null;
    }

    private static bool Is(string name, string expected) => string.Equals(name, expected, StringComparison.OrdinalIgnoreCase);

    private sealed class TableBuilder(ICssDeclarations css, IHtmlCellBorders borders)
    {
        private readonly List<List<HtmlTableCell?>> _rows = [];
        private readonly Dictionary<(int Row, int Column), bool> _covered = [];
        private readonly Stack<Formatting> _formatting = new();
        private readonly List<(string Tag, Formatting Shape)> _inline = [];
        private readonly Stack<bool> _nestedRows = new();
        private StringBuilder? _text;
        private Formatting _cell = Formatting.Plain;
        private Formatting? _written;
        private (int Columns, int Rows) _span = (1, 1);
        private long _cells;
        private bool _bordered;
        private HtmlCellEdges? _edges;
        private StringBuilder? _caption;
        private string? _captionText;

        public bool Full => _cells >= MaximumCells || _rows.Count >= PulseOffice.Sheets.Addressing.CellAddress.MaxRows;

        private Formatting Inline => _inline.Count > 0 ? _inline[^1].Shape : _cell;

        public void Open(HtmlStartTag table)
        {
            _bordered = borders.TableBordered(table);
            _formatting.Push(Formatting.Plain.With(table, css));
        }

        public void OpenNested()
        {
            NewLine();
            _nestedRows.Push(false);
        }

        public void CloseNested()
        {
            if (_nestedRows.Count > 0) _nestedRows.Pop();
            NewLine();
        }

        public void StartNested(HtmlStartTag tag)
        {
            if (_text is null || _nestedRows.Count == 0) return;
            switch (tag.Name.ToLowerInvariant())
            {
                case "tr":
                    NewLine();
                    _nestedRows.Pop();
                    _nestedRows.Push(false);
                    break;
                case "td" or "th":
                    if (_nestedRows.Pop() && _text.Length > 0) _text.Append('\t');
                    _nestedRows.Push(true);
                    break;
                case "br":
                    _text.Append(' ');
                    break;
            }
        }

        private void NewLine()
        {
            if (_text is { Length: > 0 } && _text[^1] != '\n')
            {
                while (_text.Length > 0 && _text[^1] == ' ') _text.Length--;
                _text.Append('\n');
            }
        }

        public void Start(HtmlStartTag tag)
        {
            var name = tag.Name.ToLowerInvariant();
            switch (name)
            {
                case "caption" when _captionText is null:
                    CloseCell();
                    _caption = new StringBuilder();
                    break;
                case "tr":
                    CloseCell();
                    _rows.Add([]);
                    break;
                case "td" or "th":
                    CloseCell();
                    if (_rows.Count == 0) _rows.Add([]);
                    var inherited = _formatting.Count > 0 ? _formatting.Peek() : Formatting.Plain;
                    _cell = (name == "th" ? inherited with { Bold = true } : inherited).With(tag, css);
                    _span = (Span(tag.Attribute("colspan")), Span(tag.Attribute("rowspan")));
                    _edges = borders.Read(css.Parse(tag.Attribute("style")), _bordered);
                    _text = new StringBuilder();
                    _written = null;
                    _inline.Clear();
                    _formatting.Push(_cell);
                    break;
                case "br" when _text is not null:
                    _text.Append('\n');
                    break;
                case "p" or "div" when _text is { Length: > 0 }:
                    _text.Append('\n');
                    break;
                case "b" or "strong" when _text is not null:
                    Mark(name, Inline with { Bold = true });
                    break;
                case "i" or "em" when _text is not null:
                    Mark(name, Inline with { Italic = true });
                    break;
                case "u" when _text is not null:
                    Mark(name, Inline with { Underline = true });
                    break;
                case "span" or "font" when _text is not null && !tag.SelfClosing:
                    Mark(name, Inline.With(tag, css));
                    break;
            }
        }

        private void Mark(string tag, Formatting formatting)
        {
            if (_inline.Count < MaximumInlineDepth) _inline.Add((tag, formatting));
        }

        public void End(string name)
        {
            switch (name.ToLowerInvariant())
            {
                case "caption" when _caption is not null:
                    _captionText = _caption.ToString().Trim();
                    _caption = null;
                    break;
                case "td" or "th":
                    CloseCell();
                    break;
                case "tr":
                    CloseCell();
                    break;
                case var inline when _text is not null:
                    var open = _inline.FindLastIndex(entry => entry.Tag == inline);
                    if (open >= 0) _inline.RemoveRange(open, _inline.Count - open);
                    break;
            }
        }

        public void Text(string text)
        {
            if (_caption is not null)
            {
                _caption.Append(string.Join(' ', text.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))).Append(' ');
                return;
            }
            if (_text is null) return;
            var collapsed = string.Join(' ', text.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
            if (collapsed.Length == 0) return;
            if (_text.Length > 0 && char.IsWhiteSpace(text[0]) && _text[^1] is not ('\n' or '\t' or ' ')) _text.Append(' ');
            _text.Append(collapsed);
            if (char.IsWhiteSpace(text[^1])) _text.Append(' ');
            _written = _written is null ? Inline : _written.Common(Inline);
        }

        private void CloseCell()
        {
            if (_text is null) return;
            var row = _rows.Count - 1;
            var cells = _rows[row];
            while (_covered.ContainsKey((row, cells.Count))) cells.Add(null);
            var column = cells.Count;
            var shown = _written ?? _cell;
            cells.Add(new HtmlTableCell(_text.ToString().Trim())
            {
                Bold = shown.Bold, Italic = shown.Italic, Underline = shown.Underline, ColorHex = shown.Color, BackgroundHex = _cell.Background, Borders = _edges,
                Alignment = _cell.Alignment,
                VerticalAlignment = _cell.VerticalAlignment,
            });
            var room = Math.Max(1, MaximumCells - _cells);
            var columns = (int)Math.Min(_span.Columns, room);
            var rows = (int)Math.Clamp(room / columns, 1, _span.Rows);
            cells[column] = cells[column]! with { ColumnSpan = columns, RowSpan = rows };
            for (var down = 0; down < rows; down++)
                for (var across = 0; across < columns; across++)
                    if (down > 0 || across > 0) _covered[(row + down, column + across)] = true;
            for (var across = 1; across < columns; across++) cells.Add(null);
            _cells += (long)columns * rows;
            _text = null;
            _written = null;
            _inline.Clear();
            _nestedRows.Clear();
            if (_formatting.Count > 1) _formatting.Pop();
        }

        private int Span(string? value) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var span) ? Math.Clamp(span, 1, 1000) : 1;

        public HtmlTable? Build()
        {
            CloseCell();
            foreach (var ((row, column), _) in _covered)
                if (row < _rows.Count)
                    while (_rows[row].Count <= column) _rows[row].Add(null);
            var kept = _rows.Where(row => row.Count > 0).ToList();
            if (kept.Count == 0) return null;
            var rows = kept.Select((row, index) => (IReadOnlyList<HtmlTableCell?>)[.. row.Select(cell =>
                cell is { RowSpan: > 1 } spanning && spanning.RowSpan > kept.Count - index ? spanning with { RowSpan = kept.Count - index } : cell)]).ToList();
            return new HtmlTable(rows) { Caption = _captionText is { Length: > 0 } caption ? caption : null };
        }
    }

    private sealed record Formatting(bool Bold, bool Italic, bool Underline, string? Color, string? Background)
    {
        public static readonly Formatting Plain = new(false, false, false, null, null);

        public HtmlTextAlignment Alignment { get; init; }

        public HtmlVerticalAlignment VerticalAlignment { get; init; }

        public Formatting Common(Formatting other) => new(
            Bold && other.Bold,
            Italic && other.Italic,
            Underline && other.Underline,
            Color == other.Color ? Color : null,
            Background == other.Background ? Background : null)
        {
            Alignment = Alignment,
            VerticalAlignment = VerticalAlignment,
        };

        public Formatting With(HtmlStartTag tag, ICssDeclarations css)
        {
            var result = this;
            if (Hex(css.Background(tag.Attribute("bgcolor"))) is { } fill) result = result with { Background = fill };
            if (Hex(css.Color(tag.Attribute("color"))) is { } ink) result = result with { Color = ink };
            if (Aligned(tag.Attribute("align")) is { } aligned) result = result with { Alignment = aligned };
            if (VerticallyAligned(tag.Attribute("valign")) is { } valigned) result = result with { VerticalAlignment = valigned };
            if (tag.Attribute("style") is not { Length: > 0 } style) return result;
            foreach (var (property, value) in css.Parse(style))
            {
                switch (property.ToLowerInvariant())
                {
                    case "font-weight":
                        result = result with { Bold = value.Trim() is "bold" or "bolder" || (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var weight) && weight >= 600) };
                        break;
                    case "font-style":
                        result = result with { Italic = value.Contains("italic", StringComparison.OrdinalIgnoreCase) || value.Contains("oblique", StringComparison.OrdinalIgnoreCase) };
                        break;
                    case "text-decoration" or "text-decoration-line":
                        result = result with { Underline = value.Contains("underline", StringComparison.OrdinalIgnoreCase) };
                        break;
                    case "color" when Hex(css.Color(value)) is { } styleInk:
                        result = result with { Color = styleInk };
                        break;
                    case "vertical-align" when VerticallyAligned(value) is { } styleVertical:
                        result = result with { VerticalAlignment = styleVertical };
                        break;
                    case "text-align" when Aligned(value) is { } styleAligned:
                        result = result with { Alignment = styleAligned };
                        break;
                    case "background" or "background-color" when Hex(css.Background(value)) is { } styleFill:
                        result = result with { Background = styleFill };
                        break;
                }
            }
            return result;
        }

        private static HtmlTextAlignment? Aligned(string? value) => value?.Trim().ToLowerInvariant() switch
        {
            "left" => HtmlTextAlignment.Left,
            "center" or "middle" or "-webkit-center" => HtmlTextAlignment.Center,
            "right" => HtmlTextAlignment.Right,
            "justify" => HtmlTextAlignment.Justify,
            _ => null,
        };

        private static HtmlVerticalAlignment? VerticallyAligned(string? value) => value?.Trim().ToLowerInvariant() switch
        {
            "top" or "text-top" => HtmlVerticalAlignment.Top,
            "middle" or "center" => HtmlVerticalAlignment.Middle,
            "bottom" or "text-bottom" => HtmlVerticalAlignment.Bottom,
            _ => null,
        };

        private static string? Hex(uint? color) =>
            color is { } value ? new ArgbColor(value).ToHashedHex() : null;
    }
}

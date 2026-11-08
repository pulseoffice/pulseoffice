using Avalonia;
using Avalonia.Media;
using PulseOffice.Document.Embedding;
using PulseOffice.Localization;

namespace PulseOffice.Data.UI;

public interface IDataTableRenderer
{
    void Render(DrawingContext context, DataTableObject table, Rect bounds, System.Globalization.CultureInfo culture);

    IReadOnlyList<double> ColumnWidths(DataSnapshot snapshot, double available, double fontSize);
}

public sealed class DataTableRenderer : IDataTableRenderer
{
    private readonly ITextCatalog _texts;
    private readonly ITableCellTextStyles _styles;
    private readonly OptimalColumnSizing _optimal = new(new PulseOffice.Document.Tables.ColumnWidths());

    public DataTableRenderer(ITextCatalog texts)
        : this(texts, new TableCellTextStyles())
    {
    }

    public DataTableRenderer(ITextCatalog texts, ITableCellTextStyles styles)
    {
        _texts = texts;
        _styles = styles;
    }

    private const double CellPadding = 6;
    private const double MinimumColumnWidth = 48;

    public void Render(DrawingContext context, DataTableObject table, Rect bounds, System.Globalization.CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(culture);

        var snapshot = table.Snapshot;
        var style = table.Style;
        var typeface = new Typeface(FontFamily.Default);
        var text = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
        var muted = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
        var line = new Pen(new SolidColorBrush(Color.FromArgb(0x40, 0x64, 0x74, 0x8B)), 1);
        var accent = style.AccentHex is { Length: > 0 } hex && Color.TryParse(hex, out var parsed)
            ? new SolidColorBrush(parsed)
            : new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A));

        if (snapshot.Columns.Count == 0)
        {
            Draw(context, culture, table.LastError ?? _texts["data.table.empty"], muted, bounds.TopLeft + new Point(CellPadding, CellPadding),
                typeface, style.FontSize, bounds.Width);
            return;
        }

        var headerOffset = style.HeaderRow ? 1 : 0;
        var heights = Enumerable.Range(0, snapshot.Rows.Count + headerOffset)
            .Select(i => TableMetrics.RowHeight(table, i)).ToArray();
        if (style.OwnLook)
        {
            RenderOwnLook(context, culture, table, bounds, heights, typeface);
            return;
        }
        var rowHeight = style.HeaderRow ? heights[0] : style.FontSize * 1.9;
        var widths = Widths(table, bounds.Width);
        var y = bounds.Y;

        if (style.HeaderRow)
        {
            context.FillRectangle(
                new SolidColorBrush(accent.Color, 0.14), new Rect(bounds.X, y, bounds.Width, rowHeight));

            var x = bounds.X;
            for (var i = 0; i < snapshot.Columns.Count; i++)
            {
                var span = HeaderSpan(table, i);
                if (span > 0)
                    Draw(context, culture, snapshot.Columns[i].Name, accent,
                        new Point(x + CellPadding, y + rowHeight * 0.2), typeface, style.FontSize,
                        widths.Skip(i).Take(span).Sum() - CellPadding * 2, bold: true);
                x += widths[i];
            }

            y += rowHeight;
        }

        var bodyTop = y;
        var xs = new double[snapshot.Columns.Count + 1];
        xs[0] = bounds.X;
        for (var c = 0; c < snapshot.Columns.Count; c++) xs[c + 1] = xs[c] + widths[c];
        var index = new TableCellIndex(table);

        var tops = new double[snapshot.Rows.Count + 1];
        tops[0] = bodyTop;
        for (var r = 0; r < snapshot.Rows.Count; r++) tops[r + 1] = tops[r] + heights[r + headerOffset];

        for (var r = 0; r < snapshot.Rows.Count; r++)
        {
            var rowTop = tops[r];
            var bodyRowHeight = heights[r + headerOffset];
            if (rowTop >= bounds.Bottom) break;

            if (style.BandedRows && r % 2 == 1)
                context.FillRectangle(
                    new SolidColorBrush(Color.FromArgb(0x14, 0x64, 0x74, 0x8B)),
                    new Rect(bounds.X, rowTop, bounds.Width, bodyRowHeight));

            var row = snapshot.Rows[r];

            for (var c = 0; c < snapshot.Columns.Count; c++)
            {
                if (index.Cover(r, c) is not null) continue;

                var origin = index.Origin(r, c);
                var colSpan = origin?.ColumnSpan ?? 1;
                var rowSpan = origin?.RowSpan ?? 1;

                var cellRight = xs[Math.Min(c + colSpan, snapshot.Columns.Count)];
                var cellBottom = tops[Math.Min(r + rowSpan, snapshot.Rows.Count)];

                var value = c < row.Count ? row[c] : null;
                var kind = snapshot.Columns[c].Kind;

                var cellHeight = cellBottom - rowTop;
                var textHeight = style.FontSize * 1.3;
                var valign = index.Format(r, c)?.VerticalAlign ?? CellVerticalAlign.Top;
                var textY = valign switch
                {
                    CellVerticalAlign.Center => rowTop + (cellHeight - textHeight) / 2,
                    CellVerticalAlign.Bottom => rowTop + cellHeight - textHeight - bodyRowHeight * 0.2,
                    _ => rowTop + Math.Max(0, (Math.Min(bodyRowHeight, style.FontSize * 1.9) - textHeight) / 2),
                };

                Draw(context, culture, Format(value, kind, culture), text,
                    new Point(xs[c] + CellPadding, textY), typeface, style.FontSize,
                    cellRight - xs[c] - CellPadding * 2,
                    alignRight: kind == DataValueKind.Number);

                if (style.Grid)
                    context.DrawRectangle(null, line, new Rect(
                        new Point(xs[c], rowTop), new Point(cellRight, Math.Min(cellBottom, bounds.Bottom))));
            }
        }

        if (style.Grid && style.HeaderRow)
        {
            var hx = bounds.X;
            for (var i = 0; i < widths.Count; i++)
            {
                var span = HeaderSpan(table, i);
                if (span > 0) context.DrawRectangle(null, line, new Rect(hx, bounds.Y, widths.Skip(i).Take(span).Sum(), rowHeight));
                hx += widths[i];
            }
        }
    }

    private int HeaderSpan(DataTableObject table, int column) =>
        table.Merges.Any(merge => merge.Covers(TableCellFormat.HeaderRow, column))
            ? 0
            : table.Merges.FirstOrDefault(merge => merge.IsOrigin(TableCellFormat.HeaderRow, column))?.ColumnSpan ?? 1;

    private void RenderOwnLook(DrawingContext context, System.Globalization.CultureInfo culture, DataTableObject table, Rect bounds, double[] heights, Typeface typeface)
    {
        var snapshot = table.Snapshot;
        var columns = snapshot.Columns.Count;
        var widths = Widths(table, bounds.Width);
        var xs = new double[columns + 1];
        xs[0] = bounds.X;
        for (var c = 0; c < columns; c++) xs[c + 1] = xs[c] + widths[c];

        var first = table.Style.HeaderRow ? TableCellFormat.HeaderRow : 0;
        var displayedRows = snapshot.Rows.Count - first;
        var tops = new double[displayedRows + 1];
        tops[0] = bounds.Y;
        for (var d = 0; d < displayedRows; d++) tops[d + 1] = tops[d] + heights[Math.Min(d, heights.Length - 1)];

        var index = new TableCellIndex(table);
        var cells = new List<(Rect Area, TableCellFormat? Format, string Text, DataValueKind Kind)>();
        for (var row = first; row < snapshot.Rows.Count; row++)
        {
            var top = tops[row - first];
            if (top >= bounds.Bottom) break;
            for (var c = 0; c < columns; c++)
            {
                if (index.Cover(row, c) is not null) continue;
                var origin = index.Origin(row, c);
                var displayed = row - first;
                var right = xs[Math.Min(c + (origin?.ColumnSpan ?? 1), columns)];
                var bottom = tops[Math.Min(displayed + (origin?.RowSpan ?? 1), displayedRows)];
                var text = row < 0 ? snapshot.Columns[c].Name : c < snapshot.Rows[row].Count ? Format(snapshot.Rows[row][c], snapshot.Columns[c].Kind, culture) : string.Empty;
                cells.Add((new Rect(new Point(xs[c], tops[displayed]), new Point(right, bottom)),
                    index.Format(row, c), text, snapshot.Columns[c].Kind));
            }
        }

        foreach (var cell in cells)
            if (cell.Format?.Look?.FillHex is { } fill && Color.TryParse(fill, out var colour))
                context.FillRectangle(new SolidColorBrush(colour), cell.Area);

        foreach (var cell in cells)
        {
            var look = cell.Format?.Look ?? TableCellLook.Plain;
            var size = look.FontSize ?? table.Style.FontSize;
            var brush = new SolidColorBrush(look.TextHex is { } hex && Color.TryParse(hex, out var ink) ? ink : Colors.Black);
            var textHeight = size * 1.3;
            var y = (cell.Format?.VerticalAlign ?? CellVerticalAlign.Top) switch
            {
                CellVerticalAlign.Center => cell.Area.Y + (cell.Area.Height - textHeight) / 2,
                CellVerticalAlign.Bottom => cell.Area.Bottom - textHeight - CellPadding / 2,
                _ => cell.Area.Y + CellPadding / 2,
            };
            Draw(context, culture, cell.Text, brush, new Point(cell.Area.X + CellPadding, y), _styles.Typeface(look), size, cell.Area.Width - CellPadding * 2,
                alignRight: look.Horizontal is CellHorizontalAlign.Right, centre: look.Horizontal is CellHorizontalAlign.Center,
                decorations: _styles.Decorations(look));
        }

        foreach (var cell in cells)
        {
            var look = cell.Format?.Look ?? TableCellLook.Plain;
            Border(context, look.Top, cell.Area.TopLeft, cell.Area.TopRight);
            Border(context, look.Bottom, cell.Area.BottomLeft, cell.Area.BottomRight);
            Border(context, look.Left, cell.Area.TopLeft, cell.Area.BottomLeft);
            Border(context, look.Right, cell.Area.TopRight, cell.Area.BottomRight);
        }
    }

    private void Border(DrawingContext context, TableBorderLine? line, Point from, Point to)
    {
        if (line is not { WidthPoints: > 0 } || !Color.TryParse(line.ColorHex, out var colour)) return;
        context.DrawLine(new Pen(new SolidColorBrush(colour), line.WidthPoints), from, to);
    }

    private IReadOnlyList<double> Widths(DataTableObject table, double available)
    {
        var fractions = table.Layout.ColumnFractions;
        if (fractions.Count != table.Snapshot.Columns.Count) fractions = _optimal.Apply(table).ColumnFractions;
        return [.. fractions.Select(fraction => fraction * available)];
    }

    public IReadOnlyList<double> ColumnWidths(DataSnapshot snapshot, double available, double fontSize)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.Columns.Count == 0) return [];

        var weights = new double[snapshot.Columns.Count];

        for (var c = 0; c < snapshot.Columns.Count; c++)
        {
            var longest = (double)snapshot.Columns[c].Name.Length;

            foreach (var row in snapshot.Rows.Take(50))
                if (c < row.Count)
                    longest = Math.Max(longest, Format(row[c], snapshot.Columns[c].Kind, System.Globalization.CultureInfo.InvariantCulture).Length);

            weights[c] = Math.Max(MinimumColumnWidth, Math.Min(longest, 40) * fontSize * 0.58 + CellPadding * 2);
        }

        var total = weights.Sum();
        if (total <= 0) return weights;

        var scale = available / total;
        return [.. weights.Select(width => width * scale)];
    }

    private static string Format(object? value, DataValueKind kind, System.Globalization.CultureInfo culture) => value switch
    {
        null or DBNull => string.Empty,
        DateTime moment => kind == DataValueKind.Date && moment.TimeOfDay == TimeSpan.Zero
            ? moment.ToString("d", culture)
            : moment.ToString("g", culture),
        bool flag => flag.ToString(culture),
        IFormattable number when kind == DataValueKind.Number =>
            number.ToString("0.########", culture),
        _ => value.ToString() ?? string.Empty,
    };

    private static void Draw(
        DrawingContext context, System.Globalization.CultureInfo culture, string content, IBrush brush, Point origin, Typeface typeface,
        double size, double width, bool bold = false, bool alignRight = false, bool centre = false, TextDecorationCollection? decorations = null)
    {
        if (content.Length == 0) return;

        var formatted = new FormattedText(
            content, culture, FlowDirection.LeftToRight,
            bold ? new Typeface(typeface.FontFamily, weight: FontWeight.SemiBold) : typeface,
            size, brush)
        {
            MaxTextWidth = Math.Max(1, width),
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis,
        };
        if (decorations is not null) formatted.SetTextDecorations(decorations);

        var x = alignRight ? origin.X + Math.Max(0, width - formatted.Width)
            : centre ? origin.X + Math.Max(0, (width - formatted.Width) / 2)
            : origin.X;
        context.DrawText(formatted, new Point(x, origin.Y));
    }
}

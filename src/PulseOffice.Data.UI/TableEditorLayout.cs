using PulseOffice.Document.Embedding;

namespace PulseOffice.Data.UI;

public interface ICellLineEstimator
{
    int Lines(string text, double width, double fontSize);
}

public sealed class CellLineEstimator : ICellLineEstimator
{
    private const double AverageGlyphWidth = 0.56;

    public int Lines(string text, double width, double fontSize)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0) return 1;

        var perLine = Math.Max(1, (int)(width / Math.Max(1, fontSize * AverageGlyphWidth)));
        var lines = 0;
        var start = 0;
        while (true)
        {
            var end = text.IndexOf('\n', start);
            var length = (end < 0 ? text.Length : end) - start;
            if (length > 0 && text[start + length - 1] == '\r') length--;
            lines += Math.Max(1, (length + perLine - 1) / perLine);
            if (end < 0) break;
            start = end + 1;
        }
        return lines;
    }
}

public sealed record TableEditorMetrics(
    double RowNumberWidth,
    double LetterHeight,
    double ColumnWidth,
    double FontSize,
    double LineHeight,
    double PaddingX,
    double PaddingY,
    double MinRowHeight,
    int MaxLines);

public sealed class TableEditorLayout
{
    private readonly TableEditorSheet _sheet;
    private readonly ICellLineEstimator _lines;
    private double[] _tops = [0];

    public TableEditorLayout(TableEditorSheet sheet, ICellLineEstimator lines, TableEditorMetrics metrics)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
        _lines = lines ?? throw new ArgumentNullException(nameof(lines));
        Metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        Recompute();
    }

    public TableEditorMetrics Metrics { get; }

    public double HeaderHeight { get; private set; }

    public double BodyHeight => _tops[^1];

    public double BodyWidth => _sheet.ColumnCount * Metrics.ColumnWidth;

    public double FrozenTop => Metrics.LetterHeight + HeaderHeight;

    public double ExtentWidth => Metrics.RowNumberWidth + BodyWidth;

    public double ExtentHeight => FrozenTop + BodyHeight;

    public void Recompute()
    {
        HeaderHeight = Height(TableCellFormat.HeaderRow);
        _tops = new double[_sheet.RowCount + 1];
        for (var row = 0; row < _sheet.RowCount; row++) _tops[row + 1] = _tops[row] + Height(row);
    }

    public void Recompute(IEnumerable<int> rows)
    {
        foreach (var row in rows)
        {
            if (row == TableCellFormat.HeaderRow) { HeaderHeight = Height(row); continue; }
            if (row < 0 || row >= _sheet.RowCount || _tops.Length != _sheet.RowCount + 1) { Recompute(); return; }
            var delta = Height(row) - RowHeight(row);
            if (delta == 0) continue;
            for (var next = row + 1; next < _tops.Length; next++) _tops[next] += delta;
        }
    }

    public double RowTop(int row) => row == TableCellFormat.HeaderRow ? -HeaderHeight : _tops[Math.Clamp(row, 0, _tops.Length - 1)];

    public double RowHeight(int row) => row == TableCellFormat.HeaderRow ? HeaderHeight : _tops[row + 1] - _tops[row];

    public double ColumnLeft(int column) => column * Metrics.ColumnWidth;

    public int RowAt(double y)
    {
        if (_sheet.RowCount == 0) return 0;
        if (y <= 0) return 0;
        var low = 0;
        var high = _sheet.RowCount - 1;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (_tops[middle] <= y) low = middle;
            else high = middle - 1;
        }
        return low;
    }

    public int ColumnAt(double x) =>
        Math.Clamp((int)Math.Floor(x / Metrics.ColumnWidth), 0, Math.Max(0, _sheet.ColumnCount - 1));

    public (double Left, double Top, double Width, double Height) CellBox(TableCellRef cell)
    {
        var origin = _sheet.Index.Origin(cell);
        var rowSpan = origin?.RowSpan ?? 1;
        var columnSpan = origin?.ColumnSpan ?? 1;
        var top = RowTop(cell.Row);
        var bottom = cell.IsHeader ? 0 : _tops[Math.Min(cell.Row + rowSpan, _tops.Length - 1)];
        return (ColumnLeft(cell.Column), top, columnSpan * Metrics.ColumnWidth, bottom - top);
    }

    private double Height(int row)
    {
        var lines = 1;
        for (var column = 0; column < _sheet.ColumnCount; column++)
        {
            var cell = new TableCellRef(row, column);
            if (_sheet.Index.Owner(cell) is { } owner && (owner.RowSpan > 1 || !owner.IsOrigin(row, column))) continue;
            var width = (_sheet.Index.Origin(cell)?.ColumnSpan ?? 1) * Metrics.ColumnWidth - Metrics.PaddingX * 2;
            lines = Math.Max(lines, _lines.Lines(_sheet.Text(cell), width, Metrics.FontSize));
        }
        return Math.Max(Metrics.MinRowHeight, Math.Min(lines, Metrics.MaxLines) * Metrics.LineHeight + Metrics.PaddingY * 2);
    }
}

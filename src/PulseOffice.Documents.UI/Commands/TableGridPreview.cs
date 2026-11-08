using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PulseOffice.Documents.Editing.Tables;

namespace PulseOffice.Documents.UI.Commands;

public sealed class TableGridPreview : Control
{
    private readonly IBrush _paper = Brushes.White;
    private readonly IPen _line = new Pen(new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80)), 1);
    private readonly IBrush _ink = new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x37));
    private readonly IBrush _heading = new SolidColorBrush(Color.FromRgb(0xE5, 0xEC, 0xF6));
    private IReadOnlyList<IReadOnlyList<string>> _rows = [];

    public TableGridPreview()
    {
        Width = 240;
        Height = 140;
    }

    public IReadOnlyList<IReadOnlyList<string>> Rows
    {
        get => _rows;
        set
        {
            _rows = value ?? [];
            InvalidateVisual();
        }
    }

    public bool HeadingRow
    {
        get;
        set
        {
            field = value;
            InvalidateVisual();
        }
    }

    public int ColumnCount => _rows.Count == 0 ? 0 : _rows.Max(row => row.Count);

    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(_paper, bounds);
        var columns = ColumnCount;
        if (_rows.Count == 0 || columns == 0) return;
        var area = bounds.Deflate(8);
        var cellWidth = area.Width / columns;
        var cellHeight = Math.Min(28, area.Height / _rows.Count);
        for (var row = 0; row < _rows.Count; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var cell = new Rect(area.X + column * cellWidth, area.Y + row * cellHeight, cellWidth, cellHeight);
                if (HeadingRow && row == 0) context.FillRectangle(_heading, cell);
                context.DrawRectangle(_line, cell);
                var text = column < _rows[row].Count ? _rows[row][column] : string.Empty;
                if (text.Length == 0) continue;
                var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 10, _ink)
                {
                    MaxTextWidth = Math.Max(1, cell.Width - 6),
                    MaxLineCount = 1,
                    Trimming = TextTrimming.CharacterEllipsis,
                };
                using (context.PushClip(cell))
                    context.DrawText(formatted, new Point(cell.X + 3, cell.Y + (cell.Height - formatted.Height) / 2));
            }
        }
    }
}

public interface ITableTextSampler
{
    IReadOnlyList<IReadOnlyList<string>> Split(IReadOnlyList<string> lines, TableTextSeparator separator, char custom, int maximum);
}

public sealed class TableTextSampler : ITableTextSampler
{
    public IReadOnlyList<IReadOnlyList<string>> Split(IReadOnlyList<string> lines, TableTextSeparator separator, char custom, int maximum)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (separator == TableTextSeparator.Paragraph) return [.. lines.Take(maximum).Select(line => (IReadOnlyList<string>)[line])];
        var mark = separator switch
        {
            TableTextSeparator.Tab => '\t',
            TableTextSeparator.Semicolon => ';',
            _ => custom,
        };
        return [.. lines.Take(maximum).Select(line => (IReadOnlyList<string>)line.Split(mark))];
    }
}

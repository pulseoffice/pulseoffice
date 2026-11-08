using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PulseOffice.Documents.Blocks;

namespace PulseOffice.Documents.UI.Commands;

public sealed record TablePlacementSketch(double TextWidth, double TableWidth, int Columns, TableAlignment Alignment, double Indent);

public sealed class TablePlacementPreview : Control
{
    private const double Gutter = 22;
    private readonly IBrush _paper = Brushes.White;
    private readonly IBrush _text = new SolidColorBrush(Color.FromRgb(0xD1, 0xD5, 0xDB));
    private readonly IBrush _heading = new SolidColorBrush(Color.FromRgb(0xE5, 0xEC, 0xF6));
    private readonly IPen _line = new Pen(new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80)), 1);
    private readonly IPen _guide = new Pen(new SolidColorBrush(Color.FromRgb(0x93, 0xC5, 0xFD)), 1, new DashStyle([3, 3], 0));
    private TablePlacementSketch _sketch = new(450, 300, 3, TableAlignment.Left, 0);

    public TablePlacementPreview()
    {
        Width = 200;
        Height = 220;
    }

    public TablePlacementSketch Sketch
    {
        get => _sketch;
        set
        {
            _sketch = value ?? _sketch;
            InvalidateVisual();
        }
    }

    public Rect TableBounds
    {
        get
        {
            var text = TextArea;
            var scale = text.Width / Math.Max(1, _sketch.TextWidth);
            var width = Math.Min(text.Width + Gutter, Math.Max(12, _sketch.TableWidth * scale));
            var room = Math.Max(0, text.Width - width);
            var offset = _sketch.Alignment switch
            {
                TableAlignment.Center => room / 2,
                TableAlignment.Right => room,
                _ => Math.Clamp(_sketch.Indent * scale, -Gutter + 2, Math.Max(0, text.Width - 12)),
            };
            return new Rect(text.X + offset, text.Y + 52, width, 60);
        }
    }

    private Rect TextArea => new(Gutter, Gutter, Math.Max(10, Bounds.Width - 2 * Gutter), Math.Max(10, Bounds.Height - 2 * Gutter));

    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.FillRectangle(_paper, new Rect(Bounds.Size));
        var text = TextArea;
        context.DrawLine(_guide, new Point(text.Left, 4), new Point(text.Left, Bounds.Height - 4));
        context.DrawLine(_guide, new Point(text.Right, 4), new Point(text.Right, Bounds.Height - 4));
        for (var line = 0; line < 4; line++)
            context.FillRectangle(_text, new Rect(text.X, text.Y + line * 11, line == 3 ? text.Width * 0.6 : text.Width, 5));
        var table = TableBounds;
        for (var line = 0; line < 3; line++)
            context.FillRectangle(_text, new Rect(text.X, table.Bottom + 12 + line * 11, line == 2 ? text.Width * 0.45 : text.Width, 5));
        var columns = Math.Max(1, _sketch.Columns);
        var rows = 3;
        var cellWidth = table.Width / columns;
        var cellHeight = table.Height / rows;
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var cell = new Rect(table.X + column * cellWidth, table.Y + row * cellHeight, cellWidth, cellHeight);
                if (row == 0) context.FillRectangle(_heading, cell);
                context.DrawRectangle(_line, cell);
            }
        }
    }
}

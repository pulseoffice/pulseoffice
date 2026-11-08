using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PulseOffice.Document.Borders;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Borders;

public sealed class BorderGlyph : Control
{
    private readonly IBorderFramePainter _painter;
    private readonly BorderFrameScope _scope;
    private readonly BorderFrame _frame;
    private readonly ResourceBrushes _brushes = new();

    public BorderGlyph(IBorderFramePainter painter, BorderFrameScope scope, BorderFrame frame, double size = 22)
    {
        _painter = painter ?? throw new ArgumentNullException(nameof(painter));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _frame = frame ?? throw new ArgumentNullException(nameof(frame));
        Width = size;
        Height = size;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var guide = _brushes.Find(this, Palette.Keys.TextDisabled, Brushes.Gray);
        var ink = _brushes.Find(this, Palette.Keys.TextPrimary, Brushes.Black);
        var colour = ink is ISolidColorBrush solid ? solid.Color.ToString() : "#000000";
        var inked = _frame.Positions.Aggregate(_frame, (frame, position) => frame.With(position, frame.Stroke(position)! with { ColorHex = colour }));
        _painter.Paint(context, new Rect(Bounds.Size).Deflate(3), _scope, inked, new BorderPaintColors(guide, Brushes.Transparent, guide), showSample: false);
    }
}

public sealed class BorderDashSample : Control
{
    private readonly IBorderFramePainter _painter;
    private readonly BorderDash _dash;
    private readonly ResourceBrushes _brushes = new();

    public BorderDashSample(IBorderFramePainter painter, BorderDash dash)
    {
        _painter = painter ?? throw new ArgumentNullException(nameof(painter));
        _dash = dash;
        Width = 96;
        Height = 14;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public BorderDash Dash => _dash;

    public override void Render(DrawingContext context)
    {
        var ink = _brushes.Find(this, Palette.Keys.TextPrimary, Brushes.Black);
        var colour = ink is ISolidColorBrush solid ? solid.Color.ToString() : "#000000";
        var middle = Bounds.Height / 2;
        _painter.PaintStroke(context, new Point(2, middle), new Point(Bounds.Width - 2, middle), new BorderStroke(_dash, 1.5, colour));
    }
}

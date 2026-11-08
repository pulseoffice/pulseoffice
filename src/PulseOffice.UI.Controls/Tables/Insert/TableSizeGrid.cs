using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Tables.Insert;

public sealed class TableSizeGrid : ThemedSurface
{
    public const double Cell = 17;
    public const double Gap = 3;
    private const double Inset = 3;

    private readonly TableSizeModel _model;
    private bool _dragging;

    public TableSizeGrid(TableSizeModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        Focusable = true;
        FocusAdorner = null;
        Cursor = new Cursor(StandardCursorType.Hand);
        _model.Changed += (_, _) => Refresh();
        AttachedToVisualTree += (_, _) => Refresh();
    }

    public event EventHandler? Picked;

    public TableSizeModel Model => _model;

    public bool IdleHighlight
    {
        get;
        set
        {
            field = value;
            InvalidateVisual();
        }
    } = true;

    public Point CenterOf(int row, int column) =>
        new(Inset + (column - 1) * (Cell + Gap) + Cell / 2, Inset + (row - 1) * (Cell + Gap) + Cell / 2);

    protected override Size MeasureOverride(Size availableSize) =>
        new(Inset * 2 + _model.GridColumns * (Cell + Gap) - Gap, Inset * 2 + _model.GridRows * (Cell + Gap) - Gap);

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        var accent = this.FindResource("PulseAccent") as IBrush ?? Brushes.SteelBlue;
        var tint = accent is ISolidColorBrush solid ? new SolidColorBrush(solid.Color, 0.22) : accent;
        var surface = Themed(Palette.Keys.Surface, Brushes.White);
        var line = new Pen(Themed(Palette.Keys.BorderStrong, Brushes.Gray), 1);
        var chosen = new Pen(accent, 1.2);

        for (var row = 0; row < _model.GridRows; row++)
            for (var column = 0; column < _model.GridColumns; column++)
            {
                var inside = (IdleHighlight || _model.Hovering) && row < _model.ShownRows && column < _model.ShownColumns;
                var cell = new Rect(Inset + column * (Cell + Gap), Inset + row * (Cell + Gap), Cell, Cell).Deflate(0.5);
                context.DrawRectangle(inside ? tint : surface, inside ? chosen : line, new RoundedRect(cell, 2.5));
            }

        if (IsFocused && !_model.Hovering)
            context.DrawRectangle(null, new Pen(accent, 1.5), new RoundedRect(new Rect(Bounds.Size).Deflate(0.75), 5));
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        HoverAt(e.GetPosition(this));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!_dragging) _model.EndHover();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _dragging = true;
        e.Pointer.Capture(this);
        HoverAt(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging) return;
        _dragging = false;
        e.Pointer.Capture(null);
        HoverAt(e.GetPosition(this));
        _model.CommitHover();
        e.Handled = true;
        Picked?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;
        (int Rows, int Columns)? step = e.Key switch
        {
            Key.Right => (0, 1),
            Key.Left => (0, -1),
            Key.Down => (1, 0),
            Key.Up => (-1, 0),
            _ => null,
        };
        if (step is { } move)
        {
            _model.Move(move.Rows, move.Columns);
            e.Handled = true;
        }
        else if (e.Key is Key.Enter or Key.Space)
        {
            e.Handled = true;
            Picked?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    private void HoverAt(Point point)
    {
        if (!_dragging && !new Rect(Bounds.Size).Contains(point)) return;
        var column = (int)Math.Floor((point.X - Inset + Gap / 2) / (Cell + Gap)) + 1;
        var row = (int)Math.Floor((point.Y - Inset + Gap / 2) / (Cell + Gap)) + 1;
        _model.Hover(row, column);
    }

    private void Refresh()
    {
        Avalonia.Automation.AutomationProperties.SetName(this, $"{_model.ShownColumns} × {_model.ShownRows}");
        InvalidateMeasure();
        InvalidateVisual();
    }
}

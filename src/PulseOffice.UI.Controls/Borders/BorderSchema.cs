using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using PulseOffice.Document.Borders;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Borders;

public sealed class BorderSchema : Control
{
    private const double Inset = 16;

    private readonly BorderEditorModel _model;
    private readonly IBorderFramePainter _painter;
    private readonly IBorderSchemaGeometry _geometry;
    private readonly IReadOnlyList<BorderPosition> _positions;
    private readonly ResourceBrushes _brushes = new();
    private readonly IBrush _paperGuide = new ImmutableSolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF));
    private readonly IBrush _paperSample = new ImmutableSolidColorBrush(Color.FromRgb(0xE5, 0xE7, 0xEB));
    private BorderPosition? _hover;
    private int _focus;

    public BorderSchema(BorderEditorModel model, IBorderFramePainter painter, IBorderSchemaGeometry geometry, string name, string help)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _painter = painter ?? throw new ArgumentNullException(nameof(painter));
        _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        _positions = [.. model.Scope.Positions];
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);
        AutomationProperties.SetName(this, name);
        AutomationProperties.SetHelpText(this, help);
        ToolTip.SetTip(this, help);
        _model.Changed += (_, _) => InvalidateVisual();
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public BorderPosition FocusedPosition => _positions[_focus];

    public BorderPosition? HoveredPosition => _hover;

    public event EventHandler<BorderPosition>? FocusMoved;

    protected override Size MeasureOverride(Size availableSize) => new(200, 150);

    public Rect Area => new Rect(Bounds.Size).Deflate(Inset);

    public void Click(Point point)
    {
        if (_geometry.HitTest(Area, _model.Scope, point) is not { } position) return;
        _focus = Math.Max(0, IndexOf(position));
        _model.Toggle(position);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var hover = _geometry.HitTest(Area, _model.Scope, e.GetPosition(this));
        if (hover == _hover) return;
        _hover = hover;
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = null;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Focus(NavigationMethod.Pointer);
        Click(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Right or Key.Down:
                Move(1);
                break;
            case Key.Left or Key.Up:
                Move(-1);
                break;
            case Key.Space or Key.Enter:
                _model.Toggle(FocusedPosition);
                break;
            default:
                return;
        }
        e.Handled = true;
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

    public override void Render(DrawingContext context)
    {
        var border = Brush(Palette.Keys.Border, Brushes.LightGray);
        context.DrawRectangle(Brushes.White, new Pen(border, 1), new Rect(Bounds.Size), 6, 6);
        var guide = _paperGuide;
        var sample = _paperSample;
        var accent = Brush("PulseAccent", Brushes.DodgerBlue);
        var marked = IsFocused ? FocusedPosition : _hover;
        _painter.Paint(context, Area, _model.Scope, _model.Frame, new BorderPaintColors(guide, sample, accent), marked);
    }

    private void Move(int step)
    {
        _focus = (_focus + step + _positions.Count) % _positions.Count;
        FocusMoved?.Invoke(this, FocusedPosition);
        InvalidateVisual();
    }

    private int IndexOf(BorderPosition position)
    {
        for (var i = 0; i < _positions.Count; i++)
            if (_positions[i] == position) return i;
        return -1;
    }

    private IBrush Brush(string key, IBrush fallback) => _brushes.Find(this, key, fallback);
}

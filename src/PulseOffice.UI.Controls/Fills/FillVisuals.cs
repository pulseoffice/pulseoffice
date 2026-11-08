using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PulseOffice.Drawing.Shapes;
using PulseOffice.UI.Controls.Shapes;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Fills;

public sealed class FillSwatch : Control
{
    private const double Checker = 6;

    private readonly IShapeBrushes _brushes;
    private readonly ResourceBrushes _resources = new();
    private ShapeFill _fill;

    public FillSwatch(ShapeFill fill, IShapeBrushes brushes)
    {
        _fill = fill ?? throw new ArgumentNullException(nameof(fill));
        _brushes = brushes ?? throw new ArgumentNullException(nameof(brushes));
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public ShapeFill Fill => _fill;

    public void Show(ShapeFill fill)
    {
        _fill = fill ?? throw new ArgumentNullException(nameof(fill));
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        var light = _resources.Find(this, Palette.Keys.Surface, Brushes.White);
        var dark = _resources.Find(this, Palette.Keys.SurfaceSunken, Brushes.Gainsboro);
        using (context.PushClip(new RoundedRect(bounds, 4)))
        {
            context.FillRectangle(light, bounds);
            for (var y = 0; y * Checker < bounds.Height; y++)
                for (var x = 0; x * Checker < bounds.Width; x++)
                    if ((x + y) % 2 == 1) context.FillRectangle(dark, new Rect(x * Checker, y * Checker, Checker, Checker));
            if (_brushes.Fill(_fill, bounds) is { } brush) context.FillRectangle(brush, bounds);
        }
        context.DrawRectangle(null, new Pen(_resources.Find(this, Palette.Keys.Border, Brushes.Gray), 1), bounds.Deflate(0.5), 4, 4);
    }
}

public sealed class GradientStopBar : Control
{
    private const double HandleRadius = 6;
    private const double BarInset = 8;
    private const double KeyStep = 0.01;

    private readonly FillEditorModel _model;
    private readonly IShapeBrushes _brushes;
    private readonly ResourceBrushes _resources = new();
    private bool _dragging;

    public GradientStopBar(FillEditorModel model, IShapeBrushes brushes, string name, string help)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _brushes = brushes ?? throw new ArgumentNullException(nameof(brushes));
        Focusable = true;
        Height = 40;
        AutomationProperties.SetName(this, name);
        AutomationProperties.SetHelpText(this, help);
        ToolTip.SetTip(this, help);
        _model.Changed += (_, _) => InvalidateVisual();
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public Rect Bar => new(BarInset, 4, Math.Max(1, Bounds.Width - BarInset * 2), Math.Max(1, Bounds.Height - HandleRadius * 2 - 8));

    public double PositionAt(double x) => Math.Clamp((x - Bar.X) / Bar.Width, 0, 1);

    public int? StopAt(Point point)
    {
        var stops = _model.Fill.Gradient.Stops;
        for (var i = 0; i < stops.Count; i++)
        {
            var x = Bar.X + stops[i].Position * Bar.Width;
            if (Math.Abs(point.X - x) <= HandleRadius + 2 && point.Y >= Bar.Bottom - 2) return i;
        }
        return null;
    }

    public void Press(Point point)
    {
        if (StopAt(point) is { } index)
        {
            _model.SelectStop(index);
            _dragging = true;
            return;
        }
        _model.AddStop(PositionAt(point.X));
        _dragging = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus(NavigationMethod.Pointer);
        Press(e.GetPosition(this));
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging) _model.MoveStop(PositionAt(e.GetPosition(this).X));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        e.Pointer.Capture(null);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var stops = _model.Fill.Gradient.Stops;
        var current = stops[_model.SelectedStop].Position;
        var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? KeyStep * 10 : KeyStep;
        switch (e.Key)
        {
            case Key.Left:
                _model.MoveStop(current - step);
                break;
            case Key.Right:
                _model.MoveStop(current + step);
                break;
            case Key.Tab when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                _model.SelectStop((_model.SelectedStop + 1) % stops.Count);
                break;
            case Key.Delete or Key.Back:
                _model.RemoveStop();
                break;
            case Key.Insert:
                _model.AddStop(Math.Min(1, current + 0.1));
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        var bar = Bar;
        var linear = _model.Fill with { Kind = ShapeFillKind.Gradient, Opacity = 1, Gradient = _model.Fill.Gradient with { Kind = GradientKind.Linear, AngleDegrees = 0, Border = 0 } };
        if (_brushes.Fill(linear, bar) is { } brush) context.DrawRectangle(brush, new Pen(_resources.Find(this, Palette.Keys.Border, Brushes.Gray), 1), bar, 3, 3);
        var ink = _resources.Find(this, Palette.Keys.TextPrimary, Brushes.Black);
        var accent = _resources.Find(this, "PulseAccent", Brushes.DodgerBlue);
        var stops = _model.Fill.Gradient.Stops;
        for (var i = 0; i < stops.Count; i++)
        {
            var x = bar.X + stops[i].Position * bar.Width;
            var centre = new Point(x, bar.Bottom + HandleRadius + 2);
            var selected = i == _model.SelectedStop;
            var fill = new SolidColorBrush(_brushes.Colour(stops[i].ColorHex, Colors.Black));
            context.DrawLine(new Pen(ink, 1), new Point(x, bar.Bottom - 3), new Point(x, centre.Y - HandleRadius));
            context.DrawEllipse(fill, new Pen(selected ? accent : ink, selected ? 2.5 : 1), centre, HandleRadius, HandleRadius);
            if (selected && IsFocused) context.DrawEllipse(null, new Pen(accent, 1) { DashStyle = new DashStyle([1, 2], 0) }, centre, HandleRadius + 3, HandleRadius + 3);
        }
    }
}

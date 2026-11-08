using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Fields;

public interface IAngleDialGeometry
{
    bool IsHalf(double minimum, double maximum);

    Point Centre(Rect area, double minimum, double maximum);

    double Radius(Rect area, double minimum, double maximum);

    double AngleAt(Rect area, double minimum, double maximum, Point point, double snap);

    Point Tip(Rect area, double minimum, double maximum, double degrees);
}

public sealed class AngleDialGeometry : IAngleDialGeometry
{
    public bool IsHalf(double minimum, double maximum) => maximum - minimum <= 180 && minimum >= -90 && maximum <= 90;

    public Point Centre(Rect area, double minimum, double maximum) =>
        IsHalf(minimum, maximum) ? new Point(area.Left + 6, area.Center.Y) : area.Center;

    public double Radius(Rect area, double minimum, double maximum) =>
        IsHalf(minimum, maximum)
            ? Math.Max(4, Math.Min(area.Width - 12, area.Height / 2 - 4))
            : Math.Max(4, Math.Min(area.Width, area.Height) / 2 - 4);

    public double AngleAt(Rect area, double minimum, double maximum, Point point, double snap)
    {
        var centre = Centre(area, minimum, maximum);
        var degrees = Math.Atan2(centre.Y - point.Y, point.X - centre.X) * 180 / Math.PI;
        if (!IsHalf(minimum, maximum) && degrees < minimum) degrees += 360;
        if (snap > 0) degrees = Math.Round(degrees / snap) * snap;
        return Math.Clamp(degrees, minimum, maximum);
    }

    public Point Tip(Rect area, double minimum, double maximum, double degrees)
    {
        var centre = Centre(area, minimum, maximum);
        var radius = Radius(area, minimum, maximum);
        var radians = degrees * Math.PI / 180;
        return new Point(centre.X + radius * Math.Cos(radians), centre.Y - radius * Math.Sin(radians));
    }
}

public sealed class AngleDial : Control
{
    private const double FullTurn = 360;

    private readonly IAngleDialGeometry _geometry;
    private readonly ResourceBrushes _brushes = new();
    private readonly double _minimum;
    private readonly double _maximum;
    private double _value;
    private bool _dragging;

    public AngleDial(IAngleDialGeometry geometry, double minimum, double maximum, double initial, string accessibleName)
    {
        _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        _minimum = Turn(minimum, -FullTurn);
        _maximum = Turn(maximum, FullTurn);
        if (_minimum > _maximum) (_minimum, _maximum) = (_maximum, _minimum);
        _value = Math.Clamp(double.IsFinite(initial) ? initial : 0, _minimum, _maximum);
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);
        AutomationProperties.SetName(this, accessibleName);
        ToolTip.SetTip(this, accessibleName);
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public event EventHandler<double>? ValueChanged;

    public double Value
    {
        get => _value;
        set
        {
            if (!double.IsFinite(value)) return;
            var clamped = Math.Clamp(value, _minimum, _maximum);
            if (Math.Abs(clamped - _value) < 1e-9) return;
            _value = clamped;
            InvalidateVisual();
        }
    }

    public Rect Area => new Rect(Bounds.Size).Deflate(2);

    public void PointAt(Point point, bool fine)
    {
        Commit(_geometry.AngleAt(Area, _minimum, _maximum, point, fine ? 1 : 5));
    }

    public void Step(double degrees) => Commit(_value + degrees);

    protected override Size MeasureOverride(Size availableSize) =>
        _geometry.IsHalf(_minimum, _maximum) ? new Size(64, 112) : new Size(104, 104);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Focus(NavigationMethod.Pointer);
        _dragging = true;
        e.Pointer.Capture(this);
        PointAt(e.GetPosition(this), e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging) PointAt(e.GetPosition(this), e.KeyModifiers.HasFlag(KeyModifiers.Shift));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var large = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 15 : 1;
        switch (e.Key)
        {
            case Key.Up or Key.Right:
                Step(large);
                break;
            case Key.Down or Key.Left:
                Step(-large);
                break;
            case Key.Home:
                Commit(_minimum);
                break;
            case Key.End:
                Commit(_maximum);
                break;
            case Key.D0 or Key.NumPad0:
                Commit(Math.Clamp(0, _minimum, _maximum));
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
        var area = Area;
        if (area.Width <= 0 || area.Height <= 0) return;

        var centre = _geometry.Centre(area, _minimum, _maximum);
        var radius = _geometry.Radius(area, _minimum, _maximum);
        var track = _brushes.Find(this, Palette.Keys.BorderStrong, Brushes.Gray);
        var ink = _brushes.Find(this, Palette.Keys.TextSecondary, Brushes.Gray);
        var accent = _brushes.Find(this, "PulseAccent", Brushes.SteelBlue);
        var face = _brushes.Find(this, Palette.Keys.Surface, Brushes.White);

        var half = _geometry.IsHalf(_minimum, _maximum);
        var outline = new Pen(track, 1);
        if (half)
        {
            var arc = new StreamGeometry();
            using (var g = arc.Open())
            {
                g.BeginFigure(new Point(centre.X, centre.Y - radius), true);
                g.ArcTo(new Point(centre.X, centre.Y + radius), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
                g.EndFigure(true);
            }
            context.DrawGeometry(face, outline, arc);
        }
        else
        {
            context.DrawEllipse(face, outline, centre, radius, radius);
        }

        var tickStep = half ? 15 : 30;
        for (var degrees = Math.Ceiling(_minimum / tickStep) * tickStep; degrees <= _maximum; degrees += tickStep)
        {
            var outer = _geometry.Tip(area, _minimum, _maximum, degrees);
            var major = Math.Abs(degrees % 45) < 1e-9;
            var inner = Lerp(centre, outer, major ? 0.82 : 0.9);
            context.DrawLine(new Pen(major ? ink : track, 1), inner, outer);
        }

        var tip = _geometry.Tip(area, _minimum, _maximum, _value);
        context.DrawLine(new Pen(accent, 2, lineCap: PenLineCap.Round), centre, Lerp(centre, tip, 0.86));
        context.DrawEllipse(accent, null, Lerp(centre, tip, 0.86), 4, 4);
        context.DrawEllipse(accent, null, centre, 2.5, 2.5);

        if (IsFocused)
        {
            var ring = new Pen(accent, 1, new ImmutableDashStyle([2, 2], 0));
            context.DrawRectangle(null, ring, new Rect(Bounds.Size).Deflate(0.5), 4, 4);
        }
    }

    private double Turn(double degrees, double fallback) =>
        double.IsFinite(degrees) ? Math.Clamp(degrees, -FullTurn, FullTurn) : fallback;

    private void Commit(double degrees)
    {
        if (!double.IsFinite(degrees)) return;
        var clamped = Math.Clamp(Math.Round(degrees), _minimum, _maximum);
        if (Math.Abs(clamped - _value) < 1e-9) return;
        _value = clamped;
        InvalidateVisual();
        ValueChanged?.Invoke(this, clamped);
    }

    private static Point Lerp(Point from, Point to, double amount) =>
        new(from.X + (to.X - from.X) * amount, from.Y + (to.Y - from.Y) * amount);
}

public sealed class AngleField : UserControl
{
    private readonly AngleDial _dial;
    private readonly MeasureField _field;
    private bool _syncing;

    public AngleField(MeasureField field, IAngleDialGeometry geometry, double spacing, string accessibleName)
    {
        _field = field ?? throw new ArgumentNullException(nameof(field));
        _dial = new AngleDial(geometry, field.Entry.Bounds.Minimum, field.Entry.Bounds.Maximum, field.Value, accessibleName);
        _dial.ValueChanged += (_, degrees) =>
        {
            if (_syncing) return;
            _syncing = true;
            _field.Value = degrees;
            _syncing = false;
            ValueChanged?.Invoke(this, degrees);
        };
        _field.ValueCommitted += (_, degrees) =>
        {
            if (_syncing) return;
            _syncing = true;
            _dial.Value = degrees;
            _syncing = false;
            ValueChanged?.Invoke(this, degrees);
        };
        _field.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;

        Content = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = spacing,
            Children = { _dial, _field },
        };
    }

    public event EventHandler<double>? ValueChanged;

    public AngleDial Dial => _dial;

    public MeasureField Field => _field;

    public double Read() => _field.Read();
}

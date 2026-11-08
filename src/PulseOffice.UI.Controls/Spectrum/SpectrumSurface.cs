using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace PulseOffice.UI.Controls.Spectrum;

public abstract class SpectrumSurface : Control
{
    private static readonly IPen ThumbOuter = new Pen(new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)), 1);
    private static readonly IPen ThumbInner = new Pen(Brushes.White, 2);

    private bool _dragging;

    protected SpectrumSurface()
    {
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Cross);
    }

    public event EventHandler? Picked;

    protected abstract void PickAt(Point point, Size size);

    protected abstract void Nudge(Key key, bool large);

    protected void Raise()
    {
        InvalidateVisual();
        Picked?.Invoke(this, EventArgs.Empty);
    }

    protected void DrawThumb(DrawingContext context, Point centre, double radius)
    {
        context.DrawEllipse(null, ThumbOuter, centre, radius + 1.5, radius + 1.5);
        context.DrawEllipse(null, ThumbInner, centre, radius, radius);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        _dragging = true;
        e.Pointer.Capture(this);
        Focus(NavigationMethod.Pointer);
        PickAt(e.GetPosition(this), Bounds.Size);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging) PickAt(e.GetPosition(this), Bounds.Size);
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
        if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down)) return;

        Nudge(e.Key, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        e.Handled = true;
    }

    protected static double Fraction(double position, double length) =>
        length <= 0 || !double.IsFinite(position) || !double.IsFinite(length) ? 0 : Math.Clamp(position / length, 0, 1);
}

public sealed class SaturationValueArea : SpectrumSurface
{
    private SpectrumColor _colour = new(0, 1, 1);

    public SpectrumColor Colour
    {
        get => _colour;
        set
        {
            _colour = value;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(Bounds.Size);
        var pure = new SpectrumColor(_colour.Hue, 1, 1).ToRgb();
        var geometry = new RoundedRect(rect, 4);

        using (context.PushClip(geometry))
        {
            context.FillRectangle(new SolidColorBrush(Color.FromRgb(pure.Red, pure.Green, pure.Blue)), rect);
            context.FillRectangle(Gradient(new RelativePoint(0, 0, RelativeUnit.Relative), new RelativePoint(1, 0, RelativeUnit.Relative), Colors.White, Color.FromArgb(0, 255, 255, 255)), rect);
            context.FillRectangle(Gradient(new RelativePoint(0, 0, RelativeUnit.Relative), new RelativePoint(0, 1, RelativeUnit.Relative), Color.FromArgb(0, 0, 0, 0), Colors.Black), rect);
        }

        DrawThumb(context, new Point(_colour.Saturation * rect.Width, (1 - _colour.Value) * rect.Height), 6);
    }

    protected override void PickAt(Point point, Size size)
    {
        _colour = _colour.WithShade(Fraction(point.X, size.Width), 1 - Fraction(point.Y, size.Height));
        Raise();
    }

    protected override void Nudge(Key key, bool large)
    {
        var step = large ? 0.1 : 0.01;
        _colour = key switch
        {
            Key.Left => _colour.WithShade(_colour.Saturation - step, _colour.Value),
            Key.Right => _colour.WithShade(_colour.Saturation + step, _colour.Value),
            Key.Up => _colour.WithShade(_colour.Saturation, _colour.Value + step),
            _ => _colour.WithShade(_colour.Saturation, _colour.Value - step),
        };
        Raise();
    }

    private static LinearGradientBrush Gradient(RelativePoint start, RelativePoint end, Color from, Color to) => new()
    {
        StartPoint = start,
        EndPoint = end,
        GradientStops = { new GradientStop(from, 0), new GradientStop(to, 1) },
    };
}

public sealed class HueStrip : SpectrumSurface
{
    private double _hue;

    public double Hue
    {
        get => _hue;
        set
        {
            _hue = double.IsFinite(value) ? Math.Clamp(value, 0, 360) : 0;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(Bounds.Size);
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
        };

        for (var step = 0; step <= 6; step++)
        {
            var rgb = new SpectrumColor(step * 60, 1, 1).ToRgb();
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(rgb.Red, rgb.Green, rgb.Blue), step / 6.0));
        }

        context.DrawRectangle(brush, null, new RoundedRect(rect, rect.Height / 2));
        DrawThumb(context, new Point(_hue / 360 * rect.Width, rect.Height / 2), rect.Height / 2 - 1);
    }

    protected override void PickAt(Point point, Size size)
    {
        _hue = Fraction(point.X, size.Width) * 360;
        Raise();
    }

    protected override void Nudge(Key key, bool large)
    {
        var step = (large ? 15 : 1) * (key is Key.Left or Key.Down ? -1 : 1);
        _hue = Math.Clamp(_hue + step, 0, 360);
        Raise();
    }
}

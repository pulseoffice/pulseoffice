using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Rulers;

public enum RulerOrientation { Horizontal, Vertical }

public enum RulerMarkerKind { FirstLine, Left, Right, TabLeft, TabCenter, TabRight, TabDecimal, MarginStart, MarginEnd }

public sealed record RulerMarker(string Id, RulerMarkerKind Kind, double Position)
{
    public bool IsTab => Kind is RulerMarkerKind.TabLeft or RulerMarkerKind.TabCenter or RulerMarkerKind.TabRight or RulerMarkerKind.TabDecimal;

    public bool IsMargin => Kind is RulerMarkerKind.MarginStart or RulerMarkerKind.MarginEnd;
}

public sealed record RulerModel(
    RulerOrientation Orientation,
    double Origin,
    double Scale,
    double Length,
    double ActiveStart,
    double ActiveEnd,
    IReadOnlyList<RulerMarker> Markers,
    double UnitPoints)
{
    public static readonly RulerModel Empty = new(RulerOrientation.Horizontal, 0, 1, 0, 0, 0, [], 72 / 2.54);

    public double ToPixels(double points) => Origin + points * Scale;

    public double ToPoints(double pixels) => (pixels - Origin) / Math.Max(0.0001, Scale);
}

public sealed record RulerMarkerMove(RulerMarker Marker, double Position);

public sealed class RulerPress(double position, PointerPressedEventArgs pointer) : EventArgs
{
    public double Position { get; } = position;

    public PointerPressedEventArgs Pointer { get; } = pointer;

    public bool Handled { get; set; }
}

public sealed class RulerStrip : Control
{
    public const double Thickness = 20;

    private const double Grab = 5;
    private const double TearOff = 24;

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<RulerStrip, IBrush?>(nameof(Background));

    public static readonly StyledProperty<IBrush?> ActiveProperty =
        AvaloniaProperty.Register<RulerStrip, IBrush?>(nameof(Active));

    public static readonly StyledProperty<IBrush?> GutterProperty =
        AvaloniaProperty.Register<RulerStrip, IBrush?>(nameof(Gutter));

    public static readonly StyledProperty<IBrush?> InkProperty =
        AvaloniaProperty.Register<RulerStrip, IBrush?>(nameof(Ink));

    public static readonly StyledProperty<IBrush?> AccentProperty =
        AvaloniaProperty.Register<RulerStrip, IBrush?>(nameof(Accent));

    private readonly RulerTicks _ticks = new(6);
    private RulerModel _model = RulerModel.Empty;
    private RulerMarker? _dragged;
    private double _dragAt;
    private bool _torn;

    static RulerStrip()
    {
        AffectsRender<RulerStrip>(BackgroundProperty, ActiveProperty, GutterProperty, InkProperty, AccentProperty);
    }

    public RulerStrip(RulerOrientation orientation)
    {
        Orientation = orientation;
        if (orientation == RulerOrientation.Horizontal) Height = Thickness;
        else Width = Thickness;
        ClipToBounds = true;
        Bind(BackgroundProperty, this.GetResourceObservable(Palette.Keys.SurfaceSunken));
        Bind(ActiveProperty, this.GetResourceObservable(Palette.Keys.Surface));
        Bind(GutterProperty, this.GetResourceObservable(Palette.Keys.Border));
        Bind(InkProperty, this.GetResourceObservable(Palette.Keys.TextSecondary));
        Bind(AccentProperty, this.GetResourceObservable("PulseAccent"));
    }

    public RulerOrientation Orientation { get; }

    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public IBrush? Active
    {
        get => GetValue(ActiveProperty);
        set => SetValue(ActiveProperty, value);
    }

    public IBrush? Gutter
    {
        get => GetValue(GutterProperty);
        set => SetValue(GutterProperty, value);
    }

    public IBrush? Ink
    {
        get => GetValue(InkProperty);
        set => SetValue(InkProperty, value);
    }

    public IBrush? Accent
    {
        get => GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    public RulerModel Model
    {
        get => _model;
        set
        {
            _model = value ?? RulerModel.Empty;
            InvalidateVisual();
        }
    }

    public event EventHandler<RulerMarkerMove>? MarkerMoved;

    public event EventHandler<RulerMarker>? MarkerRemoved;

    public event EventHandler<double>? Clicked;

    public event EventHandler<RulerPress>? BlankPressed;

    public override void Render(DrawingContext context)
    {
        var along = Along(Bounds.Size);
        var across = Across(Bounds.Size);
        context.FillRectangle(Background ?? Brushes.WhiteSmoke, new Rect(Bounds.Size));
        if (_model.Length <= 0) return;
        var start = _model.ToPixels(0);
        var end = _model.ToPixels(_model.Length);
        context.FillRectangle(Gutter ?? Brushes.Gainsboro, Span(start, end, across, 3));
        context.FillRectangle(Active ?? Brushes.White, Span(_model.ToPixels(_model.ActiveStart), _model.ToPixels(_model.ActiveEnd), across, 3));
        Ticks(context, along, across);
        foreach (var marker in _model.Markers) Marker(context, marker, across);
        if (_dragged is not null) Guide(context, _dragAt, across);
    }

    private void Ticks(DrawingContext context, double along, double across)
    {
        var ink = new Pen(Ink ?? Brushes.Gray, 1);
        var sample = LabelText(8);
        var extent = Orientation == RulerOrientation.Horizontal ? sample.Width : sample.Height;
        foreach (var tick in _ticks.Plan(_model, _model.ToPoints(0), _model.ToPoints(along), extent))
        {
            var pixel = _model.ToPixels(tick.Points);
            switch (tick.Kind)
            {
                case RulerTickKind.Label:
                    Label(context, tick.Value, pixel, across);
                    break;
                case RulerTickKind.Whole:
                    Line(context, ink, pixel, across * 0.35, across * 0.65);
                    break;
                default:
                    var length = tick.Kind == RulerTickKind.Half ? across * 0.3 : across * 0.15;
                    Line(context, ink, pixel, across / 2 - length / 2, across / 2 + length / 2);
                    break;
            }
        }
    }

    private FormattedText LabelText(int value) =>
        new(value.ToString(CultureInfo.CurrentCulture), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 9, Ink ?? Brushes.Gray);

    private void Label(DrawingContext context, int value, double pixel, double across)
    {
        var text = LabelText(value);
        var origin = Orientation == RulerOrientation.Horizontal
            ? new Point(pixel - text.Width / 2, (across - text.Height) / 2)
            : new Point((across - text.Width) / 2, pixel - text.Height / 2);
        context.DrawText(text, origin);
    }

    private void Line(DrawingContext context, Pen pen, double pixel, double from, double to)
    {
        if (Orientation == RulerOrientation.Horizontal) context.DrawLine(pen, new Point(pixel + 0.5, from), new Point(pixel + 0.5, to));
        else context.DrawLine(pen, new Point(from, pixel + 0.5), new Point(to, pixel + 0.5));
    }

    private void Guide(DrawingContext context, double pixel, double across)
    {
        var pen = new Pen(Accent ?? Brushes.DodgerBlue, 1, new DashStyle([2, 2], 0));
        Line(context, pen, pixel, 0, across);
    }

    private Rect Span(double from, double to, double across, double inset) =>
        Orientation == RulerOrientation.Horizontal
            ? new Rect(Math.Min(from, to), inset, Math.Abs(to - from), across - inset * 2)
            : new Rect(inset, Math.Min(from, to), across - inset * 2, Math.Abs(to - from));

    private void Marker(DrawingContext context, RulerMarker marker, double across)
    {
        var pixel = ReferenceEquals(marker, _dragged) ? _dragAt : _model.ToPixels(marker.Position);
        if (_torn && ReferenceEquals(marker, _dragged)) return;
        var fill = marker.IsMargin ? Ink ?? Brushes.Gray : Accent ?? Brushes.DodgerBlue;
        var pen = new Pen(fill, 1.4);
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            switch (marker.Kind)
            {
                case RulerMarkerKind.FirstLine:
                    Triangle(g, pixel, 2, 7, down: true);
                    break;
                case RulerMarkerKind.Left or RulerMarkerKind.Right:
                    Triangle(g, pixel, across - 2, 7, down: false);
                    break;
                case RulerMarkerKind.MarginStart or RulerMarkerKind.MarginEnd:
                    Bar(g, pixel, across);
                    break;
            }
        }
        if (marker.IsTab)
        {
            Tab(context, pen, marker.Kind, pixel, across);
            return;
        }
        context.DrawGeometry(marker.IsMargin ? null : fill, marker.IsMargin ? pen : null, geometry);
    }

    private void Triangle(StreamGeometryContext g, double pixel, double edge, double size, bool down)
    {
        var tip = down ? edge + size : edge - size;
        g.BeginFigure(At(pixel - size / 2, edge), true);
        g.LineTo(At(pixel + size / 2, edge));
        g.LineTo(At(pixel, tip));
        g.EndFigure(true);
    }

    private void Bar(StreamGeometryContext g, double pixel, double across)
    {
        g.BeginFigure(At(pixel, 2), false);
        g.LineTo(At(pixel, across - 2));
        g.EndFigure(false);
    }

    private void Tab(DrawingContext context, Pen pen, RulerMarkerKind kind, double pixel, double across)
    {
        var bottom = across - 4;
        var top = bottom - 6;
        context.DrawLine(pen, At(pixel, top), At(pixel, bottom));
        switch (kind)
        {
            case RulerMarkerKind.TabLeft:
                context.DrawLine(pen, At(pixel, bottom), At(pixel + 5, bottom));
                break;
            case RulerMarkerKind.TabRight:
                context.DrawLine(pen, At(pixel - 5, bottom), At(pixel, bottom));
                break;
            case RulerMarkerKind.TabCenter:
                context.DrawLine(pen, At(pixel - 4, bottom), At(pixel + 4, bottom));
                break;
            default:
                context.DrawLine(pen, At(pixel - 4, bottom), At(pixel + 4, bottom));
                context.DrawEllipse(pen.Brush, null, At(pixel + 3, top + 2), 1, 1);
                break;
        }
    }

    private Point At(double along, double across) =>
        Orientation == RulerOrientation.Horizontal ? new Point(along, across) : new Point(across, along);

    private double Along(Size size) => Orientation == RulerOrientation.Horizontal ? size.Width : size.Height;

    private double Across(Size size) => Orientation == RulerOrientation.Horizontal ? size.Height : size.Width;

    private double Along(Point point) => Orientation == RulerOrientation.Horizontal ? point.X : point.Y;

    private double Across(Point point) => Orientation == RulerOrientation.Horizontal ? point.Y : point.X;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetPosition(this);
        var along = Along(point);
        var upper = Across(point) < Across(Bounds.Size) / 2;
        _dragged = _model.Markers
            .Where(marker => Math.Abs(_model.ToPixels(marker.Position) - along) <= Grab)
            .OrderBy(marker => Fits(marker, upper) ? 0 : 1)
            .ThenBy(marker => Math.Abs(_model.ToPixels(marker.Position) - along))
            .ThenBy(marker => marker.IsMargin)
            .FirstOrDefault();
        if (_dragged is null)
        {
            var position = _model.ToPoints(along);
            var press = new RulerPress(position, e);
            BlankPressed?.Invoke(this, press);
            if (press.Handled)
            {
                e.Handled = true;
                return;
            }
            if (position >= _model.ActiveStart && position <= _model.ActiveEnd) Clicked?.Invoke(this, Snap(position, e.KeyModifiers));
            e.Handled = true;
            return;
        }
        _dragAt = along;
        _torn = false;
        e.Pointer.Capture(this);
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        if (_dragged is null)
        {
            var near = _model.Markers.Any(marker => Math.Abs(_model.ToPixels(marker.Position) - Along(point)) <= Grab);
            Cursor = near ? new Cursor(Orientation == RulerOrientation.Horizontal ? StandardCursorType.SizeWestEast : StandardCursorType.SizeNorthSouth) : null;
            return;
        }
        _dragAt = _model.ToPixels(Snap(_model.ToPoints(Along(point)), e.KeyModifiers));
        _torn = _dragged.IsTab && (Across(point) < -TearOff || Across(point) > Across(Bounds.Size) + TearOff);
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragged is not { } marker) return;
        var position = _model.ToPoints(_dragAt);
        var torn = _torn;
        _dragged = null;
        _torn = false;
        e.Pointer.Capture(null);
        InvalidateVisual();
        if (torn) MarkerRemoved?.Invoke(this, marker);
        else if (Math.Abs(position - marker.Position) > 0.01) MarkerMoved?.Invoke(this, new RulerMarkerMove(marker, position));
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_dragged is null) return;
        _dragged = null;
        _torn = false;
        InvalidateVisual();
    }

    private bool Fits(RulerMarker marker, bool upper) =>
        marker.IsMargin || (marker.Kind == RulerMarkerKind.FirstLine) == upper;

    private double Snap(double points, KeyModifiers modifiers)
    {
        if (modifiers.HasFlag(KeyModifiers.Alt)) return points;
        var step = _model.UnitPoints / 8;
        return Math.Round(points / step) * step;
    }
}

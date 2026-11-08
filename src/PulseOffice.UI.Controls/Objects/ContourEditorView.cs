using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Objects;

public sealed class ContourCanvas : Control
{
    private const double Inset = 16;
    private const double Grab = 7;

    private readonly List<Point> _points;
    private readonly Bitmap? _picture;
    private int _dragged = -1;

    public ContourCanvas(IReadOnlyList<Point> points, Bitmap? picture)
    {
        _points = points.Count >= 3 ? [.. points] : [new(0, 0), new(1, 0), new(1, 1), new(0, 1)];
        _picture = picture;
        Width = 460;
        Height = 360;
        Focusable = true;
    }

    public IReadOnlyList<Point> Points => _points;

    public event EventHandler? Edited;

    private Rect Area => new(Inset, Inset, Math.Max(1, Bounds.Width - Inset * 2), Math.Max(1, Bounds.Height - Inset * 2));

    private Point ToScreen(Point unit) => new(Area.X + unit.X * Area.Width, Area.Y + unit.Y * Area.Height);

    private Point ToUnit(Point screen) => new(Math.Clamp((screen.X - Area.X) / Area.Width, 0, 1), Math.Clamp((screen.Y - Area.Y) / Area.Height, 0, 1));

    public override void Render(DrawingContext context)
    {
        var area = Area;
        context.FillRectangle(this.FindResource(Palette.Keys.SurfaceSunken) as IBrush ?? Brushes.WhiteSmoke, new Rect(Bounds.Size));
        if (_picture is not null) context.DrawImage(_picture, area);
        else context.DrawRectangle(Brushes.White, new Pen(Brushes.LightGray, 1), area);
        var accent = this.FindResource("PulseAccent") as IBrush ?? Brushes.DodgerBlue;
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(ToScreen(_points[0]), true);
            foreach (var point in _points.Skip(1)) g.LineTo(ToScreen(point));
            g.EndFigure(true);
        }
        context.DrawGeometry(new SolidColorBrush(Color.FromArgb(40, 37, 99, 235)), new Pen(accent, 1.5), geometry);
        foreach (var point in _points) context.DrawRectangle(Brushes.White, new Pen(accent, 1.5), new Rect(ToScreen(point) - new Point(4, 4), new Size(8, 8)));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var at = e.GetPosition(this);
        var hit = _points.FindIndex(point => Distance(ToScreen(point), at) <= Grab);
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            if (hit >= 0 && _points.Count > 3)
            {
                _points.RemoveAt(hit);
                Changed();
            }
            e.Handled = true;
            return;
        }
        if (hit < 0 && e.ClickCount >= 2)
        {
            var edge = Enumerable.Range(0, _points.Count).MinBy(index => EdgeDistance(ToScreen(_points[index]), ToScreen(_points[(index + 1) % _points.Count]), at));
            _points.Insert(edge + 1, ToUnit(at));
            Changed();
            e.Handled = true;
            return;
        }
        _dragged = hit;
        if (hit >= 0) e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragged < 0) return;
        _points[_dragged] = ToUnit(e.GetPosition(this));
        Changed();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragged = -1;
        e.Pointer.Capture(null);
    }

    private void Changed()
    {
        InvalidateVisual();
        Edited?.Invoke(this, EventArgs.Empty);
    }

    private double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private double EdgeDistance(Point a, Point b, Point p)
    {
        var length = (b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y);
        if (length < 1e-9) return Distance(a, p);
        var t = Math.Clamp(((p.X - a.X) * (b.X - a.X) + (p.Y - a.Y) * (b.Y - a.Y)) / length, 0, 1);
        return Distance(new Point(a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y)), p);
    }
}

public sealed class ContourEditorView : UserControl, IDialogContent, IDialogFooterOwner
{
    private readonly ContourCanvas _canvas;

    public ContourEditorView(IReadOnlyList<Point> points, Bitmap? picture, ITextCatalog texts)
    {
        _canvas = new ContourCanvas(points, picture);
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = texts["contour.explanation"], TextWrapping = TextWrapping.Wrap, MaxWidth = 460 });
        panel.Children.Add(_canvas);
        Content = panel;
        Footer = new DialogFooter()
            .WithCancel(texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(texts["prompt.confirm"], () => Completed?.Invoke(this, _canvas.Points.ToList()), primary: true);
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }
}

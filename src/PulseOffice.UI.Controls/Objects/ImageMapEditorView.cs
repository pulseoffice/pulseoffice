using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PulseOffice.Document.Embedding;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Objects;

public sealed class ImageMapEditorView : UserControl, IDialogContent, IDialogFooterOwner
{
    private readonly ImageMapCanvas _canvas;
    private readonly ListBox _list = new();
    private readonly TextBox _target = new();
    private readonly TextBox _description = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 64 };
    private readonly Button _delete;
    private readonly ITextCatalog _texts;
    private readonly DesignTokens.Metrics _metrics;
    private readonly List<ToggleButton> _toolButtons = [];
    private bool _loading;

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public ImageMapEditorView(Bitmap? image, ImageMap map, ITextCatalog texts, InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(map);
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _metrics = DesignTokens.For(density);

        _canvas = new ImageMapCanvas(image, map.Areas);
        _canvas.Changed += (_, _) => Refresh();

        _delete = new Button { Content = texts["imageMap.delete"], FontSize = _metrics.FontSizeSmall };
        _delete.Click += (_, _) => _canvas.DeleteSelected();

        _target.PlaceholderText = "https://…";
        Avalonia.Automation.AutomationProperties.SetName(_target, texts["imageMap.target"]);
        Avalonia.Automation.AutomationProperties.SetName(_description, texts["imageMap.description"]);
        Avalonia.Automation.AutomationProperties.SetName(_list, texts["imageMap.areas"]);
        _target.TextChanged += (_, _) => Edit(area => area with { Target = _target.Text?.Trim() ?? string.Empty });
        _description.TextChanged += (_, _) => Edit(area => area with { Description = _description.Text ?? string.Empty });
        _list.SelectionChanged += (_, _) =>
        {
            if (!_loading) _canvas.Select(_list.SelectedIndex);
        };

        Content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,260"),
            ColumnSpacing = _metrics.SpacingLarge,
            Children =
            {
                Left(),
                Right(),
            },
        };

        Footer = new DialogFooter(density)
            .WithCancel(texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(texts["prompt.confirm"], () => Completed?.Invoke(this, Result()), primary: true);

        Choose(ImageMapTool.Rectangle);
        Refresh();
    }

    private Control Left()
    {
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = _metrics.SpacingSmall };
        foreach (var tool in Enum.GetValues<ImageMapTool>())
        {
            var button = new ToggleButton
            {
                Content = _texts[$"imageMap.tool.{tool.ToString().ToLowerInvariant()}"],
                FontSize = _metrics.FontSizeSmall,
                Tag = tool,
                CornerRadius = new CornerRadius(_metrics.RadiusTight),
            };
            button.Click += (_, _) => Choose(tool);
            _toolButtons.Add(button);
            tools.Children.Add(button);
        }

        var frame = new Border
        {
            Child = _canvas,
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Height = 380,
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border)
         .Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken);

        var hint = new TextBlock { FontSize = _metrics.FontSizeSmall, TextWrapping = TextWrapping.Wrap, Text = _texts["imageMap.hint"] }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        var column = new StackPanel { Spacing = _metrics.SpacingMedium, Children = { tools, frame, hint } };
        Grid.SetColumn(column, 0);
        return column;
    }

    private Control Right()
    {
        _list.MaxHeight = 170;
        _list.FontSize = _metrics.FontSizeSmall;
        var column = new StackPanel
        {
            Spacing = _metrics.SpacingSmall,
            Children =
            {
                Label("imageMap.areas"),
                new Border { Child = _list, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(_metrics.RadiusTight), MinHeight = 120 }
                    .Themed(Border.BorderBrushProperty, Palette.Keys.Border),
                Label("imageMap.target"),
                _target,
                Label("imageMap.description"),
                _description,
                _delete,
            },
        };
        Grid.SetColumn(column, 1);
        return column;
    }

    private TextBlock Label(string key) =>
        new TextBlock { Text = _texts[key].ToUpperInvariant(), FontSize = _metrics.FontSizeSmall * 0.9, LetterSpacing = 0.6, Margin = new Thickness(0, _metrics.SpacingSmall, 0, 0) }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

    private void Choose(ImageMapTool tool)
    {
        _canvas.Tool = tool;
        foreach (var button in _toolButtons) button.IsChecked = (ImageMapTool)button.Tag! == tool;
    }

    private void Edit(Func<ImageMapArea, ImageMapArea> change)
    {
        if (!_loading) _canvas.ChangeSelected(change);
    }

    private void Refresh()
    {
        _loading = true;
        _list.ItemsSource = _canvas.Areas
            .Select((area, index) => area.Target.Length > 0 ? area.Target : _texts.Format("imageMap.area", index + 1))
            .ToList();
        _list.SelectedIndex = _canvas.SelectedIndex;

        var selected = _canvas.Selected;
        _target.IsEnabled = _description.IsEnabled = _delete.IsEnabled = selected is not null;
        if (_target.Text != (selected?.Target ?? string.Empty)) _target.Text = selected?.Target ?? string.Empty;
        if (_description.Text != (selected?.Description ?? string.Empty)) _description.Text = selected?.Description ?? string.Empty;
        _loading = false;
    }

    private ImageMap Result()
    {
        var map = new ImageMap();
        map.Replace(_canvas.Areas);
        return map;
    }
}

public enum ImageMapTool { Select, Rectangle, Circle, Polygon }

internal sealed class ImageMapCanvas : Control
{
    private readonly Bitmap? _image;
    private readonly List<ImageMapArea> _areas;
    private DraftGesture? _gesture;
    private int _counter;

    public ImageMapCanvas(Bitmap? image, IReadOnlyList<ImageMapArea> areas)
    {
        _image = image;
        _areas = [.. areas];
        _counter = _areas.Count;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Cross);
    }

    public event EventHandler? Changed;

    public ImageMapTool Tool { get; set; } = ImageMapTool.Rectangle;

    public IReadOnlyList<ImageMapArea> Areas => _areas;

    public int SelectedIndex { get; private set; } = -1;

    public ImageMapArea? Selected => SelectedIndex >= 0 && SelectedIndex < _areas.Count ? _areas[SelectedIndex] : null;

    public void Select(int index)
    {
        SelectedIndex = index >= 0 && index < _areas.Count ? index : -1;
        Notify();
    }

    public void ChangeSelected(Func<ImageMapArea, ImageMapArea> change)
    {
        if (Selected is not { } area) return;
        _areas[SelectedIndex] = change(area);
        Notify();
    }

    public void DeleteSelected()
    {
        if (Selected is null) return;
        _areas.RemoveAt(SelectedIndex);
        SelectedIndex = -1;
        Notify();
    }

    internal Rect ImageRect()
    {
        var size = _image?.Size ?? new Size(4, 3);
        if (size.Width <= 0 || size.Height <= 0) size = new Size(4, 3);
        var scale = Math.Max(0, Math.Min(Bounds.Width / size.Width, Bounds.Height / size.Height));
        var width = size.Width * scale;
        var height = size.Height * scale;
        return new Rect((Bounds.Width - width) / 2, (Bounds.Height - height) / 2, width, height);
    }

    internal (double X, double Y) Normalise(Point point)
    {
        var rect = ImageRect();
        if (rect.Width <= 0 || rect.Height <= 0) return (0, 0);
        return (Math.Clamp((point.X - rect.X) / rect.Width, 0, 1), Math.Clamp((point.Y - rect.Y) / rect.Height, 0, 1));
    }

    internal Point Place((double X, double Y) point)
    {
        var rect = ImageRect();
        return new Point(rect.X + point.X * rect.Width, rect.Y + point.Y * rect.Height);
    }

    internal void Add(ImageMapShape shape, IReadOnlyList<(double X, double Y)> points)
    {
        string id;
        do id = $"area{++_counter}";
        while (_areas.Any(area => area.Id == id));

        _areas.Add(new ImageMapArea { Id = id, Shape = shape, Points = points });
        SelectedIndex = _areas.Count - 1;
        Notify();
    }

    internal void Notify()
    {
        InvalidateVisual();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var point = e.GetPosition(this);

        if (_gesture is PolygonGesture polygon)
        {
            polygon.Press(point, e.ClickCount);
            if (polygon.Done) _gesture = null;
            e.Handled = true;
            return;
        }

        _gesture = Tool switch
        {
            ImageMapTool.Rectangle => new BoxGesture(this, ImageMapShape.Rectangle, point),
            ImageMapTool.Circle => new BoxGesture(this, ImageMapShape.Circle, point),
            ImageMapTool.Polygon => new PolygonGesture(this, point),
            _ => null,
        };

        if (_gesture is null)
        {
            var (x, y) = Normalise(point);
            var hit = -1;
            for (var i = _areas.Count - 1; i >= 0 && hit < 0; i--)
                if (_areas[i].Contains(x, y)) hit = i;
            Select(hit);
        }
        else e.Pointer.Capture(this);

        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _gesture?.Move(e.GetPosition(this));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_gesture is BoxGesture box)
        {
            box.Release(e.GetPosition(this));
            _gesture = null;
        }
        e.Pointer.Capture(null);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && _gesture is not null)
        {
            _gesture = null;
            InvalidateVisual();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && _gesture is PolygonGesture polygon)
        {
            polygon.Finish();
            _gesture = null;
            e.Handled = true;
        }
        else if (e.Key is Key.Delete or Key.Back && Selected is not null)
        {
            DeleteSelected();
            e.Handled = true;
        }
    }

    public override void Render(DrawingContext context)
    {
        var rect = ImageRect();
        if (_image is not null) context.DrawImage(_image, rect);

        var accent = this.FindResource("PulseAccent") as ISolidColorBrush ?? Brushes.DodgerBlue;
        for (var i = 0; i < _areas.Count; i++)
        {
            var selected = i == SelectedIndex;
            var fill = new SolidColorBrush(accent.Color, selected ? 0.38 : 0.2);
            var pen = new Pen(accent, selected ? 2.5 : 1.5);
            if (Geometry(_areas[i]) is { } geometry) context.DrawGeometry(fill, pen, geometry);
        }

        _gesture?.Render(context, new Pen(accent, 1.5, new DashStyle([4, 3], 0)));
    }

    internal Geometry? Geometry(ImageMapArea area)
    {
        if (area.Points.Count < 2) return null;
        var points = area.Points.Select(Place).ToList();

        return area.Shape switch
        {
            ImageMapShape.Rectangle => new RectangleGeometry(new Rect(points[0], points[1]).Normalize()),
            ImageMapShape.Circle => new EllipseGeometry(new Rect(
                points[0].X - Radius(area), points[0].Y - Radius(area), Radius(area) * 2, Radius(area) * 2)),
            _ => new PolylineGeometry(points, true),
        };
    }

    private double Radius(ImageMapArea area)
    {
        var rect = ImageRect();
        var dx = (area.Points[1].X - area.Points[0].X) * rect.Width;
        var dy = (area.Points[1].Y - area.Points[0].Y) * rect.Height;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private abstract class DraftGesture(ImageMapCanvas canvas)
    {
        protected ImageMapCanvas Canvas { get; } = canvas;

        public abstract void Move(Point point);

        public abstract void Render(DrawingContext context, Pen pen);
    }

    private sealed class BoxGesture(ImageMapCanvas canvas, ImageMapShape shape, Point origin) : DraftGesture(canvas)
    {
        private readonly Point _start = origin;
        private Point _end = origin;

        public override void Move(Point point)
        {
            _end = point;
            Canvas.InvalidateVisual();
        }

        public void Release(Point point)
        {
            _end = point;
            if (Math.Abs(_end.X - _start.X) < 4 && Math.Abs(_end.Y - _start.Y) < 4)
            {
                Canvas.InvalidateVisual();
                return;
            }

            var from = Canvas.Normalise(_start);
            var to = Canvas.Normalise(_end);
            Canvas.Add(shape, [from, to]);
        }

        public override void Render(DrawingContext context, Pen pen)
        {
            if (shape == ImageMapShape.Circle)
            {
                var radius = Math.Sqrt(Math.Pow(_end.X - _start.X, 2) + Math.Pow(_end.Y - _start.Y, 2));
                context.DrawEllipse(null, pen, _start, radius, radius);
            }
            else context.DrawRectangle(null, pen, new Rect(_start, _end).Normalize());
        }
    }

    private sealed class PolygonGesture : DraftGesture
    {
        private readonly List<Point> _points;
        private Point _cursor;

        public PolygonGesture(ImageMapCanvas canvas, Point start) : base(canvas)
        {
            _points = [start];
            _cursor = start;
        }

        public bool Done { get; private set; }

        public void Press(Point point, int clicks)
        {
            if (clicks >= 2 || (_points.Count >= 3 && Distance(point, _points[0]) < 8))
            {
                Finish();
                return;
            }
            _points.Add(point);
            Canvas.InvalidateVisual();
        }

        public void Finish()
        {
            Done = true;
            if (_points.Count >= 3) Canvas.Add(ImageMapShape.Polygon, [.. _points.Select(Canvas.Normalise)]);
            else Canvas.InvalidateVisual();
        }

        public override void Move(Point point)
        {
            _cursor = point;
            Canvas.InvalidateVisual();
        }

        public override void Render(DrawingContext context, Pen pen)
        {
            var path = new List<Point>(_points) { _cursor };
            context.DrawGeometry(null, pen, new PolylineGeometry(path, false));
            foreach (var point in _points) context.DrawEllipse(pen.Brush, null, point, 3, 3);
        }

        private double Distance(Point a, Point b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
    }
}

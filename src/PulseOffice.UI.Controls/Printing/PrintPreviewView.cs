using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using PulseOffice.Localization;
using PulseOffice.Printing;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Printing;

public sealed class PrintPreviewView : UserControl, IDialogContent, IDialogFooterOwner
{
    private static readonly double[] ZoomSteps = [0.25, 0.5, 0.75, 1.0, 1.5, 2.0, 3.0];

    private readonly IPrintableDocument _document;
    private readonly IPreviewSpreads _spreads;
    private readonly PrintPagesView _pages;
    private readonly TextBlock _label = new() { VerticalAlignment = VerticalAlignment.Center, MinWidth = 120, TextAlignment = Avalonia.Media.TextAlignment.Center };
    private readonly ComboBox _zoom = new() { MinWidth = 130 };
    private readonly ComboBox _layout = new() { MinWidth = 150 };
    private readonly ITextCatalog _texts;

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public PrintPreviewView(IPrintableDocument document, ITextCatalog texts, IPageCanvasFactory canvases, InterfaceDensity density = InterfaceDensity.Compact, Action? print = null)
        : this(document, texts, new PreviewSpreads(), canvases, density, print)
    {
    }

    public PrintPreviewView(IPrintableDocument document, ITextCatalog texts, IPreviewSpreads spreads, IPageCanvasFactory canvases, InterfaceDensity density = InterfaceDensity.Compact, Action? print = null)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _spreads = spreads ?? throw new ArgumentNullException(nameof(spreads));
        Focusable = true;

        var metrics = DesignTokens.For(density);
        _pages = new PrintPagesView(spreads, canvases) { Document = document };

        _layout.ItemsSource = new[] { texts["print.preview.single"], texts["print.preview.facing"], texts["print.preview.grid"], texts["print.preview.book"] };
        _layout.SelectedIndex = 0;
        Avalonia.Automation.AutomationProperties.SetName(_layout, texts["print.preview.layoutName"]);
        Avalonia.Automation.AutomationProperties.SetName(_zoom, texts["print.preview.zoomName"]);
        _layout.SelectionChanged += (_, _) => ApplyLayout();

        var zooms = new List<string> { texts["print.preview.fit"], texts["print.preview.fitWidth"] };
        zooms.AddRange(ZoomSteps.Select(step => $"{step * 100:0}%"));
        _zoom.ItemsSource = zooms;
        _zoom.SelectedIndex = 0;
        _zoom.SelectionChanged += (_, _) => ApplyZoom();

        var bar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = metrics.SpacingSmall,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        bar.Children.Add(Button("⏮", texts["print.preview.firstPage"], () => Show(0)));
        bar.Children.Add(Button("‹", texts["print.preview.previous"], () => Move(-1)));
        bar.Children.Add(_label);
        bar.Children.Add(Button("›", texts["print.preview.next"], () => Move(1)));
        bar.Children.Add(Button("⏭", texts["print.preview.lastPage"], () => Show(_document.PageCount - 1)));
        bar.Children.Add(new Border { Width = metrics.SpacingLarge });
        bar.Children.Add(_layout);
        bar.Children.Add(Button("−", texts["print.preview.zoomOut"], () => ZoomBy(1 / 1.25)));
        bar.Children.Add(_zoom);
        bar.Children.Add(Button("+", texts["print.preview.zoomIn"], () => ZoomBy(1.25)));

        var footer = new DialogFooter(density).WithCancel(texts["prompt.close"], () => Completed?.Invoke(this, null));
        if (print is not null)
            footer = footer.WithAction(texts["print.preview.print"], () =>
            {
                Completed?.Invoke(this, null);
                print();
            }, primary: true);
        Footer = footer;

        var surface = new Border
        {
            Child = new ScrollViewer
            {
                Content = _pages,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            },
        }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken);

        var root = new DockPanel { LastChildFill = true };
        var top = new Border
        {
            Child = bar,
            Padding = new Thickness(metrics.SpacingMedium, metrics.SpacingSmall),
            BorderThickness = new Thickness(0, 0, 0, 1),
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border);
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        root.Children.Add(surface);
        Content = root;

        Show(0);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        switch (e.Key)
        {
            case Key.PageDown or Key.Right or Key.Down:
                Move(1);
                e.Handled = true;
                break;
            case Key.PageUp or Key.Left or Key.Up:
                Move(-1);
                e.Handled = true;
                break;
            case Key.Home:
                Show(0);
                e.Handled = true;
                break;
            case Key.End:
                Show(_document.PageCount - 1);
                e.Handled = true;
                break;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0)
        {
            ZoomBy(e.Delta.Y > 0 ? 1.15 : 1 / 1.15);
            e.Handled = true;
        }
        base.OnPointerWheelChanged(e);
    }

    private PreviewLayout CurrentLayout => _layout.SelectedIndex switch
    {
        1 => PreviewLayout.Facing,
        2 => PreviewLayout.Grid,
        3 => PreviewLayout.Book,
        _ => PreviewLayout.Single,
    };

    private void ApplyLayout()
    {
        _pages.Layout = CurrentLayout;
        Show(_pages.First);
    }

    private void ApplyZoom()
    {
        _pages.Zoom = _zoom.SelectedIndex switch
        {
            0 => PreviewZoom.Fit,
            1 => PreviewZoom.FitWidth,
            _ => PreviewZoom.Factor,
        };
        if (_pages.Zoom == PreviewZoom.Factor) _pages.Factor = ZoomSteps[Math.Clamp(_zoom.SelectedIndex - 2, 0, ZoomSteps.Length - 1)];
        _pages.HorizontalAlignment = _pages.Zoom == PreviewZoom.Factor ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        _pages.VerticalAlignment = _pages.Zoom == PreviewZoom.Fit ? VerticalAlignment.Stretch : VerticalAlignment.Top;
    }

    private void ZoomBy(double ratio)
    {
        var current = _pages.Zoom == PreviewZoom.Factor ? _pages.Factor : 1;
        var target = Math.Clamp(current * ratio, ZoomSteps[0], ZoomSteps[^1]);
        var nearest = Array.IndexOf(ZoomSteps, ZoomSteps.OrderBy(step => Math.Abs(step - target)).First());
        if (nearest + 2 == _zoom.SelectedIndex) nearest = Math.Clamp(nearest + (ratio > 1 ? 1 : -1), 0, ZoomSteps.Length - 1);
        _zoom.SelectedIndex = nearest + 2;
    }

    private void Move(int direction) =>
        Show(_spreads.Step(CurrentLayout, _pages.First, _pages.Columns, _pages.Rows, direction, _document.PageCount));

    private void Show(int page)
    {
        if (_document.PageCount == 0) return;
        _pages.First = _spreads.Align(CurrentLayout, Math.Clamp(page, 0, _document.PageCount - 1), _pages.Columns, _pages.Rows);
        var visible = _pages.Visible;
        _label.Text = visible.Count <= 1
            ? _texts.Format("print.preview.page", (visible.Count == 0 ? _pages.First : visible[0]) + 1, _document.PageCount)
            : _texts.Format("print.preview.pages", visible[0] + 1, visible[^1] + 1, _document.PageCount);
    }

    private Button Button(string caption, string tip, Action action)
    {
        var button = new Button { Content = caption, Padding = new Thickness(10, 2) };
        ToolTip.SetTip(button, tip);
        Avalonia.Automation.AutomationProperties.SetName(button, tip);
        button.Click += (_, _) => action();
        return button;
    }
}

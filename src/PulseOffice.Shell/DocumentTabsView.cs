using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Shell;

public sealed class DocumentTabsView : ContentControl, PulseOffice.UI.Controls.Ribbon.IKeyTipRegion
{
    private readonly IDocumentWorkspace _workspace;
    private readonly DesignTokens.Metrics _metrics;
    private readonly StackPanel _tabs;
    private readonly Border _dropIndicator;
    private readonly ScrollViewer _scroller;
    private readonly TabStripDragGeometry _geometry = new();
    private OpenDocument? _dragged;
    private readonly List<(Control Tab, OpenDocument Document)> _shown = [];

    private readonly ITextCatalog _texts;

    public DocumentTabsView(
        IDocumentWorkspace workspace, ITextCatalog texts,
        InterfaceDensity density = InterfaceDensity.Compact)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _metrics = DesignTokens.For(density);

        _tabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 1 };

        this.Themed(BackgroundProperty, Palette.Keys.SurfaceRaised);

        _dropIndicator = new Border
        {
            Width = 2,
            IsVisible = false,
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Left,
        }.Themed(Border.BackgroundProperty, Palette.Keys.TextPrimary);

        var strip = new Panel();
        strip.Children.Add(_tabs);
        strip.Children.Add(_dropIndicator);

        _scroller = new ScrollViewer
        {
            Content = strip,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
        Content = _scroller;

        Height = _metrics.ControlHeight + 4;
        _workspace.ActiveChanged += (_, _) => Refresh();
        _workspace.ActiveUpdated += (_, _) => Refresh();
        _workspace.DocumentClosed += (_, _) => Refresh();
        _workspace.DocumentOpened += (_, _) => Refresh();
        _workspace.DocumentMoved += (_, _) => Refresh();
        _workspace.DocumentDetached += (_, _) => Refresh();
        _texts.LanguageChanged += (_, _) => Refresh();

        Refresh();
    }

    public event EventHandler<OpenDocument>? CloseRequested;

    public event EventHandler<TabTearOffRequest>? TearOffRequested;

    public double ScrollDuringDrag(double viewportX)
    {
        var step = _geometry.ScrollStep(viewportX, _scroller.Viewport.Width);
        if (step == 0) return 0;
        var offset = _scroller.Offset;
        var next = _geometry.ScrolledOffset(offset.X, step, _scroller.Extent.Width, _scroller.Viewport.Width);
        if (next == offset.X) return 0;
        _scroller.Offset = new Vector(next, offset.Y);
        return next - offset.X;
    }

    public bool ReleaseDrag(OpenDocument document, Point inView, PixelPoint onScreen)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!_geometry.IsTornOff(inView, Bounds.Size) || TearOffRequested is null) return false;
        HideDropIndicator();
        TearOffRequested.Invoke(this, new TabTearOffRequest(document, onScreen));
        return true;
    }

    private OpenDocument? _pressed;

    public int DropIndicatorIndex { get; private set; } = -1;

    public int ShowDropIndicator(OpenDocument document, double x)
    {
        ArgumentNullException.ThrowIfNull(document);
        var target = TabIndexAt(x);
        var from = _shown.FindIndex(entry => ReferenceEquals(entry.Document, document));
        if (target < 0 || target == from)
        {
            HideDropIndicator();
            return -1;
        }

        var bounds = _shown[target].Tab.Bounds;
        _dropIndicator.Margin = new Thickness((target > from ? bounds.Right : bounds.Left) - 1, 0, 0, 0);
        _dropIndicator.IsVisible = true;
        DropIndicatorIndex = target;
        return target;
    }

    public void HideDropIndicator()
    {
        _dropIndicator.IsVisible = false;
        DropIndicatorIndex = -1;
    }

    public void Drop(OpenDocument document, double x)
    {
        ArgumentNullException.ThrowIfNull(document);
        HideDropIndicator();
        var target = TabIndexAt(x);
        if (target >= 0) _workspace.Move(document, target);
        _workspace.Activate(document);
    }

    public int TabIndexAt(double x)
    {
        for (var index = 0; index < _shown.Count; index++)
        {
            var bounds = _shown[index].Tab.Bounds;
            if (x >= bounds.Left && x < bounds.Right) return index;
        }

        return -1;
    }

    public void Refresh()
    {
        _tabs.Children.Clear();
        _shown.Clear();

        foreach (var document in _workspace.Documents)
        {
            var tab = BuildTab(document);
            _shown.Add((tab, document));
            _tabs.Children.Add(tab);
        }
    }

    public IReadOnlyList<PulseOffice.UI.Controls.Ribbon.KeyTipTarget> KeyTipTargets()
    {
        if (!IsEffectivelyVisible || _shown.Count < 2) return [];
        return [.. _shown
            .Where(entry => entry.Tab.IsEffectivelyVisible)
            .Select(entry =>
            {
                var document = entry.Document;
                return new PulseOffice.UI.Controls.Ribbon.KeyTipTarget(LabelOf(document), entry.Tab, new Rect(entry.Tab.Bounds.Size),
                    () => _workspace.Activate(document));
            })];
    }

    private string LabelOf(OpenDocument document)
    {
        var shared = _workspace.Documents.Any(other => !ReferenceEquals(other, document)
            && string.Equals(other.Title, document.Title, StringComparison.CurrentCultureIgnoreCase));
        if (!shared || string.IsNullOrEmpty(document.Path)) return document.DisplayName;

        var named = document.Title + System.IO.Path.GetExtension(document.Path);
        return document.IsModified ? named + " •" : named;
    }

    private Control BuildTab(OpenDocument document)
    {
        var isActive = ReferenceEquals(_workspace.Active, document);
        var accent = Color.Parse(document.Module == ModuleKind.Shell
            ? DesignTokens.ShellAccent
            : ModuleIdentity.For(document.Module).AccentHex);

        var label = new TextBlock
        {
            Text = LabelOf(document),
            FontSize = _metrics.FontSize,
            FontWeight = isActive ? FontWeight.SemiBold : FontWeight.Normal,
            VerticalAlignment = VerticalAlignment.Center,
        }.Themed(
            TextBlock.ForegroundProperty,
            isActive ? Palette.Keys.TextPrimary : Palette.Keys.TextSecondary);

        var close = new Button
        {
            Content = "×",
            FontSize = _metrics.FontSize,
            Padding = new Thickness(4, 0),
            Background = Brushes.Transparent,
            BorderThickness = default,
            VerticalAlignment = VerticalAlignment.Center,
        };

        close.Click += (_, _) => CloseRequested?.Invoke(this, document);
        ToolTip.SetTip(close, _texts["document.close"]);
        AutomationProperties.SetName(close, _texts.Format("document.closeNamed", document.Title));

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = _metrics.SpacingSmall,
        };

        if (!string.IsNullOrEmpty(document.Path)) ToolTip.SetTip(label, document.Path);
        content.Children.Add(label);
        content.Children.Add(close);

        var tab = new Border
        {
            Child = content,
            Padding = new Thickness(_metrics.SpacingMedium, 2),

            BorderThickness = new Thickness(0, 2, 0, 0),
            BorderBrush = isActive ? new SolidColorBrush(accent) : Brushes.Transparent,
            Background = isActive
                ? new SolidColorBrush(accent, DesignTokens.SubtleAccentOpacity)
                : Brushes.Transparent,
        };

        tab.PointerPressed += (_, e) =>
        {
            if (e.Source is Button) return;
            if (e.GetCurrentPoint(tab).Properties.IsMiddleButtonPressed)
            {
                CloseRequested?.Invoke(this, document);
                e.Handled = true;
                return;
            }

            if (!e.GetCurrentPoint(tab).Properties.IsLeftButtonPressed) return;
            _dragged = document;
            if (ReferenceEquals(_workspace.Active, document)) return;
            _pressed = document;
        };
        tab.PointerMoved += (_, e) =>
        {
            if (!ReferenceEquals(_dragged, document) || !e.GetCurrentPoint(tab).Properties.IsLeftButtonPressed) return;
            ScrollDuringDrag(e.GetPosition(_scroller).X);
            if (_geometry.IsTornOff(e.GetPosition(this), Bounds.Size)) HideDropIndicator();
            else ShowDropIndicator(document, e.GetPosition(_tabs).X);
        };
        tab.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton != Avalonia.Input.MouseButton.Left || e.Source is Button) return;
            _pressed = null;
            _dragged = null;
            var screen = this.PointToScreen(e.GetPosition(this));
            if (ReleaseDrag(document, e.GetPosition(this), screen)) return;
            Drop(document, e.GetPosition(_tabs).X);
        };
        tab.PointerCaptureLost += (_, _) =>
        {
            if (ReferenceEquals(_pressed, document)) _workspace.Activate(document);
            _pressed = null;
            _dragged = null;
            HideDropIndicator();
        };

        return tab;
    }
}

public sealed record TabTearOffRequest(OpenDocument Document, PixelPoint ScreenPoint);

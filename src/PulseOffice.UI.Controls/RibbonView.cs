using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Ribbon;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls;

public sealed class RibbonView : ContentControl, Text.ITextEditingCompanion, IKeyTipSource
{
    private const int FullRows = 3;
    private const int CompactRows = 2;

    private readonly DesignTokens.Metrics _metrics;
    private readonly RibbonItemFactory _factory;
    private readonly RibbonTabStrip _tabStrip;
    private readonly Border _body;
    private readonly KeyTipAssigner _assigner = new();
    private readonly KeyTipCollector _collector = new();
    private readonly Dictionary<Flyout, Interactive> _popupTops = [];
    private TopLevel? _top;
    private bool _altAlone;
    private bool _swallowText;

    public event EventHandler<RibbonInvocation>? CommandInvoked;

    private readonly IRibbonReductionPlanner _planner;
    private RibbonGroupsPanel? _groups;
    private string? _signature;

    public RibbonView(RibbonServices services, InterfaceDensity density = InterfaceDensity.Compact)
    {
        ArgumentNullException.ThrowIfNull(services);

        _metrics = DesignTokens.For(density);
        _planner = services.Planner;
        _factory = new RibbonItemFactory(new RibbonBuildContext(services, _metrics,
            invocation => CommandInvoked?.Invoke(this, invocation)));

        _tabStrip = new RibbonTabStrip(_metrics)
        {
            Margin = new Thickness(_metrics.SpacingSmall, _metrics.SpacingSmall / 2, 0, 0),
        };

        _tabStrip.TabChosen += (_, tab) => Show(tab);

        _body = new Border
        {
            Padding = new Thickness(_metrics.SpacingMedium, _metrics.SpacingSmall, _metrics.SpacingMedium, 0),
        };

        var layout = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_tabStrip, Dock.Top);
        layout.Children.Add(_tabStrip);
        layout.Children.Add(_body);

        var tipLayer = new Canvas();
        KeyTips = new RibbonKeyTips(tipLayer, this, _assigner, _metrics);
        _factory.Context.PopoverOpened += (_, flyout) => HookPopup(flyout);
        _factory.Context.PopoverClosed += (_, flyout) =>
        {
            UnhookPopup(flyout);
            KeyTips.ItemsClosed();
        };
        Content = new Panel { Children = { layout, tipLayer } };

        BorderThickness = new Thickness(0, 0, 0, 1);

        this.Themed(BorderBrushProperty, Palette.Keys.Border);
        BuildBackground();
    }

    private void BuildBackground()
    {
        var gradient = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        };

        this.GetResourceObservable(Palette.Keys.SurfaceRaised).Subscribe(
            new Avalonia.Reactive.AnonymousObserver<object?>(value => gradient.GradientStops = BuildStops(value)));

        Background = gradient;
    }

    private static GradientStops BuildStops(object? raised)
    {
        var top = (raised as SolidColorBrush)?.Color ?? Colors.White;

        var bottom = Color.FromArgb(top.A,
            (byte)Math.Max(0, top.R - 6),
            (byte)Math.Max(0, top.G - 6),
            (byte)Math.Max(0, top.B - 6));

        return [new GradientStop(top, 0), new GradientStop(bottom, 1)];
    }

    private bool _compact;
    private ModuleKind _module = ModuleKind.Shell;

    private int Rows => _compact ? CompactRows : FullRows;

    public RibbonKeyTips KeyTips { get; }

    public Func<bool> KeyTipsBlocked { get; set; } = () => false;

    public IReadOnlyList<KeyTipTarget> Tabs()
    {
        var targets = new List<KeyTipTarget>();
        var tabs = _tabStrip.Tabs;
        for (var index = 0; index < tabs.Count; index++)
        {
            if (_tabStrip.BoundsOf(index) is not { } bounds) continue;
            var chosen = index;
            targets.Add(new KeyTipTarget(tabs[index].Title, _tabStrip, bounds, () => _tabStrip.Choose(chosen), OpensLevel: true));
        }
        foreach (var region in KeyTipRegions) targets.AddRange(region.KeyTipTargets());
        return targets;
    }

    public List<IKeyTipRegion> KeyTipRegions { get; } = [];

    public KeyTipCollector KeyTipCollector => _collector;

    public KeyTipItems? Items()
    {
        if (_factory.Context.OpenPopover is not { Content: Control content } popover) return null;
        content.UpdateLayout();
        return new KeyTipItems(popover, _collector.Collect(content));
    }

    public void CloseItems() => _factory.Context.OpenPopover?.Hide();

    private void HookPopup(Flyout flyout)
    {
        if (flyout.Content is not Visual content || PopupRoot(content) is not { } root || _popupTops.ContainsKey(flyout)) return;
        var shared = _popupTops.ContainsValue(root);
        _popupTops[flyout] = root;
        if (shared) return;
        root.AddHandler(KeyDownEvent, OnTopKeyDown, RoutingStrategies.Tunnel, true);
        root.AddHandler(KeyUpEvent, OnTopKeyUp, RoutingStrategies.Tunnel, true);
        root.AddHandler(TextInputEvent, OnTopTextInput, RoutingStrategies.Tunnel, true);
    }

    private Interactive? PopupRoot(Visual content) =>
        content.GetSelfAndVisualAncestors().OfType<Interactive>().FirstOrDefault(ancestor => ancestor is Avalonia.Controls.Primitives.OverlayPopupHost or Avalonia.Controls.Primitives.PopupRoot);

    private void UnhookPopup(Flyout flyout)
    {
        if (!_popupTops.Remove(flyout, out var root) || _popupTops.ContainsValue(root)) return;
        root.RemoveHandler(KeyDownEvent, OnTopKeyDown);
        root.RemoveHandler(KeyUpEvent, OnTopKeyUp);
        root.RemoveHandler(TextInputEvent, OnTopTextInput);
    }

    public IReadOnlyList<KeyTipTarget> Commands()
    {
        UpdateLayout();
        return _groups is null ? [] : [.. _groups.Groups.SelectMany(group => group.KeyTipTargets())];
    }

    private bool TipsBlocked() => !IsEffectivelyVisible || !IsEffectivelyEnabled || KeyTipsBlocked();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _top = TopLevel.GetTopLevel(this);
        if (_top is null) return;
        _top.AddHandler(KeyDownEvent, OnTopKeyDown, RoutingStrategies.Tunnel, true);
        _top.AddHandler(KeyUpEvent, OnTopKeyUp, RoutingStrategies.Tunnel, true);
        _top.AddHandler(TextInputEvent, OnTopTextInput, RoutingStrategies.Tunnel, true);
        _top.AddHandler(PointerPressedEvent, OnTopPointerPressed, RoutingStrategies.Tunnel, true);
        if (_top is WindowBase window) window.Deactivated += OnTopDeactivated;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        KeyTips.Close();
        if (_top is null) return;
        _top.RemoveHandler(KeyDownEvent, OnTopKeyDown);
        _top.RemoveHandler(KeyUpEvent, OnTopKeyUp);
        _top.RemoveHandler(TextInputEvent, OnTopTextInput);
        _top.RemoveHandler(PointerPressedEvent, OnTopPointerPressed);
        if (_top is WindowBase window) window.Deactivated -= OnTopDeactivated;
        _top = null;
    }

    private void OnTopKeyDown(object? sender, KeyEventArgs e)
    {
        _swallowText = false;
        if (TipsBlocked())
        {
            _altAlone = false;
            KeyTips.Close();
            return;
        }
        if (e.Key is Key.LeftAlt or Key.RightAlt)
        {
            _altAlone = (e.KeyModifiers & ~KeyModifiers.Alt) == KeyModifiers.None;
            return;
        }
        _altAlone = false;
        if (e.Key == Key.F10 && e.KeyModifiers == KeyModifiers.None)
        {
            KeyTips.Toggle();
            e.Handled = true;
            return;
        }
        if (KeyTips.Level == KeyTipLevel.Hidden
            || e.Key is Key.LeftShift or Key.RightShift or Key.LeftCtrl or Key.RightCtrl or Key.LWin or Key.RWin) return;
        if (e.Key == Key.Escape)
        {
            KeyTips.StepBack();
            e.Handled = true;
            return;
        }
        if (e.KeyModifiers is KeyModifiers.None or KeyModifiers.Shift && KeyTips.Type(Symbol(e)))
        {
            e.Handled = true;
            _swallowText = true;
            return;
        }
        KeyTips.Close();
    }

    private string? Symbol(KeyEventArgs e)
    {
        if (_assigner.Typed(e.KeySymbol) is not null) return e.KeySymbol;
        return e.Key switch
        {
            >= Key.A and <= Key.Z => ((char)('A' + (e.Key - Key.A))).ToString(),
            >= Key.D0 and <= Key.D9 => ((char)('0' + (e.Key - Key.D0))).ToString(),
            >= Key.NumPad0 and <= Key.NumPad9 => ((char)('0' + (e.Key - Key.NumPad0))).ToString(),
            _ => null,
        };
    }

    private void OnTopKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.LeftAlt or Key.RightAlt) || !_altAlone) return;
        _altAlone = false;
        if (TipsBlocked()) return;
        KeyTips.Toggle();
        e.Handled = true;
    }

    private void OnTopTextInput(object? sender, TextInputEventArgs e)
    {
        if (!_swallowText && KeyTips.Level == KeyTipLevel.Hidden) return;
        _swallowText = false;
        e.Handled = true;
    }

    private void OnTopPointerPressed(object? sender, PointerPressedEventArgs e) => KeyTips.Close();

    private void OnTopDeactivated(object? sender, EventArgs e) => KeyTips.Close();

    public bool FocusTabs() => _tabStrip.FocusFromKeyboard();

    public bool Compact
    {
        get => _compact;
        set
        {
            if (_compact == value) return;

            _compact = value;
            _signature = null;

            if (_tabStrip.Current is { } current) Show(current);
        }
    }

    public void Update(RibbonModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var previous = model.Module == _module ? _tabStrip.Current?.Title : null;
        var signature = Signature(model);

        if (signature == _signature && _tabStrip.Current is { } shown && _groups is not null
            && model.Tabs.FirstOrDefault(tab => tab.Title == shown.Title) is { } same)
        {
            _tabStrip.Replace(model.Tabs);

            var views = _groups.Groups.ToList();
            for (var index = 0; index < views.Count && index < same.Groups.Count; index++)
                views[index].Refresh(same.Groups[index]);

            return;
        }

        _signature = signature;
        _module = model.Module;
        KeyTips.Close();

        IsVisible = !model.IsEmpty;

        if (model.IsEmpty)
        {
            _body.Child = null;
            _tabStrip.Show([], null);
            return;
        }

        _tabStrip.Show(model.Tabs, previous);
    }

    private void Show(RibbonTab tab)
    {
        var groups = new RibbonGroupsPanel(_planner);

        for (var i = 0; i < tab.Groups.Count; i++)
        {
            groups.Children.Add(new RibbonGroupView(tab.Groups[i], _factory, Rows));

            if (i < tab.Groups.Count - 1)
                groups.Children.Add(new Border
                {
                    Width = 1,
                    Height = Rows * _factory.ItemHeight,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(_metrics.SpacingMedium, 0),
                }.Themed(BackgroundProperty, Palette.Keys.Border));
        }

        _groups = groups;
        _body.Child = groups;
    }

    private string Signature(RibbonModel model) =>
        $"{model.Module}#{_compact}#" + string.Join("|", model.Tabs.Select(tab =>
            tab.Title + ":" + string.Join(";", tab.Groups.Select(group =>
                group.Title + "=" + string.Join(",", group.Items.Select(item => item.Command.Id.Value))))));

}

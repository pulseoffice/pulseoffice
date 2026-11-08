using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Ribbon;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls;

public sealed class RibbonView : ContentControl, Text.ITextEditingCompanion
{
    private const int FullRows = 3;
    private const int CompactRows = 2;

    private readonly DesignTokens.Metrics _metrics;
    private readonly RibbonItemFactory _factory;
    private readonly RibbonTabStrip _tabStrip;
    private readonly Border _body;

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

        Content = layout;

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

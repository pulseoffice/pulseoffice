using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Panels;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Navigation;

public sealed record NavigatorCategory(string Key, string Title);

public sealed record NavigatorItem(string Id, string Title, int Depth)
{
    public bool Movable { get; init; }
}

public enum NavigatorMove
{
    Up,
    Down,
    Promote,
    Demote,
}

public interface INavigatorSource
{
    IReadOnlyList<NavigatorCategory> Categories { get; }

    IReadOnlyList<NavigatorItem> Items(string category);

    string? Current(string category);

    void Go(string category, string id);

    bool CanMove(string category, string id, NavigatorMove move);

    void Move(string category, string id, NavigatorMove move);

    bool CanDrop(string category, string id, string? beforeId);

    void Drop(string category, string id, string? beforeId);

    event EventHandler? Changed;
}

public sealed class NavigatorPanel : UserControl
{
    private const double DragThreshold = 6;

    private readonly INavigatorSource _source;
    private readonly DesignTokens.Metrics _metrics;
    private readonly PanelChoiceBox _categories;
    private bool _filling;
    private readonly PanelSearchBox _filter;
    private readonly ListBox _list = new();
    private readonly TextBlock _empty = new();
    private readonly PanelActionBar _actions;
    private string _category;
    private NavigatorItem? _dragged;
    private Point _pressedAt;
    private bool _dragging;
    private bool _refillPending;

    public NavigatorPanel(INavigatorSource source, IIconCatalog icons, ITextCatalog texts, InterfaceDensity density = InterfaceDensity.Compact)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        ArgumentNullException.ThrowIfNull(icons);
        ArgumentNullException.ThrowIfNull(texts);
        _metrics = DesignTokens.For(density);
        _category = source.Categories.Count > 0 ? source.Categories[0].Key : string.Empty;

        _categories = new PanelChoiceBox(icons, _metrics) { Tip = texts["navigator.panel.category"] };
        _categories.Chosen += (_, key) =>
        {
            if (_filling) return;
            Choose(key);
        };
        _filter = new PanelSearchBox(icons, _metrics, texts["navigator.panel.filter"]);
        _filter.TextChanged += (_, _) => Refill();
        _filter.Descend += (_, _) => FocusList();
        _filter.Submitted += (_, _) =>
        {
            if (_list.SelectedItem is null && _list.ItemCount > 0) _list.SelectedIndex = 0;
            GoSelected();
        };
        _empty.Text = texts["navigator.panel.empty"];
        _empty.FontSize = _metrics.FontSizeSmall;
        _empty.Margin = new Thickness(_metrics.SpacingMedium);
        _empty.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        _list.SelectionMode = SelectionMode.Single;
        _list.ItemTemplate = new FuncDataTemplate<NavigatorItem>((entry, _) => entry is null ? null : Row(entry));
        _list.ContainerPrepared += (_, args) =>
        {
            if (_list.ItemFromContainer(args.Container) is NavigatorItem entry)
                args.Container.SetValue(PaddingProperty, new Thickness(_metrics.SpacingMedium + entry.Depth * 14, 4, _metrics.SpacingSmall, 4));
        };
        _list.DoubleTapped += (_, _) => GoSelected();
        _list.AddHandler(KeyDownEvent, (_, args) =>
        {
            if (args.Key != Key.Enter) return;
            GoSelected();
            args.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _list.SelectionChanged += (_, _) => UpdateButtons();
        _list.AddHandler(PointerPressedEvent, OnPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _list.AddHandler(PointerMovedEvent, OnMoved, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _list.AddHandler(PointerReleasedEvent, OnReleased, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        _actions = new PanelActionBar(icons, _metrics,
        [
            new PanelAction("go", "panel.go", texts["navigator.panel.go.tip"]),
            new PanelAction("up", "panel.move.up", texts["navigator.panel.up.tip"]) { StartsGroup = true },
            new PanelAction("down", "panel.move.down", texts["navigator.panel.down.tip"]),
            new PanelAction("promote", "panel.move.promote", texts["navigator.panel.promote.tip"]) { StartsGroup = true },
            new PanelAction("demote", "panel.move.demote", texts["navigator.panel.demote.tip"]),
        ]);
        _actions.Invoked += (_, key) =>
        {
            if (key == "go")
            {
                GoSelected();
                return;
            }
            if (Selected() is not { } item) return;
            _source.Move(_category, item.Id, Enum.Parse<NavigatorMove>(key, ignoreCase: true));
        };

        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"),
            RowSpacing = _metrics.SpacingSmall,
            Margin = new Thickness(0, _metrics.SpacingSmall, 0, 0),
        };
        Control[] rows = [_categories, _filter, new PanelListFrame(_metrics, _list, _empty), _actions];
        for (var i = 0; i < rows.Length; i++)
        {
            Grid.SetRow(rows[i], i);
            layout.Children.Add(rows[i]);
        }
        Content = layout;

        BuildCategories();
        Refill();
        AttachedToVisualTree += (_, _) =>
        {
            _source.Changed += OnChanged;
            BuildCategories();
            Refill();
        };
        DetachedFromVisualTree += (_, _) => _source.Changed -= OnChanged;
    }

    public string Category => _category;

    public PanelActionBar Actions => _actions;

    public IReadOnlyList<string> Visible => [.. _list.Items.OfType<NavigatorItem>().Select(item => item.Title)];

    public void Choose(string category)
    {
        if (_source.Categories.All(choice => choice.Key != category)) return;
        _category = category;
        if (!_filling)
        {
            _filling = true;
            _categories.Select(category);
            _filling = false;
        }
        Refill();
    }

    public void Select(string id)
    {
        foreach (var item in _list.Items.OfType<NavigatorItem>())
            if (item.Id == id) _list.SelectedItem = item;
    }

    public void GoSelected()
    {
        if (Selected() is { } item) _source.Go(_category, item.Id);
    }

    private void OnChanged(object? sender, EventArgs args)
    {
        if (_refillPending) return;
        _refillPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _refillPending = false;
            BuildCategories();
            Refill();
        }, DispatcherPriority.Background);
    }

    private void BuildCategories()
    {
        if (_source.Categories.Count > 0 && _source.Categories.All(choice => choice.Key != _category)) _category = _source.Categories[0].Key;
        _filling = true;
        _categories.Show([.. _source.Categories.Select(category => new PanelChoice(category.Key, category.Title, "panel.nav." + category.Key.ToLowerInvariant()))], _category);
        _filling = false;
    }

    private void Refill()
    {
        var filter = _filter.Text.Trim();
        var keep = Selected()?.Id ?? _source.Current(_category);
        List<NavigatorItem> items = [.. _source.Items(_category).Where(entry => filter.Length == 0 || entry.Title.Contains(filter, StringComparison.CurrentCultureIgnoreCase))];
        if (_list.ItemsSource is not IReadOnlyList<NavigatorItem> shown || !shown.SequenceEqual(items)) _list.ItemsSource = items;
        _empty.IsVisible = items.Count == 0;
        if (items.FirstOrDefault(entry => entry.Id == keep) is { } selected) _list.SelectedItem = selected;
        UpdateButtons();
    }

    private Control Row(NavigatorItem entry)
    {
        var text = new TextBlock
        {
            Text = entry.Title,
            FontSize = _metrics.FontSize,
            FontWeight = entry.Depth == 0 && entry.Movable ? FontWeight.SemiBold : FontWeight.Normal,
            TextTrimming = TextTrimming.CharacterEllipsis,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary);
        ToolTip.SetTip(text, entry.Title);
        return text;
    }

    private void FocusList()
    {
        if (_list.SelectedItem is null && _list.ItemCount > 0) _list.SelectedIndex = 0;
        (_list.ContainerFromIndex(Math.Max(0, _list.SelectedIndex)) ?? (Control)_list).Focus(NavigationMethod.Directional);
    }

    private NavigatorItem? Selected() => _list.SelectedItem as NavigatorItem;

    private NavigatorItem? ItemAt(PointerEventArgs args)
    {
        var hit = _list.InputHitTest(args.GetPosition(_list)) as Visual;
        var container = hit?.FindAncestorOfType<ListBoxItem>(true);
        return container is null ? null : _list.ItemFromContainer(container) as NavigatorItem;
    }

    private void OnPressed(object? sender, PointerPressedEventArgs args)
    {
        _dragged = ItemAt(args) is { Movable: true } item ? item : null;
        _pressedAt = args.GetPosition(_list);
        _dragging = false;
    }

    private void OnMoved(object? sender, PointerEventArgs args)
    {
        if (_dragged is null || _dragging) return;
        var delta = args.GetPosition(_list) - _pressedAt;
        if (Math.Abs(delta.Y) < DragThreshold) return;
        _dragging = true;
        Cursor = new Cursor(StandardCursorType.DragMove);
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs args)
    {
        var dragged = _dragged;
        var dragging = _dragging;
        _dragged = null;
        _dragging = false;
        Cursor = Cursor.Default;
        if (dragged is null || !dragging) return;
        var target = ItemAt(args);
        var visible = _list.Items.OfType<NavigatorItem>().ToList();
        var before = target is null ? null : TargetBefore(visible, target, dragged, args);
        if (target is not null && target.Id == dragged.Id) return;
        if (!_source.CanDrop(_category, dragged.Id, before)) return;
        _source.Drop(_category, dragged.Id, before);
        args.Handled = true;
    }

    private string? TargetBefore(List<NavigatorItem> visible, NavigatorItem target, NavigatorItem dragged, PointerEventArgs args)
    {
        if (_list.ContainerFromItem(target) is not { } container) return target.Id;
        var local = args.GetPosition(container);
        if (local.Y < container.Bounds.Height / 2) return target.Id;
        var index = visible.IndexOf(target);
        for (var next = index + 1; next < visible.Count; next++)
            if (visible[next].Movable && visible[next].Depth <= dragged.Depth) return visible[next].Id;
        return null;
    }

    private void UpdateButtons()
    {
        var selected = Selected();
        _actions.SetEnabled("go", selected is not null);
        foreach (var move in Enum.GetValues<NavigatorMove>())
            _actions.SetEnabled(move.ToString().ToLowerInvariant(), selected is not null && _source.CanMove(_category, selected.Id, move));
        var movable = _source.Items(_category).Any(item => item.Movable);
        foreach (var move in Enum.GetValues<NavigatorMove>()) _actions.SetVisible(move.ToString().ToLowerInvariant(), movable);
    }
}

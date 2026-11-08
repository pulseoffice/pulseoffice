using Avalonia;
using Avalonia.Controls;
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

namespace PulseOffice.UI.Controls.StylePanels;

public sealed record StylesPanelServices(IStyleRecents Recents, IIconCatalog Icons, ITextCatalog Texts);

public sealed class StylesPanel : UserControl
{
    private readonly IStylesPanelSource _source;
    private readonly StylesPanelModel _model;
    private readonly ITextCatalog _texts;
    private readonly IIconCatalog _icons;
    private readonly DesignTokens.Metrics _metrics;
    private readonly StylePreviewPainter _painter;
    private readonly PanelChoiceBox _families;
    private readonly Button _filter;
    private readonly PanelSearchBox _search;
    private readonly ListBox _list = new() { SelectionMode = SelectionMode.Single };
    private readonly TextBlock _empty = new() { TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center };
    private readonly PanelActionBar _actions;
    private readonly Border _confirm = new() { IsVisible = false };
    private readonly TextBlock _confirmText = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _failure = new() { TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private bool _filling;
    private IReadOnlyList<StyleRow>? _rendered;

    public StylesPanel(IStylesPanelSource source, StylesPanelServices services, InterfaceDensity density = InterfaceDensity.Compact)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        ArgumentNullException.ThrowIfNull(services);
        _texts = services.Texts;
        _icons = services.Icons;
        _metrics = DesignTokens.For(density);
        _painter = new StylePreviewPainter(_metrics);
        _model = new StylesPanelModel(source, services.Recents);

        _families = new PanelChoiceBox(_icons, _metrics) { Tip = _texts["styles.panel.family"] };
        _families.Chosen += (_, key) => _model.Choose(key);

        _filter = new Button
        {
            Width = _metrics.ControlHeight + 2,
            Height = _metrics.ControlHeight + 2,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = Icon("panel.filter"),
        };
        _filter.Classes.Add("ribbon");
        _filter.Click += (_, _) => OpenFilterMenu();

        _search = new PanelSearchBox(_icons, _metrics, _texts["styles.panel.filter"]);
        _search.TextChanged += (_, _) => _model.SetSearch(_search.Text);
        _search.Descend += (_, _) => FocusList();
        _search.Submitted += (_, _) =>
        {
            if (_model.Selected is null && _model.Rows.Count > 0) _model.Select(_model.Rows[0].Entry.Name);
            _model.Apply();
        };

        _empty.FontSize = _metrics.FontSizeSmall;
        _empty.Margin = new Thickness(_metrics.SpacingMedium, _metrics.SpacingLarge);
        _empty.VerticalAlignment = VerticalAlignment.Top;
        _empty.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        _list.SelectionChanged += (_, _) =>
        {
            if (_filling) return;
            _model.Select(Selected()?.Entry.Name);
        };
        _list.DoubleTapped += (_, args) =>
        {
            if (args.Source is Visual visual && visual.FindAncestorOfType<ListBoxItem>(true) is null) return;
            _model.Apply();
        };
        _list.AddHandler(KeyDownEvent, OnListKey, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _list.AddHandler(PointerPressedEvent, OnPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _list.ContextMenu = new ContextMenu();
        _list.ContextMenu.Opening += (_, _) => FillContextMenu(_list.ContextMenu);

        _actions = new PanelActionBar(_icons, _metrics,
        [
            new PanelAction("new", "panel.style.new", _texts["styles.panel.new.tip"]),
            new PanelAction("update", "panel.style.update", _texts["styles.panel.update.tip"]),
            new PanelAction("edit", "panel.style.edit", _texts["styles.panel.edit.tip"]),
            new PanelAction("delete", "panel.delete", _texts["styles.panel.delete.tip"]) { Trailing = true },
        ]);
        _actions.Invoked += (_, key) => _ = RunAsync(Action(key));

        BuildConfirm();

        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = _metrics.SpacingSmall };
        Grid.SetColumn(_filter, 1);
        top.Children.Add(_families);
        top.Children.Add(_filter);

        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,Auto,Auto"),
            RowSpacing = _metrics.SpacingSmall,
            Margin = new Thickness(0, _metrics.SpacingSmall, 0, 0),
        };
        _failure.FontSize = _metrics.FontSizeSmall;
        _failure.Themed(TextBlock.ForegroundProperty, Palette.Keys.Danger);
        Control[] rows = [top, _search, new PanelListFrame(_metrics, _list, _empty), _confirm, _failure, _actions];
        for (var i = 0; i < rows.Length; i++)
        {
            Grid.SetRow(rows[i], i);
            layout.Children.Add(rows[i]);
        }
        Content = layout;

        _model.Changed += (_, _) => Render();
        Render();
        _source.Changed += OnChanged;
        AttachedToVisualTree += (_, _) =>
        {
            _source.Changed -= OnChanged;
            _source.Changed += OnChanged;
            _model.Refresh();
        };
        DetachedFromVisualTree += (_, _) => _source.Changed -= OnChanged;
    }

    public StylesPanelModel Model => _model;

    public PanelActionBar Actions => _actions;

    public string Family => _model.Family;

    public IReadOnlyList<string> Visible => [.. _model.Rows.Select(row => row.Entry.Name)];

    public bool ConfirmVisible => _confirm.IsVisible;

    public string? Failure => _failure.IsVisible ? _failure.Text : null;

    public void Choose(string family) => _model.Choose(family);

    public void Press(Key key) => _list.RaiseEvent(new KeyEventArgs { RoutedEvent = KeyDownEvent, Key = key, Source = _list });

    public IReadOnlyList<(string Header, bool Enabled)> ContextItems()
    {
        var menu = new ContextMenu();
        FillContextMenu(menu);
        return [.. menu.Items.OfType<MenuItem>().Select(item => (item.Header?.ToString() ?? string.Empty, item.IsEnabled))];
    }

    private void OnChanged(object? sender, EventArgs args) => Dispatcher.UIThread.Post(_model.Refresh);

    private void Render()
    {
        _filling = true;
        _families.Show([.. _model.Families.Select(family => new PanelChoice(family.Key, family.Title, family.Icon))], _model.Family);
        _families.IsVisible = _model.Families.Count > 0;
        if (!ReferenceEquals(_rendered, _model.Rows))
        {
            _rendered = _model.Rows;
            _list.ItemsSource = _model.Rows.Select(Item).ToList();
        }
        var items = _list.Items.OfType<ListBoxItem>().ToList();
        var wanted = items.FirstOrDefault(item => ((StyleRow)item.Tag!).Entry.Name == _model.Selected);
        if (!ReferenceEquals(_list.SelectedItem, wanted))
        {
            _list.SelectedItem = wanted;
            if (wanted is not null) Dispatcher.UIThread.Post(() => wanted.BringIntoView());
        }
        _filling = false;

        _empty.IsVisible = items.Count == 0;
        _empty.Text = _texts[_model.Search.Length > 0 ? "styles.panel.empty.search" : _model.Filter == StyleListFilter.All ? "styles.panel.empty" : "styles.panel.empty.filter"];

        var filtered = _model.Filter != StyleListFilter.All;
        _filter.Classes.Set("checked", filtered);
        var filterTip = _texts.Format("styles.panel.filter.tip", _texts[FilterKey(_model.Filter)]);
        ToolTip.SetTip(_filter, filterTip);
        Avalonia.Automation.AutomationProperties.SetName(_filter, filterTip);

        _actions.SetEnabled("new", _model.Can(StylePanelAction.New));
        _actions.SetEnabled("update", _model.Can(StylePanelAction.Update));
        _actions.SetEnabled("edit", _model.Can(StylePanelAction.Edit));
        _actions.SetEnabled("delete", _model.Can(StylePanelAction.Delete));

        var confirming = _model.PendingDelete is not null;
        if (confirming && !_confirm.IsVisible && _list.SelectedItem is ListBoxItem shown) Dispatcher.UIThread.Post(() => shown.BringIntoView(), DispatcherPriority.Background);
        _confirm.IsVisible = confirming;
        if (_model.PendingDelete is { } pending)
            _confirmText.Text = _texts.Format("styles.panel.delete.confirm", _model.Rows.FirstOrDefault(row => row.Entry.Name == pending)?.Entry.Title ?? pending);
    }

    private ListBoxItem Item(StyleRow row)
    {
        var item = new ListBoxItem
        {
            Tag = row,
            Padding = new Thickness(0),
            Content = _painter.Row(row),
        };
        ToolTip.SetTip(item, row.Entry.Title);
        return item;
    }

    private StyleRow? Selected() => (_list.SelectedItem as ListBoxItem)?.Tag as StyleRow;

    public void FocusList()
    {
        if (_list.SelectedItem is null && _list.ItemCount > 0) _list.SelectedIndex = 0;
        (_list.ContainerFromIndex(Math.Max(0, _list.SelectedIndex)) ?? (Control)_list).Focus(NavigationMethod.Directional);
    }

    private void OnPressed(object? sender, PointerPressedEventArgs args)
    {
        if (!args.GetCurrentPoint(_list).Properties.IsRightButtonPressed) return;
        if (args.Source is Visual visual && visual.FindAncestorOfType<ListBoxItem>(true) is { Tag: StyleRow row })
            _model.Select(row.Entry.Name);
    }

    private void OnListKey(object? sender, KeyEventArgs args)
    {
        switch (args.Key)
        {
            case Key.Enter:
                args.Handled = true;
                if (_model.PendingDelete is not null) _ = GuardAsync(() => _model.ConfirmDeleteAsync(CancellationToken.None));
                else _model.Apply();
                break;
            case Key.Delete or Key.Back:
                args.Handled = true;
                _model.RequestDelete();
                break;
            case Key.Escape when _model.PendingDelete is not null:
                args.Handled = true;
                _model.CancelDelete();
                break;
            case Key.F2:
                args.Handled = true;
                _ = RunAsync(StylePanelAction.Edit);
                break;
        }
    }

    private async Task RunAsync(StylePanelAction action)
    {
        await GuardAsync(() => _model.RunAsync(action, CancellationToken.None)).ConfigureAwait(true);
        if (action == StylePanelAction.Delete) FocusList();
    }

    private async Task GuardAsync(Func<Task> work)
    {
        _failure.IsVisible = false;
        try
        {
            await work().ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not OperationCanceledException)
        {
            _failure.Text = _texts.Format("styles.panel.failed", ex.Message);
            _failure.IsVisible = true;
        }
    }

    private StylePanelAction Action(string key) => key switch
    {
        "new" => StylePanelAction.New,
        "update" => StylePanelAction.Update,
        "edit" => StylePanelAction.Edit,
        "derive" => StylePanelAction.Derive,
        _ => StylePanelAction.Delete,
    };

    private void FillContextMenu(ContextMenu menu)
    {
        menu.Items.Clear();
        var apply = Menu("styles.panel.apply", null, _model.CanApply, () => _model.Apply());
        apply.FontWeight = FontWeight.SemiBold;
        menu.Items.Add(apply);
        menu.Items.Add(new Separator());
        menu.Items.Add(Menu("styles.panel.edit", "panel.style.edit", _model.Can(StylePanelAction.Edit), () => _ = RunAsync(StylePanelAction.Edit)));
        menu.Items.Add(Menu("styles.panel.derive", "panel.style.derive", _model.Can(StylePanelAction.Derive), () => _ = RunAsync(StylePanelAction.Derive)));
        menu.Items.Add(Menu("styles.panel.update", "panel.style.update", _model.Can(StylePanelAction.Update), () => _ = RunAsync(StylePanelAction.Update)));
        menu.Items.Add(Menu("styles.panel.rename", null, _model.Can(StylePanelAction.Rename), () => _ = RunAsync(StylePanelAction.Rename)));
        menu.Items.Add(new Separator());
        menu.Items.Add(Menu("styles.panel.delete", "panel.delete", _model.Can(StylePanelAction.Delete), () => _model.RequestDelete()));
    }

    private MenuItem Menu(string key, string? icon, bool enabled, Action run)
    {
        var item = new MenuItem { Header = _texts[key], IsEnabled = enabled };
        if (icon is not null) item.Icon = Icon(icon);
        item.Click += (_, _) => run();
        return item;
    }

    private void OpenFilterMenu()
    {
        var menu = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedRight };
        foreach (var filter in _model.Filters)
        {
            var chosen = filter;
            var item = new MenuItem
            {
                Header = _texts[FilterKey(filter)],
                ToggleType = MenuItemToggleType.Radio,
                IsChecked = filter == _model.Filter,
            };
            item.Click += (_, _) => _model.SetFilter(chosen);
            menu.Items.Add(item);
        }
        menu.ShowAt(_filter);
    }

    private string FilterKey(StyleListFilter filter) => $"styles.panel.show.{filter.ToString().ToLowerInvariant()}";

    private void BuildConfirm()
    {
        _confirmText.FontSize = _metrics.FontSizeSmall;
        _confirmText.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary);
        var delete = new Button { Content = _texts["styles.panel.delete"] };
        delete.Classes.Add("dialogPrimary");
        delete.Click += async (_, _) =>
        {
            await GuardAsync(() => _model.ConfirmDeleteAsync(CancellationToken.None)).ConfigureAwait(true);
            FocusList();
        };
        var cancel = new Button { Content = _texts["styles.panel.cancel"] };
        cancel.Classes.Add("dialogQuiet");
        cancel.Click += (_, _) =>
        {
            _model.CancelDelete();
            FocusList();
        };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = _metrics.SpacingSmall, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(cancel);
        buttons.Children.Add(delete);
        var stack = new StackPanel { Spacing = _metrics.SpacingSmall };
        stack.Children.Add(_confirmText);
        stack.Children.Add(buttons);
        _confirm.Child = stack;
        _confirm.Padding = new Thickness(_metrics.SpacingMedium, _metrics.SpacingSmall + 2);
        _confirm.CornerRadius = new CornerRadius(_metrics.RadiusTight + 2);
        _confirm.BorderThickness = new Thickness(1);
        _confirm.Themed(Border.BorderBrushProperty, Palette.Keys.Danger).Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken);
    }

    private Control Icon(string key) =>
        _icons.Find(key) is { } geometry
            ? new IconView(geometry, _metrics.IconSize) { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            : new Border { Width = _metrics.IconSize, Height = _metrics.IconSize };
}

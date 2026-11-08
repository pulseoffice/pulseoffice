using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Panels;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Sorting;

public sealed class SortLevelsView : UserControl, IDialogContent, IDialogFooterOwner, IDialogSizeHint
{
    private readonly SortLevelsRequest _request;
    private readonly ITextCatalog _texts;
    private readonly IIconCatalog _icons;
    private readonly DesignTokens.Metrics _metrics;
    private readonly StackPanel _rows = new() { Spacing = 2 };
    private readonly List<LevelRow> _shown = [];
    private readonly CheckBox _header;
    private readonly CheckBox _case;
    private readonly CheckBox _direction;
    private Button? _confirm;
    private readonly TextBlock _warning;

    public SortLevelsView(SortLevelsRequest request, ITextCatalog texts, IIconCatalog icons, DesignTokens.Metrics metrics)
    {
        _request = request ?? throw new ArgumentNullException(nameof(request));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _icons = icons ?? throw new ArgumentNullException(nameof(icons));
        _metrics = metrics;
        _direction = new CheckBox { Content = texts["sort.levels.leftToRight"], IsChecked = request.OffersDirection && request.LeftToRight, IsVisible = request.OffersDirection };
        Model = new SortLevelsModel(Math.Max(1, (LeftToRight ? request.RowHeaders : request.Headers).Count), request.Initial);

        _header = new CheckBox { Content = texts["sort.levels.hasHeader"], IsChecked = request.HasHeader, IsVisible = request.OffersHeaderChoice };
        _case = new CheckBox { Content = texts["sort.levels.caseSensitive"], IsChecked = request.CaseSensitive, IsVisible = request.OffersCase };
        _warning = new TextBlock { FontSize = metrics.FontSizeSmall, TextWrapping = TextWrapping.Wrap, IsVisible = false }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.Warning);

        Actions = new PanelActionBar(icons, metrics,
        [
            new PanelAction("add", "sort.level.add", texts["sort.levels.add"]),
            new PanelAction("copy", "sort.level.copy", texts["sort.levels.copy"]),
            new PanelAction("delete", "sort.level.delete", texts["sort.levels.delete"]),
            new PanelAction("up", "sort.level.up", texts["sort.levels.up"]) { StartsGroup = true },
            new PanelAction("down", "sort.level.down", texts["sort.levels.down"]),
        ]);
        Actions.Invoked += (_, key) => Run(key);

        var list = new ScrollViewer
        {
            MaxHeight = 6 * (metrics.ControlHeight + 2 * metrics.SpacingSmall + 2),
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = _rows,
            Padding = new Thickness(2),
        };
        AutomationProperties.SetName(list, texts["sort.levels.list"]);

        var layout = new TwoPaneDialogLayout(metrics)
        {
            Header = new HelpTip(texts[request.ExplanationKey ?? "sort.levels.explanation"], texts, metrics),
        };
        var levels = layout.AddSection(texts["sort.levels.section"]);
        levels.Add(null, new StackPanel { Spacing = metrics.SpacingSmall, Children = { new PanelListFrame(metrics, list), Actions, _warning } }, DialogRowKind.Wide);
        var options = layout.AddSection(texts["form.section.options"]);
        options.Add(null, _header, DialogRowKind.Indented);
        options.Add(null, _case, DialogRowKind.Indented);
        options.Add(null, _direction, DialogRowKind.Indented);
        options.IsVisible = request.OffersHeaderChoice || request.OffersCase || request.OffersDirection;
        Layout = layout;
        Content = layout;

        _header.IsCheckedChanged += (_, _) => Rebuild();
        _direction.IsCheckedChanged += (_, _) => Model.Reset(Math.Max(1, ColumnNames.Count));
        Model.Changed += (_, _) => Rebuild();
        Model.SelectionChanged += (_, _) => Highlight();
        Model.Edited += (_, _) => Refresh();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);

        Footer = new DialogFooter()
            .WithCancel(texts["dialog.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(texts["sort.levels.confirm"], Confirm, out var confirm, primary: true);
        _confirm = confirm;

        Rebuild();
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public double? PreferredWidth => Math.Max(Layout.SuggestedWidth, (_request.CustomOrders.Count > 0 ? 63 : 52) * _metrics.FontSize);

    public TwoPaneDialogLayout Layout { get; }

    public SortLevelsModel Model { get; }

    public PanelActionBar Actions { get; }

    public CheckBox HeaderBox => _header;

    public CheckBox CaseBox => _case;

    public CheckBox DirectionBox => _direction;

    public IReadOnlyList<ComboBox> ColumnBoxes => [.. _shown.Select(row => row.Column)];

    public IReadOnlyList<IconChoiceGroup> OrderGroups => [.. _shown.Select(row => row.Order)];

    public string Warning => _warning.IsVisible ? _warning.Text ?? string.Empty : string.Empty;

    public SortLevelsChoice Choice => new(Model.Levels, _header.IsChecked == true, _case.IsChecked == true) { LeftToRight = LeftToRight };

    private bool LeftToRight => _direction.IsChecked == true;

    private IReadOnlyList<string> ColumnNames =>
        _header.IsChecked == true || !_request.OffersHeaderChoice
            ? LeftToRight ? _request.RowHeaders : _request.Headers
            : LeftToRight ? _request.RowLetters : _request.Letters;

    private void Confirm()
    {
        if (Model.HasDuplicates) return;
        Completed?.Invoke(this, Choice);
    }

    private void Run(string key)
    {
        switch (key)
        {
            case "add": Model.Add(); break;
            case "copy": Model.CopySelected(); break;
            case "delete": Model.RemoveSelected(); break;
            case "up": Model.MoveUp(); break;
            case "down": Model.MoveDown(); break;
        }

        if (_shown.Count > Model.Selected) _shown[Model.Selected].Column.Focus();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.Alt) return;
        var key = e.Key switch
        {
            Key.Up => "up",
            Key.Down => "down",
            Key.Insert or Key.OemPlus or Key.Add => "add",
            Key.Delete or Key.OemMinus or Key.Subtract => "delete",
            _ => null,
        };
        if (key is null) return;
        Actions.Press(key);
        e.Handled = true;
    }

    private void Rebuild()
    {
        _rows.Children.Clear();
        _shown.Clear();
        var names = ColumnNames;
        for (var index = 0; index < Model.Levels.Count; index++)
        {
            var row = new LevelRow(this, index, names);
            _shown.Add(row);
            _rows.Children.Add(row.Frame);
        }

        Refresh();
        Highlight();
    }

    private void Refresh()
    {
        Actions.SetEnabled("add", Model.CanAdd);
        Actions.SetEnabled("copy", Model.CanAdd);
        Actions.SetEnabled("delete", Model.CanRemove);
        Actions.SetEnabled("up", Model.CanMoveUp);
        Actions.SetEnabled("down", Model.CanMoveDown);
        _warning.Text = _texts["sort.levels.duplicate"];
        _warning.IsVisible = Model.HasDuplicates;
        if (_confirm is not null) _confirm.IsEnabled = !Model.HasDuplicates;
    }

    private void Highlight()
    {
        for (var index = 0; index < _shown.Count; index++)
        {
            var frame = _shown[index].Frame;
            frame.Classes.Set("selected", index == Model.Selected);
            if (index == Model.Selected) frame.Themed(Border.BackgroundProperty, "PulseAccentSubtle");
            else frame.Background = Brushes.Transparent;
        }

        Refresh();
    }

    private sealed class LevelRow
    {
        public LevelRow(SortLevelsView view, int index, IReadOnlyList<string> names)
        {
            var metrics = view._metrics;
            var texts = view._texts;
            var level = view.Model.Levels[index];

            var label = new TextBlock
            {
                Text = texts[index == 0 ? "sort.levels.first" : "sort.levels.then"],
                FontSize = metrics.FontSize,
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 6 * metrics.FontSize,
            }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

            Column = new ComboBox
            {
                ItemsSource = names,
                SelectedIndex = Math.Clamp(level.Column, 0, Math.Max(0, names.Count - 1)),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinWidth = 13 * metrics.FontSize,
            };
            AutomationProperties.SetName(Column, $"{label.Text} – {texts["sort.levels.column"]}");

            Order = new IconChoiceGroup(
                [
                    new IconChoice("ascending", "sort.ascending", texts["sort.levels.ascending"]),
                    new IconChoice("descending", "sort.descending", texts["sort.levels.descending"]),
                ],
                view._icons, metrics, texts["sort.levels.order"], level.Descending ? 1 : 0, showLabels: true);

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = metrics.SpacingMedium };
            grid.Children.Add(label);
            Grid.SetColumn(Column, 1);
            grid.Children.Add(Column);
            Grid.SetColumn(Order, 2);
            grid.Children.Add(Order);

            if (view._request.CustomOrders.Count > 0)
            {
                Custom = new ComboBox
                {
                    ItemsSource = (IReadOnlyList<string>)[texts["sort.levels.noCustomOrder"], .. view._request.CustomOrders],
                    SelectedIndex = level.CustomOrder + 1,
                    MaxWidth = 15 * metrics.FontSize,
                };
                AutomationProperties.SetName(Custom, texts["sort.levels.customOrder"]);
                ToolTip.SetTip(Custom, texts["sort.levels.customOrder"]);
                Grid.SetColumn(Custom, 3);
                grid.Children.Add(Custom);
                Custom.SelectionChanged += (_, _) => Store(view, index);
            }

            Frame = new Border
            {
                Padding = new Thickness(metrics.SpacingSmall),
                CornerRadius = new CornerRadius(metrics.RadiusTight),
                Child = grid,
                Background = Brushes.Transparent,
            };

            Frame.AddHandler(InputElement.PointerPressedEvent, (_, _) => view.Model.Select(index), RoutingStrategies.Tunnel);
            Frame.AddHandler(InputElement.GotFocusEvent, (_, _) => view.Model.Select(index), RoutingStrategies.Bubble);
            Column.SelectionChanged += (_, _) => Store(view, index);
            Order.SelectionChanged += (_, _) => Store(view, index);
        }

        public Border Frame { get; }

        public ComboBox Column { get; }

        public IconChoiceGroup Order { get; }

        public ComboBox? Custom { get; }

        private void Store(SortLevelsView view, int index) =>
            view.Model.Set(index, new SortLevel(
                Math.Max(Column.SelectedIndex, 0),
                Order.SelectedIndex == 1,
                Custom is null ? -1 : Math.Max(Custom.SelectedIndex, 0) - 1));
    }
}

public interface ISortLevelsDialog
{
    Task<SortLevelsChoice?> AskAsync(SortLevelsRequest request, CancellationToken ct);
}

public sealed class SortLevelsDialog(IDialogService dialogs, ITextCatalog texts, IIconCatalog icons) : ISortLevelsDialog
{
    public Task<SortLevelsChoice?> AskAsync(SortLevelsRequest request, CancellationToken ct) =>
        dialogs.ShowAsync<SortLevelsChoice>(
            new SortLevelsView(request, texts, icons, DesignTokens.For(InterfaceDensity.Compact)),
            new DialogOptions { TitleKey = request.TitleKey }, ct);
}

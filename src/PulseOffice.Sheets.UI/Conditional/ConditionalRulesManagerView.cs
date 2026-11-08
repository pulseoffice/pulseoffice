using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Panels;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.Conditional;

public interface IConditionalRuleEditing
{
    Task<ConditionalRule?> EditAsync(ConditionalRule rule, CancellationToken ct);
}

public sealed class ConditionalRulesManagerView : UserControl, IDialogContent, IDialogFooterOwner
{
    public const string ActionNew = "new";
    public const string ActionEdit = "edit";
    public const string ActionDuplicate = "duplicate";
    public const string ActionDelete = "delete";
    public const string ActionUp = "up";
    public const string ActionDown = "down";

    private readonly List<ConditionalRule> _rules;
    private readonly CellRange _selection;
    private readonly IConditionalRuleEditing _editing;
    private readonly ConditionalEditorServices _services;
    private readonly ITextCatalog _texts;
    private readonly DesignTokens.Metrics _metrics = DesignTokens.For(InterfaceDensity.Compact);
    private readonly ListBox _list = new();
    private readonly PanelActionBar _actions;
    private readonly IconChoiceGroup _scope;
    private readonly TextBlock _empty = new();
    private readonly DialogFooter _footer;
    private List<int> _visible = [];
    private int _dragFrom = -1;

    public ConditionalRulesManagerView(
        IReadOnlyList<ConditionalRule> rules, CellRange selection, IConditionalRuleEditing editing, ConditionalEditorServices services, ITextCatalog texts)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = [.. rules.OrderBy(rule => rule.Priority)];
        _selection = selection;
        _editing = editing ?? throw new ArgumentNullException(nameof(editing));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));

        _scope = new IconChoiceGroup(
        [
            new IconChoice("selection", string.Empty, texts.Format("conditional.scope.selection", selection.ToString())),
            new IconChoice("sheet", string.Empty, texts["conditional.scope.sheet"]),
        ], null, _metrics, texts["conditional.scope"], _rules.Any(Covers) ? 0 : 1, showLabels: true);
        _scope.SelectionChanged += (_, _) => Refresh(-1);

        _actions = new PanelActionBar(services.Icons, _metrics,
        [
            new PanelAction(ActionNew, "panel.style.new", texts["conditional.action.new"]),
            new PanelAction(ActionEdit, "panel.style.edit", texts["conditional.action.edit"]),
            new PanelAction(ActionDuplicate, "ui.duplicate", texts["conditional.action.duplicate"]),
            new PanelAction(ActionDelete, "ui.delete", texts["conditional.action.delete"]),
            new PanelAction(ActionUp, "ui.chevronUp", texts["conditional.action.up"]) { StartsGroup = true },
            new PanelAction(ActionDown, "ui.chevronDown", texts["conditional.action.down"]),
        ]);
        _actions.Invoked += (_, key) => Launch(key);

        _list.Classes.Add("panelList");
        _list.MinHeight = 240;
        _list.MaxHeight = 360;
        AutomationProperties.SetName(_list, texts["conditional.rules"]);
        _list.SelectionChanged += (_, _) => UpdateActions();
        _list.DoubleTapped += (_, _) => Launch(ActionEdit);
        _list.KeyDown += (_, e) =>
        {
            var action = e.Key switch
            {
                Key.Delete => ActionDelete,
                Key.Enter => ActionEdit,
                Key.Up when e.KeyModifiers.HasFlag(KeyModifiers.Alt) => ActionUp,
                Key.Down when e.KeyModifiers.HasFlag(KeyModifiers.Alt) => ActionDown,
                _ => null,
            };
            if (action is null) return;
            e.Handled = true;
            Launch(action);
        };

        _list.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (e.GetCurrentPoint(_list).Properties.IsLeftButtonPressed) _dragFrom = RowAt(e.Source);
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _list.AddHandler(PointerReleasedEvent, (_, e) =>
        {
            var from = _dragFrom;
            _dragFrom = -1;
            if (from < 0) return;
            var point = e.GetPosition(_list);
            var target = _list.GetVisualDescendants().OfType<ListBoxItem>()
                .FirstOrDefault(item => item.Bounds.Height > 0 && item.TranslatePoint(new Point(0, 0), _list) is { } origin
                    && point.Y >= origin.Y && point.Y < origin.Y + item.Bounds.Height);
            if (target is not null && _list.IndexFromContainer(target) is var to and >= 0 && to != from) MoveVisible(from, to);
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);

        _empty.Text = texts["conditional.empty"];
        _empty.FontSize = _metrics.FontSize;
        _empty.TextWrapping = TextWrapping.Wrap;
        _empty.Margin = new Thickness(_metrics.SpacingMedium);
        _empty.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        var frame = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(_metrics.RadiusSoft),
            Child = new Grid { Children = { _list, _empty } },
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border)
         .Themed(Border.BackgroundProperty, Palette.Keys.Surface);

        var hint = new TextBlock { Text = texts["conditional.order.hint"], FontSize = _metrics.FontSizeSmall, TextWrapping = TextWrapping.Wrap }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        _footer = new DialogFooter()
            .WithCancel(texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(texts["prompt.confirm"], () => Completed?.Invoke(this, Result()), primary: true);

        Content = new StackPanel
        {
            Spacing = _metrics.SpacingMedium,
            MinWidth = 600,
            Children = { _scope, _actions, frame, hint },
        };

        Refresh(_visible.Count > 0 ? 0 : -1);
        if (_list.ItemCount > 0) _list.SelectedIndex = 0;
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer => _footer;

    public IReadOnlyList<ConditionalRule> Rules => _rules;

    public ListBox List => _list;

    public PanelActionBar Actions => _actions;

    public IconChoiceGroup Scope => _scope;

    public IReadOnlyList<ConditionalRule> Result() =>
        [.. _rules.Select((rule, index) => rule.Priority == index ? rule : rule with { Priority = index })];

    public Task? Pending { get; private set; }

    private void Launch(string key)
    {
        if (Pending is { IsCompleted: false }) return;
        var running = Act(key);
        Pending = running;
        running.ContinueWith(
            failed => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(failed.Exception!.GetBaseException())),
            CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    public async Task Act(string key)
    {
        var selected = _list.SelectedIndex;
        var position = selected >= 0 && selected < _visible.Count ? _visible[selected] : -1;
        switch (key)
        {
            case ActionNew:
                var created = await _editing.EditAsync(_services.Catalog.Default(ConditionalRuleKind.CellValue, _selection), CancellationToken.None).ConfigureAwait(true);
                if (created is null) return;
                _rules.Insert(0, created);
                _scope.SelectedIndex = Covers(created) || _scope.SelectedIndex == 1 ? _scope.SelectedIndex : 1;
                Refresh(0);
                break;
            case ActionEdit when position >= 0:
                var edited = await _editing.EditAsync(_rules[position], CancellationToken.None).ConfigureAwait(true);
                if (edited is null) return;
                _rules[position] = edited;
                Refresh(position);
                break;
            case ActionDuplicate when position >= 0:
                _rules.Insert(position, _rules[position]);
                Refresh(position);
                break;
            case ActionDelete when position >= 0:
                _rules.RemoveAt(position);
                Refresh(Math.Min(position, _rules.Count - 1));
                break;
            case ActionUp when selected > 0:
                Swap(position, _visible[selected - 1]);
                break;
            case ActionDown when selected >= 0 && selected < _visible.Count - 1:
                Swap(position, _visible[selected + 1]);
                break;
        }
    }

    public void MoveVisible(int from, int to)
    {
        if (from < 0 || to < 0 || from >= _visible.Count || to >= _visible.Count || from == to) return;
        var rule = _rules[_visible[from]];
        var destination = _visible[to];
        _rules.RemoveAt(_visible[from]);
        _rules.Insert(destination, rule);
        Refresh(destination);
    }

    private int RowAt(object? source)
    {
        var item = (source as Avalonia.Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true);
        return item is null ? -1 : _list.IndexFromContainer(item);
    }

    private void Swap(int first, int second)
    {
        (_rules[first], _rules[second]) = (_rules[second], _rules[first]);
        Refresh(second);
    }

    private bool Covers(ConditionalRule rule) => rule.Ranges.Any(range => range.IntersectsWith(_selection));

    private void Refresh(int focusRule)
    {
        var sheetScope = _scope.SelectedIndex == 1;
        _visible = [.. Enumerable.Range(0, _rules.Count).Where(index => sheetScope || Covers(_rules[index]))];
        _list.ItemsSource = _visible.Select(index => Row(_rules[index], index)).ToList();
        _empty.IsVisible = _visible.Count == 0;
        var target = _visible.IndexOf(focusRule);
        _list.SelectedIndex = target >= 0 ? target : (_visible.Count > 0 ? 0 : -1);
        UpdateActions();
    }

    private void UpdateActions()
    {
        var selected = _list.SelectedIndex;
        var has = selected >= 0;
        _actions.SetEnabled(ActionEdit, has);
        _actions.SetEnabled(ActionDuplicate, has);
        _actions.SetEnabled(ActionDelete, has);
        _actions.SetEnabled(ActionUp, has && selected > 0);
        _actions.SetEnabled(ActionDown, has && selected < _visible.Count - 1);
    }

    private Control Row(ConditionalRule rule, int index)
    {
        var preview = new ConditionalRulePreview(_services.Painting, _texts["conditional.sample"]) { Rule = rule, Width = 64, Height = 30 };
        var description = _services.Catalog.Describe(rule, _texts);
        var title = new TextBlock { Text = description, FontSize = _metrics.FontSize, TextTrimming = TextTrimming.CharacterEllipsis };
        var range = new TextBlock { Text = _texts.Format("conditional.appliesToValue", rule.RangeText), FontSize = _metrics.FontSizeSmall }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        var order = new TextBlock { Text = (index + 1).ToString(System.Globalization.CultureInfo.CurrentCulture), FontSize = _metrics.FontSizeSmall, Width = 18, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*"), ColumnSpacing = _metrics.SpacingMedium, Margin = new Thickness(0, 2) };
        grid.Children.Add(order);
        Grid.SetColumn(preview, 1);
        grid.Children.Add(preview);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { title, range } };
        Grid.SetColumn(text, 2);
        grid.Children.Add(text);
        AutomationProperties.SetName(grid, description + ", " + rule.RangeText);
        return grid;
    }
}

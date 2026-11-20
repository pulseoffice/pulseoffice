using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using PulseOffice.Documents.Fields;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Documents.UI.Shell;

public sealed class FolioMergeRecipientsStep : UserControl
{
    private const int SummaryColumns = 3;

    private const int MaximumConditions = 6;

    private const int SortKeys = 2;

    private readonly MergeData _data;
    private readonly IMergeRecipients _recipients;
    private readonly ITextCatalog _texts;
    private readonly DesignTokens.Metrics _metrics;
    private readonly HashSet<int> _excluded = [];
    private readonly List<ConditionEditor> _conditions = [];
    private readonly StackPanel _conditionRows;
    private readonly Button _addCondition;
    private readonly List<(ComboBox Column, ComboBox Direction)> _sort = [];
    private readonly ListBox _list = new() { Height = 200 };
    private readonly TextBlock _count = new();
    private readonly IReadOnlyList<MergeFilterOperator> _operators =
        [MergeFilterOperator.Contains, MergeFilterOperator.Equals, MergeFilterOperator.NotEquals, MergeFilterOperator.Less, MergeFilterOperator.Greater, MergeFilterOperator.Empty, MergeFilterOperator.NotEmpty];
    private bool _applying;

    public event EventHandler? Changed;

    public FolioMergeRecipientsStep(MergeData data, IMergeRecipients recipients, ITextCatalog texts, DesignTokens.Metrics metrics, MergeSelection initial)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(recipients);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(initial);
        _data = data;
        _recipients = recipients;
        _texts = texts;
        _metrics = metrics;
        AutomationProperties.SetName(_list, texts["folio.mailMerge.recipients"]);
        _list.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<FolioMergeRecipient>((entry, _) => entry is null ? new Border() : Recipient(entry), supportsRecycling: false);
        _conditionRows = new StackPanel { Spacing = _metrics.SpacingSmall };
        _addCondition = new Button { Content = texts["folio.mailMerge.filter.add"] };
        _addCondition.Click += (_, _) =>
        {
            AddCondition(null);
            Refilter();
        };

        var filter = new DialogSection(texts["folio.mailMerge.filter"], metrics);
        var filterPanel = new StackPanel { Spacing = _metrics.SpacingSmall, Children = { _conditionRows, _addCondition } };

        var sortPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = _metrics.SpacingSmall };
        for (var index = 0; index < SortKeys; index++)
        {
            var column = new ComboBox { ItemsSource = new[] { texts["folio.mailMerge.sort.none"] }.Concat(data.Columns).ToList(), SelectedIndex = 0, MinWidth = 160 };
            var direction = new ComboBox { ItemsSource = new[] { texts["folio.mailMerge.sort.ascending"], texts["folio.mailMerge.sort.descending"] }, SelectedIndex = 0, MinWidth = 120 };
            AutomationProperties.SetName(column, texts.Format("folio.mailMerge.sort.key", index + 1));
            AutomationProperties.SetName(direction, texts["folio.mailMerge.sort.direction"]);
            column.SelectionChanged += (_, _) => Refilter();
            direction.SelectionChanged += (_, _) => Refilter();
            _sort.Add((column, direction));
            sortPanel.Children.Add(column);
            sortPanel.Children.Add(direction);
        }
        var sort = new DialogSection(texts["folio.mailMerge.sort"], metrics);

        var all = new Button { Content = texts["folio.mailMerge.selectAll"] };
        all.Click += (_, _) => Include(true);
        var none = new Button { Content = texts["folio.mailMerge.selectNone"] };
        none.Click += (_, _) => Include(false);
        _count.VerticalAlignment = VerticalAlignment.Center;
        AutomationProperties.SetLiveSetting(_count, AutomationLiveSetting.Polite);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = _metrics.SpacingSmall, Children = { all, none, _count } };
        var frame = new Border { CornerRadius = new CornerRadius(_metrics.RadiusTight), BorderThickness = new Thickness(1), Child = _list }
            .Themed(Border.BorderBrushProperty, Palette.Keys.Border);

        Content = new StackPanel
        {
            Spacing = _metrics.SpacingMedium,
            Children =
            {
                new TextBlock { Text = texts["folio.mailMerge.recipients.explanation"], TextWrapping = Avalonia.Media.TextWrapping.Wrap, FontSize = _metrics.FontSize }
                    .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
                filter,
                filterPanel,
                sort,
                sortPanel,
                frame,
                actions,
            },
        };
        Apply(initial);
    }

    public IReadOnlyList<int> Chosen => _recipients.Rows(_data, Selection);

    public IReadOnlyList<int> Listed => [.. (_list.ItemsSource as IEnumerable<FolioMergeRecipient> ?? Enumerable.Empty<FolioMergeRecipient>()).Select(entry => entry.Row)];

    public int ConditionCount => _conditions.Count;

    public MergeSelection Selection => new(Filter, Sort, new HashSet<int>(_excluded));

    public void Exclude(int row, bool excluded)
    {
        if (excluded) _excluded.Add(row);
        else _excluded.Remove(row);
        Refilter();
    }

    public void Apply(MergeSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        _applying = true;
        _conditions.Clear();
        _conditionRows.Children.Clear();
        foreach (var condition in selection.Filter.Conditions.Where(condition => _data.Columns.Contains(condition.Column, StringComparer.OrdinalIgnoreCase)).Take(MaximumConditions))
            AddCondition(condition);
        if (_conditions.Count == 0) AddCondition(null);
        for (var index = 0; index < _sort.Count; index++)
        {
            var key = index < selection.Sort.Count ? selection.Sort[index] : null;
            _sort[index].Column.SelectedIndex = key is null ? 0 : ColumnIndex(key.Column) + 1;
            _sort[index].Direction.SelectedIndex = key?.Descending == true ? 1 : 0;
        }
        _excluded.Clear();
        foreach (var row in selection.Excluded.Where(row => row >= 0 && row < _data.Rows.Count)) _excluded.Add(row);
        _applying = false;
        Refilter();
    }

    private int ColumnIndex(string column)
    {
        for (var index = 0; index < _data.Columns.Count; index++)
            if (string.Equals(_data.Columns[index], column, StringComparison.OrdinalIgnoreCase)) return index;
        return -1;
    }

    private MergeFilter Filter => new([.. _conditions.Select(editor => editor.Condition).OfType<MergeCondition>()]);

    private IReadOnlyList<MergeSort> Sort =>
        [.. _sort.Where(key => key.Column.SelectedIndex > 0 && key.Column.SelectedIndex - 1 < _data.Columns.Count)
            .Select(key => new MergeSort(_data.Columns[key.Column.SelectedIndex - 1], key.Direction.SelectedIndex == 1))];

    private void AddCondition(MergeCondition? condition)
    {
        if (_conditions.Count >= MaximumConditions) return;
        var editor = new ConditionEditor(this, _conditions.Count > 0);
        _conditions.Add(editor);
        _conditionRows.Children.Add(editor.Row);
        if (condition is not null) editor.Show(condition);
        _addCondition.IsEnabled = _conditions.Count < MaximumConditions;
    }

    private void RemoveCondition(ConditionEditor editor)
    {
        if (_conditions.Count <= 1)
        {
            editor.Show(null);
            Refilter();
            return;
        }
        var index = _conditions.IndexOf(editor);
        _conditions.RemoveAt(index);
        _conditionRows.Children.Remove(editor.Row);
        if (index == 0 && _conditions.Count > 0) _conditions[0].First();
        _addCondition.IsEnabled = true;
        Refilter();
    }

    private void Refilter()
    {
        if (_applying) return;
        foreach (var editor in _conditions) editor.Enable();
        var filter = Filter;
        _list.ItemsSource = _recipients.Ordered(_data, Enumerable.Range(0, _data.Rows.Count).Where(row => _recipients.Matches(_data, row, filter)), Sort)
            .Select(row => new FolioMergeRecipient(row, Summary(row))).ToList();
        Count();
    }

    private void Count()
    {
        _count.Text = _texts.Format("folio.mailMerge.recipients.count", Chosen.Count, _data.Rows.Count);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private string Summary(int row) =>
        (row + 1).ToString(CultureInfo.CurrentCulture) + ". " + string.Join(" · ", _data.Columns.Take(SummaryColumns).Select(column => _data.Value(row, column) ?? string.Empty));

    private Control Recipient(FolioMergeRecipient entry)
    {
        var check = new CheckBox { Content = entry.Summary, IsChecked = !_excluded.Contains(entry.Row) };
        check.IsCheckedChanged += (_, _) =>
        {
            if (check.IsChecked == true) _excluded.Remove(entry.Row);
            else _excluded.Add(entry.Row);
            Count();
        };
        return check;
    }

    private void Include(bool included)
    {
        foreach (var row in Listed)
        {
            if (included) _excluded.Remove(row);
            else _excluded.Add(row);
        }
        Refilter();
    }

    private sealed class ConditionEditor
    {
        private readonly FolioMergeRecipientsStep _owner;
        private readonly ComboBox _join;
        private readonly ComboBox _column;
        private readonly ComboBox _operator;
        private readonly TextBox _value = new() { MinWidth = 140 };

        public ConditionEditor(FolioMergeRecipientsStep owner, bool joined)
        {
            _owner = owner;
            var texts = owner._texts;
            _join = new ComboBox { ItemsSource = new[] { texts["folio.mailMerge.filter.and"], texts["folio.mailMerge.filter.or"] }, SelectedIndex = 0, MinWidth = 70, IsVisible = joined };
            _column = new ComboBox { ItemsSource = new[] { texts["folio.mailMerge.filter.none"] }.Concat(owner._data.Columns).ToList(), SelectedIndex = 0, MinWidth = 150 };
            _operator = new ComboBox
            {
                ItemsSource = owner._operators.Select(item => texts["folio.mailMerge.filter." + char.ToLowerInvariant(item.ToString()[0]) + item.ToString()[1..]]).ToList(),
                SelectedIndex = 0,
                MinWidth = 130,
            };
            AutomationProperties.SetName(_join, texts["folio.mailMerge.filter.join"]);
            AutomationProperties.SetName(_column, texts["folio.mailMerge.filter.column"]);
            AutomationProperties.SetName(_operator, texts["folio.mailMerge.filter.operator"]);
            AutomationProperties.SetName(_value, texts["folio.mailMerge.filter.value"]);
            var remove = new Button { Content = texts["folio.mailMerge.filter.remove"] };
            AutomationProperties.SetName(remove, texts["folio.mailMerge.filter.remove"]);
            ToolTip.SetTip(remove, texts["folio.mailMerge.filter.remove"]);
            remove.Click += (_, _) => _owner.RemoveCondition(this);
            _join.SelectionChanged += (_, _) => _owner.Refilter();
            _column.SelectionChanged += (_, _) => _owner.Refilter();
            _operator.SelectionChanged += (_, _) => _owner.Refilter();
            _value.TextChanged += (_, _) => _owner.Refilter();
            Row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = owner._metrics.SpacingSmall, Children = { _join, _column, _operator, _value, remove } };
        }

        public Control Row { get; }

        public MergeCondition? Condition =>
            _column.SelectedIndex is > 0 and var column && column - 1 < _owner._data.Columns.Count
                ? new MergeCondition(_owner._data.Columns[column - 1], _owner._operators[Math.Clamp(_operator.SelectedIndex, 0, _owner._operators.Count - 1)], _value.Text ?? string.Empty)
                {
                    Join = _join.IsVisible && _join.SelectedIndex == 1 ? MergeJoin.Or : MergeJoin.And,
                }
                : null;

        public void Show(MergeCondition? condition)
        {
            _column.SelectedIndex = condition is null ? 0 : Math.Max(0, _owner.ColumnIndex(condition.Column) + 1);
            _operator.SelectedIndex = condition is null ? 0 : Math.Max(0, _owner._operators.ToList().IndexOf(condition.Operator));
            _value.Text = condition?.Value ?? string.Empty;
            _join.SelectedIndex = condition?.Join == MergeJoin.Or ? 1 : 0;
        }

        public void First() => _join.IsVisible = false;

        public void Enable()
        {
            var condition = Condition;
            _operator.IsEnabled = condition is not null;
            _value.IsEnabled = condition is not null && condition.Operator is not (MergeFilterOperator.Empty or MergeFilterOperator.NotEmpty);
        }
    }
}

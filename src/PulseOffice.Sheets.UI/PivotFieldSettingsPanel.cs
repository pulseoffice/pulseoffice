using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.UI.DataDialogs;
using PulseOffice.UI.Controls.Panels;

namespace PulseOffice.Sheets.UI;

public sealed class PivotFieldSettingsPanel : UserControl
{
    private static readonly ValueDisplayMode[] DisplayModes = Enum.GetValues<ValueDisplayMode>();
    private static readonly DateGrouping[] Groupings = Enum.GetValues<DateGrouping>();
    private static readonly PivotLayout[] Layouts = Enum.GetValues<PivotLayout>();
    private static readonly HashSet<ValueDisplayMode> BasedModes =
    [
        ValueDisplayMode.DifferenceFrom, ValueDisplayMode.PercentDifferenceFrom, ValueDisplayMode.RunningTotal,
        ValueDisplayMode.PercentRunningTotal, ValueDisplayMode.PercentOfParent,
    ];

    private readonly PivotLayoutModel _model;
    private readonly LedgerDialogKit _kit;
    private readonly IPivotPreviewer _previewer;
    private readonly ComboBox _field = new() { MinWidth = 220 };
    private readonly StackPanel _editor = new();
    private readonly List<int> _choices = [];
    private string _signature = string.Empty;
    private bool _filling;

    public PivotFieldSettingsPanel(PivotLayoutModel model, LedgerDialogKit kit, IPivotPreviewer previewer)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _kit = kit ?? throw new ArgumentNullException(nameof(kit));
        _previewer = previewer ?? throw new ArgumentNullException(nameof(previewer));
        _editor.Spacing = kit.Metrics.SpacingSmall;
        AutomationProperties.SetName(_field, kit["pivot.settings.field"]);
        _field.SelectionChanged += (_, _) =>
        {
            if (_filling) return;
            _signature = string.Empty;
            Refresh();
        };

        var options = TableOptions();
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = kit.Metrics.SpacingLarge };
        var fieldColumn = new StackPanel
        {
            Spacing = kit.Metrics.SpacingSmall,
            Children = { kit.Caption("pivot.settings.title"), Row("pivot.settings.field", _field), _editor },
        };
        grid.Children.Add(fieldColumn);
        Grid.SetColumn(options, 1);
        grid.Children.Add(options);
        Content = grid;

        _model.Changed += (_, _) => Refresh();
        Refresh();
    }

    public int? SelectedColumn => _field.SelectedIndex is var index and >= 0 && index < _choices.Count ? _choices[index] : null;

    public ComboBox FieldChoice => _field;

    public void Select(int column)
    {
        var index = _choices.IndexOf(column);
        if (index >= 0) _field.SelectedIndex = index;
    }

    private void Refresh()
    {
        var selected = SelectedColumn;
        var used = _model.Areas.SelectMany(area => _model.In(area)).ToList();
        _filling = true;
        _choices.Clear();
        _choices.AddRange(used);
        _field.ItemsSource = used.Select(column => _kit.Texts.Format("pivot.settings.fieldInArea",
            _model.NameOf(column), _kit["pivot.zone." + Camel(_model.AreaOf(column).ToString())])).ToList();
        _field.SelectedIndex = selected is { } kept && used.Contains(kept) ? used.IndexOf(kept) : used.Count > 0 ? 0 : -1;
        _filling = false;

        var signature = Signature();
        if (signature == _signature) return;
        _signature = signature;
        BuildEditor();
    }

    private string Signature()
    {
        var values = string.Join(",", _model.In(PivotArea.Value));
        var rowsAndColumns = string.Join(",", _model.In(PivotArea.Row).Concat(_model.In(PivotArea.Column)));
        if (SelectedColumn is not { } column) return "none|" + values;
        var field = _model.SettingsOf(column);
        return string.Join("|", column, _model.AreaOf(column), values, rowsAndColumns, field.DateGrouping,
            field.NumericGroupInterval?.ToString(CultureInfo.InvariantCulture), field.TopFilter?.Count);
    }

    private void BuildEditor()
    {
        _editor.Children.Clear();
        if (SelectedColumn is not { } column)
        {
            _editor.Children.Add(_kit.Note(_kit["pivot.settings.none"]));
            return;
        }

        var field = _model.SettingsOf(column);
        if (_model.AreaOf(column) == PivotArea.Value)
        {
            BuildValueEditor(column, field);
            return;
        }

        BuildGrouping(column, field);
        if (_model.AreaOf(column) is PivotArea.Row or PivotArea.Column) BuildSorting(column, field);
        BuildItems(column, field);
    }

    private void BuildValueEditor(int column, PivotField field)
    {
        var mode = Choice("pivot.settings.showAs", DisplayModes.Select(item => _kit["pivot.showAs." + Camel(item.ToString())]), Array.IndexOf(DisplayModes, field.DisplayMode));
        var bases = _model.In(PivotArea.Row).Concat(_model.In(PivotArea.Column)).Select(index => _model.Columns[index]).ToList();
        var baseField = Choice("pivot.settings.baseField", bases, Math.Max(0, bases.IndexOf(field.BaseField ?? string.Empty)));
        baseField.Box.IsEnabled = BasedModes.Contains(field.DisplayMode) && bases.Count > 0;
        mode.Box.SelectionChanged += (_, _) =>
        {
            if (mode.Box.SelectedIndex < 0) return;
            var chosen = DisplayModes[mode.Box.SelectedIndex];
            baseField.Box.IsEnabled = BasedModes.Contains(chosen) && bases.Count > 0;
            _model.Configure(column, current => current with
            {
                DisplayMode = chosen,
                BaseField = BasedModes.Contains(chosen) && baseField.Box.SelectedIndex >= 0 && bases.Count > 0 ? bases[baseField.Box.SelectedIndex] : null,
            });
        };
        baseField.Box.SelectionChanged += (_, _) =>
        {
            if (baseField.Box.SelectedIndex < 0 || bases.Count == 0) return;
            _model.Configure(column, current => current with { BaseField = bases[baseField.Box.SelectedIndex] });
        };

        if (field.IsCalculated) BuildCalculation(column, field);

        var format = Text("pivot.settings.numberFormat", field.NumberFormat, text =>
            _model.Configure(column, current => current with { NumberFormat = string.IsNullOrWhiteSpace(text) ? null : text.Trim() }));
        _editor.Children.Add(mode.Row);
        _editor.Children.Add(baseField.Row);
        _editor.Children.Add(format);
    }

    private void BuildCalculation(int column, PivotField field)
    {
        _editor.Children.Add(Text("pivot.calculated.name", field.SourceName, text =>
        {
            var name = text.Trim();
            if (name.Length == 0 || _model.Columns.Contains(name, StringComparer.CurrentCultureIgnoreCase)) return;
            _model.Configure(column, current => current with { SourceName = name });
        }));
        _editor.Children.Add(Text("pivot.calculated.formula", field.Formula, text =>
        {
            var formula = text.Trim();
            if (formula.Length == 0) return;
            _model.Configure(column, current => current with { Formula = formula.StartsWith('=') ? formula : "=" + formula });
        }));
    }

    private void BuildGrouping(int column, PivotField field)
    {
        var grouping = Choice("pivot.settings.group", Groupings.Select(item => _kit["pivot.group." + Camel(item.ToString())]), Array.IndexOf(Groupings, field.DateGrouping));
        grouping.Box.SelectionChanged += (_, _) =>
        {
            if (grouping.Box.SelectedIndex < 0) return;
            var chosen = Groupings[grouping.Box.SelectedIndex];
            _model.Configure(column, current => current with
            {
                DateGrouping = chosen,
                NumericGroupInterval = chosen == DateGrouping.None ? current.NumericGroupInterval : null,
                ExcludedValues = [],
            });
        };
        var interval = Text("pivot.settings.interval", field.NumericGroupInterval?.ToString(CultureInfo.CurrentCulture), text =>
        {
            double? size = double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsed) && parsed > 0 ? parsed : null;
            _model.Configure(column, current => current with
            {
                NumericGroupInterval = size,
                DateGrouping = size is null ? current.DateGrouping : DateGrouping.None,
                ExcludedValues = size == current.NumericGroupInterval ? current.ExcludedValues : [],
            });
        });
        _editor.Children.Add(grouping.Row);
        _editor.Children.Add(interval);
    }

    private void BuildSorting(int column, PivotField field)
    {
        var values = _model.In(PivotArea.Value).Select(_model.NameOf).ToList();
        var sorts = new List<string> { _kit["pivot.sort.labelAscending"], _kit["pivot.sort.labelDescending"] };
        foreach (var value in values)
        {
            sorts.Add(_kit.Texts.Format("pivot.sort.valueAscending", value));
            sorts.Add(_kit.Texts.Format("pivot.sort.valueDescending", value));
        }

        var current = field.SortByValue is { } byValue && byValue >= 0 && byValue < values.Count
            ? 2 + byValue * 2 + (field.SortDescending ? 1 : 0)
            : field.SortDescending ? 1 : 0;
        var sort = Choice("pivot.settings.sort", sorts, current);
        sort.Box.SelectionChanged += (_, _) =>
        {
            var index = sort.Box.SelectedIndex;
            if (index < 0) return;
            _model.Configure(column, settings => settings with
            {
                SortByValue = index < 2 ? null : (index - 2) / 2,
                SortDescending = index % 2 == 1,
            });
        };
        _editor.Children.Add(sort.Row);

        var subtotals = _kit.Check("pivot.settings.subtotals", field.ShowSubtotals);
        subtotals.IsCheckedChanged += (_, _) => _model.Configure(column, settings => settings with { ShowSubtotals = subtotals.IsChecked == true });
        _editor.Children.Add(subtotals);

        if (values.Count == 0) return;
        var top = field.TopFilter;
        var count = Text("pivot.settings.topCount", top is { Count: > 0 } ? top.Count.ToString(CultureInfo.CurrentCulture) : string.Empty, text =>
            _model.Configure(column, settings => settings with
            {
                TopFilter = int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var n) && n > 0
                    ? new PivotTopFilter(n, settings.TopFilter?.ValueField ?? 0, settings.TopFilter?.FromBottom ?? false)
                    : null,
            }));
        var direction = Choice("pivot.settings.topDirection", [_kit["pivot.top.top"], _kit["pivot.top.bottom"]], top?.FromBottom == true ? 1 : 0);
        var by = Choice("pivot.settings.topBy", values, Math.Clamp(top?.ValueField ?? 0, 0, values.Count - 1));
        direction.Box.IsEnabled = by.Box.IsEnabled = top is { Count: > 0 };
        void Retop() => _model.Configure(column, settings => settings.TopFilter is { } filter
            ? settings with { TopFilter = filter with { FromBottom = direction.Box.SelectedIndex == 1, ValueField = Math.Max(0, by.Box.SelectedIndex) } }
            : settings);
        direction.Box.SelectionChanged += (_, _) => Retop();
        by.Box.SelectionChanged += (_, _) => Retop();
        _editor.Children.Add(count);
        _editor.Children.Add(direction.Row);
        _editor.Children.Add(by.Row);
    }

    private void BuildItems(int column, PivotField field)
    {
        var items = _previewer.Items(field);
        if (items.Count == 0) return;
        var list = new StackPanel { Spacing = 0 };
        foreach (var item in items.Take(MaximumItems))
        {
            var key = item;
            var box = new CheckBox { Content = key.Length == 0 ? _kit["pivot.blank"] : _previewer.ItemLabel(field, key), IsChecked = !field.ExcludedValues.Contains(key) };
            box.IsCheckedChanged += (_, _) => _model.Configure(column, settings => settings with
            {
                ExcludedValues = box.IsChecked == true
                    ? [.. settings.ExcludedValues.Where(excluded => excluded != key)]
                    : [.. settings.ExcludedValues.Append(key).Distinct(StringComparer.Ordinal)],
            });
            list.Children.Add(box);
        }

        _editor.Children.Add(_kit.Caption("pivot.settings.items"));
        _editor.Children.Add(new PanelListFrame(_kit.Metrics, new ScrollViewer { MaxHeight = 140, Content = list, Padding = new Thickness(6, 4) }));
    }

    private Control TableOptions()
    {
        var options = _model.Options;
        var layout = Choice("pivot.settings.layout", Layouts.Select(item => _kit["pivot.layout." + Camel(item.ToString())]), Array.IndexOf(Layouts, options.Layout));
        layout.Box.SelectionChanged += (_, _) =>
        {
            if (layout.Box.SelectedIndex >= 0) _model.Options = _model.Options with { Layout = Layouts[layout.Box.SelectedIndex] };
        };
        var rows = _kit.Check("pivot.settings.grandRows", options.GrandTotalRows);
        rows.IsCheckedChanged += (_, _) => _model.Options = _model.Options with { GrandTotalRows = rows.IsChecked == true };
        var columns = _kit.Check("pivot.settings.grandColumns", options.GrandTotalColumns);
        columns.IsCheckedChanged += (_, _) => _model.Options = _model.Options with { GrandTotalColumns = columns.IsChecked == true };
        var repeat = _kit.Check("pivot.settings.repeatLabels", options.RepeatLabels);
        repeat.IsCheckedChanged += (_, _) => _model.Options = _model.Options with { RepeatLabels = repeat.IsChecked == true };
        return new StackPanel
        {
            Spacing = _kit.Metrics.SpacingSmall,
            MinWidth = 220,
            Children = { _kit.Caption("pivot.settings.table"), layout.Row, rows, columns, repeat },
        };
    }

    private (Control Row, ComboBox Box) Choice(string labelKey, IEnumerable<string> items, int selected)
    {
        var list = items.ToList();
        var box = new ComboBox { ItemsSource = list, SelectedIndex = list.Count == 0 ? -1 : Math.Clamp(selected, 0, list.Count - 1), MinWidth = 180 };
        AutomationProperties.SetName(box, _kit[labelKey]);
        return (Row(labelKey, box), box);
    }

    private Control Text(string labelKey, string? initial, Action<string> commit)
    {
        var box = new TextBox { Text = initial ?? string.Empty, MinWidth = 180 };
        AutomationProperties.SetName(box, _kit[labelKey]);
        var committed = box.Text;
        void Commit()
        {
            if (box.Text == committed) return;
            committed = box.Text;
            commit(box.Text ?? string.Empty);
        }

        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, args) =>
        {
            if (args.Key != Key.Enter) return;
            Commit();
            args.Handled = true;
        };
        return Row(labelKey, box);
    }

    private Control Row(string labelKey, Control editor)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("140,*"), ColumnSpacing = _kit.Metrics.SpacingSmall };
        grid.Children.Add(new TextBlock { Text = _kit[labelKey], VerticalAlignment = VerticalAlignment.Center, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        Grid.SetColumn(editor, 1);
        grid.Children.Add(editor);
        return grid;
    }

    private const int MaximumItems = 200;

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}

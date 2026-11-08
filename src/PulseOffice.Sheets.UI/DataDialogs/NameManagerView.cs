using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Panels;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.DataDialogs;

public sealed record NameEntry(string Name, string Expression, string? Scope, string? Original = null, string? OriginalScope = null);

public interface INameValuePreview
{
    string Value(string expression);
}

public sealed class NameManagerModel
{
    private readonly List<NameEntry> _entries;
    private readonly Func<string, bool> _validName;

    public NameManagerModel(IEnumerable<NameEntry> entries, Func<string, bool> validName)
    {
        _entries = [.. entries];
        _validName = validName ?? throw new ArgumentNullException(nameof(validName));
    }

    public event EventHandler? Changed;

    public IReadOnlyList<NameEntry> Entries => _entries;

    public IReadOnlyList<(NameEntry Entry, string Name, string? Scope)> Removed => _removed;

    private readonly List<(NameEntry Entry, string Name, string? Scope)> _removed = [];

    public IReadOnlyList<NameEntry> Matching(string search) =>
        [.. _entries.Where(entry => search.Length == 0
            || entry.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)
            || entry.Expression.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)];

    public string? Problem(NameEntry entry, string name, string? scope)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var trimmed = name.Trim();
        if (!_validName(trimmed)) return "names.problem.invalid";
        return _entries.Any(other => !ReferenceEquals(other, entry) && string.Equals(other.Name, trimmed, StringComparison.OrdinalIgnoreCase) && other.Scope == scope)
            ? "names.problem.taken"
            : null;
    }

    public NameEntry Add(string name, string expression, string? scope)
    {
        var entry = new NameEntry(name, expression, scope);
        _entries.Add(entry);
        Changed?.Invoke(this, EventArgs.Empty);
        return entry;
    }

    public NameEntry Replace(NameEntry entry, NameEntry updated)
    {
        var index = _entries.IndexOf(entry);
        if (index < 0) return entry;
        _entries[index] = updated;
        Changed?.Invoke(this, EventArgs.Empty);
        return updated;
    }

    public void Remove(NameEntry entry)
    {
        if (!_entries.Remove(entry)) return;
        if (entry.Original is { } original) _removed.Add((entry, original, entry.OriginalScope));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public string FreeName(string stem)
    {
        var index = 1;
        while (_entries.Any(entry => string.Equals(entry.Name, $"{stem}{index}", StringComparison.OrdinalIgnoreCase))) index++;
        return $"{stem}{index}";
    }
}

public sealed class NameManagerView : UserControl, IDialogContent, IDialogFooterOwner, IDialogSizeHint
{
    private readonly LedgerDialogKit _kit;
    private readonly INameValuePreview _values;
    private readonly IReadOnlyList<string> _sheets;
    private readonly string _newExpression;
    private readonly StackPanel _rows = new();
    private readonly TextBlock _problem;
    private readonly TextBlock _empty;
    private NameEntry? _selected;
    private bool _loading;

    public NameManagerView(LedgerDialogKit kit, NameManagerModel model, INameValuePreview values, IReadOnlyList<string> sheets, string newExpression, IReferenceSource expressions)
    {
        _kit = kit ?? throw new ArgumentNullException(nameof(kit));
        Model = model ?? throw new ArgumentNullException(nameof(model));
        _values = values ?? throw new ArgumentNullException(nameof(values));
        _sheets = sheets;
        _newExpression = newExpression;
        var metrics = kit.Metrics;

        Search = new PanelSearchBox(kit.Icons, metrics, kit["names.search"]);
        Search.TextChanged += (_, _) => Rebuild();

        Actions = new PanelActionBar(kit.Icons, metrics,
        [
            new PanelAction("new", "sort.level.add", kit["names.new"]),
            new PanelAction("delete", "sort.level.delete", kit["names.remove"]),
        ]);
        Actions.Invoked += (_, key) => Run(key);

        NameBox = new TextBox { MinWidth = 200 };
        Scope = new ComboBox { ItemsSource = (List<string>)[kit["names.scope.workbook"], .. sheets], SelectedIndex = 0, MinWidth = 200 };
        Reference = new RangeFieldControl(new RangeField("reference", kit["names.expression"], string.Empty, expressions), kit.Texts, metrics);
        _problem = kit.Note();
        _problem.Themed(TextBlock.ForegroundProperty, Palette.Keys.Danger);
        _empty = kit.Note(kit["names.noMatch"]);

        var header = Row(kit["names.column.name"], kit["names.column.value"], kit["names.column.reference"], kit["names.column.scope"], true);
        var table = new StackPanel
        {
            Children =
            {
                header,
                new ScrollViewer { MaxHeight = 220, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Content = new StackPanel { Children = { _rows, _empty } } },
            },
        };

        var layout = new TwoPaneDialogLayout(metrics) { Header = kit.Help("names.manage.explanation") };
        layout.AddSection((string?)null).Add(null, new StackPanel { Spacing = metrics.SpacingSmall, Children = { Search, new PanelListFrame(metrics, table), Actions } }, DialogRowKind.Wide);
        var edit = layout.AddSection(kit["names.section.edit"]);
        edit.Add(kit["names.name"], NameBox);
        edit.Add(kit["names.scope"], Scope);
        edit.Add(kit["names.expression"], Reference);
        edit.Add(null, _problem, DialogRowKind.Indented);
        Layout = layout;
        Content = layout;

        NameBox.TextChanged += (_, _) => Store();
        Scope.SelectionChanged += (_, _) => Store();
        Reference.TextChanged += (_, _) => Store();

        Footer = new DialogFooter()
            .WithCancel(kit["dialog.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(kit["names.confirm"], () => Completed?.Invoke(this, Model), out var confirm, primary: true);
        Confirm = confirm;

        Select(Model.Matching(string.Empty).FirstOrDefault());
        Rebuild();
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public double? PreferredWidth => 720;

    public TwoPaneDialogLayout Layout { get; }

    public NameManagerModel Model { get; }

    public PanelSearchBox Search { get; }

    public PanelActionBar Actions { get; }

    public TextBox NameBox { get; }

    public ComboBox Scope { get; }

    public RangeFieldControl Reference { get; }

    public Button Confirm { get; }

    public NameEntry? Selected => _selected;

    public IReadOnlyList<string> ShownNames => [.. _rows.Children.OfType<Border>().Select(row => row.Tag as string ?? string.Empty)];

    public string Problem => _problem.IsVisible ? _problem.Text ?? string.Empty : string.Empty;

    private void Run(string key)
    {
        if (key == "new")
        {
            Search.Text = string.Empty;
            Select(Model.Add(Model.FreeName(_kit["names.newStem"]), _newExpression, null));
            Rebuild();
            NameBox.Focus();
            NameBox.SelectAll();
        }
        else if (key == "delete" && _selected is { } entry)
        {
            Model.Remove(entry);
            Select(Model.Matching(Search.Text).FirstOrDefault());
            Rebuild();
        }
    }

    private void Select(NameEntry? entry)
    {
        _selected = entry;
        _loading = true;
        NameBox.Text = entry?.Name ?? string.Empty;
        Scope.SelectedIndex = entry?.Scope is { } scope ? Math.Max(0, _sheets.ToList().IndexOf(scope) + 1) : 0;
        Reference.Text = entry?.Expression ?? string.Empty;
        _loading = false;
        NameBox.IsEnabled = Scope.IsEnabled = Reference.IsEnabled = entry is not null;
        Actions.SetEnabled("delete", entry is not null);
        Check();
    }

    private void Store()
    {
        if (_loading || _selected is not { } entry) return;
        var scope = Scope.SelectedIndex > 0 ? _sheets[Scope.SelectedIndex - 1] : null;
        if (Check() is not null) return;
        _selected = Model.Replace(entry, entry with { Name = (NameBox.Text ?? string.Empty).Trim(), Scope = scope, Expression = Reference.Text });
        Rebuild();
    }

    private string? Check()
    {
        var problem = _selected is { } entry
            ? Model.Problem(entry, NameBox.Text ?? string.Empty, Scope.SelectedIndex > 0 ? _sheets[Scope.SelectedIndex - 1] : null)
                ?? (Reference.Text.Length == 0 ? "names.problem.empty" : null)
            : null;
        _problem.Text = problem is null ? string.Empty : _kit.Texts.Format(problem, (NameBox.Text ?? string.Empty).Trim());
        _problem.IsVisible = problem is not null;
        Confirm.IsEnabled = problem is null;
        return problem;
    }

    private void Rebuild()
    {
        _rows.Children.Clear();
        var matching = Model.Matching(Search.Text);
        foreach (var entry in matching)
        {
            var row = Row(entry.Name, _values.Value(entry.Expression), entry.Expression, entry.Scope ?? _kit["names.scope.workbook"], false);
            row.Tag = entry.Name;
            if (ReferenceEquals(entry, _selected)) row.Themed(Border.BackgroundProperty, "PulseAccentSubtle");
            row.Focusable = true;
            AutomationProperties.SetName(row, entry.Name);
            row.PointerPressed += (_, _) => { Select(entry); Rebuild(); };
            row.KeyDown += (_, args) =>
            {
                var index = matching.ToList().IndexOf(entry) + (args.Key == Key.Down ? 1 : args.Key == Key.Up ? -1 : 0);
                if (index < 0 || index >= matching.Count || args.Key is not (Key.Up or Key.Down)) return;
                Select(matching[index]);
                Rebuild();
                _rows.Children[index].Focus();
                args.Handled = true;
            };
            _rows.Children.Add(row);
        }

        _empty.IsVisible = matching.Count == 0;
    }

    private Border Row(string name, string value, string reference, string scope, bool header)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("140,110,*,90"), ColumnSpacing = _kit.Metrics.SpacingSmall };
        foreach (var (text, column) in new[] { (name, 0), (value, 1), (reference, 2), (scope, 3) })
        {
            var cell = new TextBlock
            {
                Text = text,
                FontSize = header ? _kit.Metrics.FontSizeSmall : _kit.Metrics.FontSize,
                FontWeight = header || column == 0 ? FontWeight.SemiBold : FontWeight.Normal,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontFamily = column == 2 && !header ? new FontFamily("Menlo, Consolas, monospace") : FontFamily.Default,
            }.Themed(TextBlock.ForegroundProperty, header || column == 1 ? Palette.Keys.TextSecondary : Palette.Keys.TextPrimary);
            ToolTip.SetTip(cell, text);
            Grid.SetColumn(cell, column);
            grid.Children.Add(cell);
        }

        return new Border { Padding = new Thickness(8, 4), Child = grid, Background = Brushes.Transparent };
    }
}

public sealed class SheetNameValuePreview(Workbook workbook, int sheetIndex, Engine.Operations.ISheetRangeReferences references) : INameValuePreview
{
    public string Value(string expression)
    {
        var text = (expression ?? string.Empty).Trim().TrimStart('=');
        if (references.Parse(workbook, text, sheetIndex) is not { } reference) return string.Empty;
        var range = reference.Range;
        return range.IsSingleCell
            ? workbook.Sheets[reference.SheetIndex][range.Start].Value.ToString()
            : $"{{{range.RowCount} × {range.ColumnCount}}}";
    }
}

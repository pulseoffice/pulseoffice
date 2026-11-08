using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Text.Spelling;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.AutoCorrection;

public sealed class AutoCorrectReplacementsPage(AutoCorrectChrome chrome, IReplacementQuery query, AutoCorrectImport import) : AutoCorrectPage(chrome)
{
    private readonly TextBox _search = new();
    private readonly TextBox _from = new();
    private readonly TextBox _to = new();
    private readonly TextBlock _message = chrome.Message();
    private readonly TextBlock _count = new();
    private readonly TextBlock _empty = new();
    private readonly ListBox _list = new();
    private readonly Button _sortFrom = new();
    private readonly Button _sortTo = new();
    private Button? _restore;
    private ReplacementSort _sort = ReplacementSort.FromAscending;
    private ReplacementListDraft? _draft;
    private LanguageDraft? _language;

    public override string TitleKey => "autocorrect.section.replacements";

    public override string IconKey => "ui.swap";

    public override string DescriptionKey => "autocorrect.section.replacements.description";

    public override bool UsesLanguage => true;

    public override void ShowLanguage(LanguageDraft language)
    {
        if (_draft is not null) _draft.Changed -= OnChanged;
        _language = language;
        _draft = language.Replacements;
        _draft.Changed += OnChanged;
        Chrome.Show(_message, string.Empty);
        Refill();
    }

    protected override Control Build()
    {
        var metrics = Chrome.Metrics;

        _search.PlaceholderText = Chrome.Texts["autocorrect.search"];
        _search.Padding = new Thickness(30, metrics.SpacingSmall, metrics.SpacingSmall, metrics.SpacingSmall);
        _search.VerticalContentAlignment = VerticalAlignment.Center;
        _search.TextChanged += (_, _) => Refill();
        Avalonia.Automation.AutomationProperties.SetName(_search, Chrome.Texts["autocorrect.search"]);

        var searchIcon = Chrome.Icon("ui.search", 14);
        searchIcon.IsHitTestVisible = false;
        searchIcon.HorizontalAlignment = HorizontalAlignment.Left;
        searchIcon.Margin = new Thickness(10, 0, 0, 0);

        _count.FontSize = metrics.FontSizeSmall;
        _count.VerticalAlignment = VerticalAlignment.Center;
        _count.Margin = new Thickness(metrics.SpacingMedium, 0, 0, 0);
        _count.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        var searchRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        searchRow.Children.Add(new Panel { Children = { _search, searchIcon } });
        Grid.SetColumn(_count, 1);
        searchRow.Children.Add(_count);

        Style(_from, Chrome.Texts["autocorrect.placeholder.from"]);
        Style(_to, Chrome.Texts["autocorrect.placeholder.to"]);
        _from.KeyDown += AddOnEnter;
        _to.KeyDown += AddOnEnter;
        _from.TextChanged += (_, _) => Chrome.Show(_message, string.Empty);
        _to.TextChanged += (_, _) => Chrome.Show(_message, string.Empty);

        var add = Chrome.Primary(Chrome.Texts["autocorrect.add"], Add);
        add.Margin = new Thickness(metrics.SpacingSmall, 0, 0, 0);

        var addRow = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,Auto,3*,Auto") };
        Place(addRow, _from, 0);
        Place(addRow, new TextBlock
        {
            Text = "→",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(metrics.SpacingSmall, 0),
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextDisabled), 1);
        Place(addRow, _to, 2);
        Place(addRow, add, 3);

        var addBlock = new StackPanel
        {
            Margin = new Thickness(0, metrics.SpacingMedium, 0, 0),
            Children = { Chrome.Caption(Chrome.Texts["autocorrect.newEntry"]), addRow, _message },
            Spacing = 4,
        };

        _list.Classes.Add("compact");
        _list.SelectionMode = SelectionMode.Single;
        _list.ItemTemplate = new FuncDataTemplate<ReplacementEntry>((entry, _) => entry is null ? new Border() : Row(entry), supportsRecycling: false);

        _empty.Text = Chrome.Texts["autocorrect.none"];
        _empty.IsVisible = false;
        _empty.HorizontalAlignment = HorizontalAlignment.Center;
        _empty.Margin = new Thickness(0, metrics.SpacingLarge * 2);
        _empty.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        var table = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        var header = Header();
        Grid.SetRow(header, 0);
        var body = new Panel { Children = { _list, _empty } };
        Grid.SetRow(body, 1);
        table.Children.Add(header);
        table.Children.Add(body);

        var frame = Chrome.Frame(table);
        frame.Margin = new Thickness(0, metrics.SpacingMedium);

        _restore = Chrome.Quiet(Chrome.Texts["autocorrect.restoreDefaults"], () => _draft?.RestoreDefaults());
        var hint = Chrome.Description(Chrome.Texts["autocorrect.editHint"]);
        hint.VerticalAlignment = VerticalAlignment.Center;
        var importButton = Chrome.Quiet(Chrome.Texts["autocorrect.import"], () => _ = ImportAsync());
        ToolTip.SetTip(importButton, Chrome.Texts["autocorrect.import.tip"]);
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = metrics.SpacingSmall,
            Children = { importButton, _restore },
        };
        var bottom = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        bottom.Children.Add(hint);
        Grid.SetColumn(actions, 1);
        bottom.Children.Add(actions);

        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto") };
        Grid.SetRow(searchRow, 0);
        Grid.SetRow(addBlock, 1);
        Grid.SetRow(frame, 2);
        Grid.SetRow(bottom, 3);
        grid.Children.Add(searchRow);
        grid.Children.Add(addBlock);
        grid.Children.Add(frame);
        grid.Children.Add(bottom);

        return grid;
    }

    private Control Header()
    {
        var metrics = Chrome.Metrics;
        SortButton(_sortFrom, ReplacementSort.FromAscending, ReplacementSort.FromDescending);
        SortButton(_sortTo, ReplacementSort.ToAscending, ReplacementSort.ToDescending);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(ReplacementRowView.Columns) };
        Place(grid, _sortFrom, 0);
        Place(grid, new Border { Width = 22 }, 1);
        Place(grid, _sortTo, 2);
        Place(grid, new Border(), 3);
        Place(grid, new Border { Width = 24 }, 4);

        return new Border
        {
            Padding = new Thickness(metrics.SpacingSmall, 4),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = grid,
        }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceRaised)
         .Themed(Border.BorderBrushProperty, Palette.Keys.Border);
    }

    private void SortButton(Button button, ReplacementSort ascending, ReplacementSort descending)
    {
        button.HorizontalAlignment = HorizontalAlignment.Left;
        button.Background = Brushes.Transparent;
        button.BorderThickness = new Thickness(0);
        button.Padding = new Thickness(Chrome.Metrics.SpacingSmall, 2);
        button.Classes.Add("ribbon");
        button.Click += (_, _) =>
        {
            _sort = _sort == ascending ? descending : ascending;
            Refill();
        };
    }

    private Control Row(ReplacementEntry entry)
    {
        var row = new ReplacementRowView(Chrome, entry, Update, from => _draft?.Remove(from));
        row.Rejected += (_, problem) => Chrome.Show(_message, Chrome.Problem(problem, "autocorrect.problem.invalid"));
        return row;
    }

    private EntryProblem Update(string original, string from, string to) =>
        _draft?.Update(original, from, to) ?? EntryProblem.None;

    private void Add()
    {
        if (_draft is null) return;

        var problem = _draft.Add(_from.Text ?? string.Empty, _to.Text ?? string.Empty);
        if (problem != EntryProblem.None)
        {
            Chrome.Show(_message, Chrome.Problem(problem, "autocorrect.problem.invalid"));
            return;
        }

        var added = _from.Text!.Trim();
        _from.Text = string.Empty;
        _to.Text = string.Empty;
        _from.Focus();
        _list.SelectedItem = (_list.ItemsSource as IEnumerable<ReplacementEntry>)?
            .FirstOrDefault(entry => string.Equals(entry.From, added, StringComparison.OrdinalIgnoreCase));
        if (_list.SelectedItem is { } selected) _list.ScrollIntoView(selected);
    }

    private async Task ImportAsync()
    {
        if (_language is null) return;

        var outcome = await import.RunAsync(_language).ConfigureAwait(true);
        if (outcome is { } shown) Chrome.Show(_message, shown.Text, shown.Success);
    }

    private void AddOnEnter(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        e.Handled = true;
        Add();
    }

    private void OnChanged(object? sender, EventArgs e)
    {
        Chrome.Show(_message, string.Empty);
        Refill();
    }

    private void Refill()
    {
        if (_draft is null) return;

        var visible = query.Apply(_draft.Entries, _search.Text, _sort);
        _list.ItemsSource = visible;
        _empty.IsVisible = visible.Count == 0;
        _count.Text = Chrome.Count(visible.Count, _draft.Count);
        _sortFrom.Content = Label("autocorrect.column.from", ReplacementSort.FromAscending, ReplacementSort.FromDescending);
        _sortTo.Content = Label("autocorrect.column.to", ReplacementSort.ToAscending, ReplacementSort.ToDescending);
        if (_restore is not null) _restore.IsEnabled = !_draft.IsAtDefaults;
    }

    private string Label(string key, ReplacementSort ascending, ReplacementSort descending)
    {
        var mark = _sort == ascending ? " ▲" : _sort == descending ? " ▼" : string.Empty;
        return Chrome.Texts[key].ToUpper(System.Globalization.CultureInfo.CurrentCulture) + mark;
    }

    private void Style(TextBox box, string placeholder)
    {
        box.PlaceholderText = placeholder;
        Avalonia.Automation.AutomationProperties.SetName(box, placeholder.TrimEnd('…'));
        box.Padding = new Thickness(Chrome.Metrics.SpacingMedium, Chrome.Metrics.SpacingSmall);
        box.VerticalContentAlignment = VerticalAlignment.Center;
    }

    private void Place(Grid grid, Control control, int column)
    {
        Grid.SetColumn(control, column);
        grid.Children.Add(control);
    }
}

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Sheets.UI.DataDialogs;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Panels;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI;

public sealed record FilterChoice(IReadOnlySet<string>? AllowedValues, string? Criteria, bool IncludeBlanks, ColorFilterCriterion? Color = null)
{
    public string? SecondCriteria { get; init; }

    public CriteriaJoin Join { get; init; }

    public bool? SortDescending { get; init; }
}

public sealed class FilterMenuView : UserControl, IDialogContent, IDialogFooterOwner, IDialogSizeHint
{
    private readonly LedgerDialogKit _kit;
    private readonly IFilterCriteriaCodec _codec;
    private readonly IFilterSwatchColours _swatches;
    private readonly IReadOnlyList<FilterOperator> _conditions;
    private readonly StackPanel _list = new() { Spacing = 0 };
    private readonly CheckBox _all;
    private readonly CheckBox _blanks;
    private readonly TextBlock _summary;
    private readonly TextBlock _empty;
    private readonly TextBlock _more;
    private readonly List<ToggleButton> _colourButtons = [];
    private readonly HashSet<string> _expanded = new(StringComparer.Ordinal);
    private bool _syncing;

    public FilterMenuView(string column, FilterColumnFacts facts, ColumnFilter? current, LedgerDialogKit kit, IFilterCriteriaCodec codec, IFilterSwatchColours? swatches = null)
    {
        _swatches = swatches ?? new FilterSwatchColours();
        ArgumentNullException.ThrowIfNull(facts);
        _kit = kit ?? throw new ArgumentNullException(nameof(kit));
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        Model = new FilterMenuModel(facts, current);
        var metrics = kit.Metrics;

        var title = new TextBlock { Text = column, FontWeight = FontWeight.SemiBold, FontSize = metrics.FontSize + 1 }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextPrimary);

        SortAscending = SortButton(facts.Numeric ? "filter.sort.smallest" : "filter.sort.az", "sort.ascending", false);
        SortDescendingButton = SortButton(facts.Numeric ? "filter.sort.largest" : "filter.sort.za", "sort.descending", true);

        Search = new PanelSearchBox(kit.Icons, metrics, kit["filter.search"]);
        Search.TextChanged += (_, _) => Model.Search = Search.Text;
        Search.Descend += (_, _) => _all!.Focus();

        _all = new CheckBox { Content = kit["filter.selectAll"], FontWeight = FontWeight.SemiBold };
        _all.IsCheckedChanged += (_, _) =>
        {
            if (_syncing) return;
            Model.ChooseAllVisible(Model.AllVisibleChosen != true);
        };
        _blanks = new CheckBox { IsChecked = Model.Blanks, IsVisible = facts.Blanks > 0 };
        _blanks.Content = Labelled(kit["filter.blanksItem"], facts.Blanks);
        _blanks.IsCheckedChanged += (_, _) => { Model.Blanks = _blanks.IsChecked == true; Summarize(); };
        _empty = kit.Note(kit["filter.noMatch"]);
        _more = kit.Note(string.Empty);

        var listFrame = new PanelListFrame(metrics, new ScrollViewer
        {
            MaxHeight = 220,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new StackPanel { Margin = new Thickness(6, 4), Children = { _all, _list, _more, _blanks, _empty } },
        });

        _conditions = codec.For(facts.Numeric);
        var dates = Model.IsDateColumn;
        var label = kit[facts.Numeric ? "filter.byNumber" : "filter.byText"];
        (Condition, ConditionValue) = ConditionRow(codec.Read(current?.Criteria, dates), label);
        (SecondCondition, SecondConditionValue) = ConditionRow(codec.Read(current?.SecondCriteria, dates), label);
        Join = new ComboBox
        {
            ItemsSource = new[] { kit["filter.and"], kit["filter.or"] },
            SelectedIndex = current?.Join == CriteriaJoin.Or ? 1 : 0,
            MinWidth = 76,
        };
        AutomationProperties.SetName(Join, kit["filter.join"]);

        var colours = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var colour in facts.Colours.Concat(current?.Color is { } chosen && !facts.Colours.Contains(chosen) ? [chosen] : []))
            colours.Children.Add(ColourButton(colour, current?.Color == colour));

        _summary = kit.Note();

        var layout = new StackPanel
        {
            Spacing = metrics.SpacingMedium,
            Children =
            {
                title,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = metrics.SpacingSmall, Children = { SortAscending, SortDescendingButton } },
                Section("filter.section.values", new StackPanel { Spacing = metrics.SpacingSmall, Children = { Search, listFrame } }),
                Section(facts.Numeric ? "filter.byNumber" : "filter.byText", new StackPanel
                {
                    Spacing = metrics.SpacingSmall,
                    Children =
                    {
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = metrics.SpacingSmall, Children = { Condition, ConditionValue } },
                        Join,
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = metrics.SpacingSmall, Children = { SecondCondition, SecondConditionValue } },
                    },
                }),
            },
        };
        if (colours.Children.Count > 0) layout.Children.Add(Section("filter.byColour", colours));
        layout.Children.Add(_summary);
        Content = layout;

        Model.Changed += (_, _) => Rebuild();
        Model.SelectionChanged += (_, _) => Sync();
        Rebuild();

        Footer = new DialogFooter()
            .WithCancel(kit["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(kit["filter.clear"], () => Completed?.Invoke(this, new FilterChoice(null, null, true)))
            .WithAction(kit["prompt.confirm"], () => Completed?.Invoke(this, Choice), primary: true);
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer { get; }

    public double? PreferredWidth => 380;

    public FilterMenuModel Model { get; }

    public PanelSearchBox Search { get; }

    public Button SortAscending { get; }

    public Button SortDescendingButton { get; }

    public ComboBox Condition { get; }

    public TextBox ConditionValue { get; }

    public ComboBox Join { get; }

    public ComboBox SecondCondition { get; }

    public TextBox SecondConditionValue { get; }

    public CheckBox SelectAll => _all;

    public IReadOnlyList<CheckBox> ValueBoxes => [.. _list.Children.OfType<CheckBox>()];

    public IReadOnlyList<ToggleButton> ColourButtons => _colourButtons;

    public string Summary => _summary.Text ?? string.Empty;

    public FilterChoice Choice
    {
        get
        {
            var first = Written(Condition, ConditionValue);
            var second = Written(SecondCondition, SecondConditionValue);
            return new FilterChoice(
                Model.Allowed(),
                first ?? second,
                Model.IncludesBlanks,
                _colourButtons.FirstOrDefault(button => button.IsChecked == true)?.Tag as ColorFilterCriterion)
            {
                SecondCriteria = first is null ? null : second,
                Join = Join.SelectedIndex == 1 ? CriteriaJoin.Or : CriteriaJoin.And,
            };
        }
    }

    private string? Written(ComboBox condition, TextBox value) =>
        _codec.Write(new FilterConditionDraft(_conditions[Math.Max(condition.SelectedIndex, 0)], value.Text ?? string.Empty));

    private (ComboBox Condition, TextBox Value) ConditionRow(FilterConditionDraft draft, string label)
    {
        var condition = new ComboBox
        {
            ItemsSource = _conditions.Select(entry => _kit[entry == FilterOperator.None ? "filter.condition.none" : "filter.condition." + Camel(entry.ToString())]).ToList(),
            SelectedIndex = Math.Max(0, _conditions.ToList().IndexOf(draft.Condition)),
            MinWidth = 150,
        };
        AutomationProperties.SetName(condition, label);
        var value = new TextBox { Text = draft.Value, MinWidth = 120, PlaceholderText = _kit["filter.value"] };
        AutomationProperties.SetName(value, _kit["filter.value"]);
        condition.SelectionChanged += (_, _) => value.IsEnabled = condition.SelectedIndex > 0;
        value.IsEnabled = condition.SelectedIndex > 0;
        return (condition, value);
    }

    private Button SortButton(string labelKey, string icon, bool descending)
    {
        var button = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = _kit.Metrics.SpacingSmall,
                Children =
                {
                    _kit.Icons.Find(icon) is { } geometry
                        ? new IconView(geometry, _kit.Metrics.IconSize).Themed(IconView.ForegroundProperty, Palette.Keys.TextPrimary)
                        : new TextBlock(),
                    new TextBlock { Text = _kit[labelKey], VerticalAlignment = VerticalAlignment.Center },
                },
            },
        };
        button.Classes.Add("dialogQuiet");
        AutomationProperties.SetName(button, _kit[labelKey]);
        button.Click += (_, _) => Completed?.Invoke(this, Choice with { SortDescending = descending });
        return button;
    }

    private ToggleButton ColourButton(ColorFilterCriterion colour, bool chosen)
    {
        var key = colour.Target == ColorFilterTarget.Background ? "filter.color.background" : "filter.color.text";
        var swatch = new Border
        {
            Width = 18,
            Height = 18,
            CornerRadius = new CornerRadius(3),
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Gray,
            Background = new SolidColorBrush(_swatches.Fill(colour)),
            Child = colour.Target == ColorFilterTarget.Text
                ? new TextBlock { Text = "A", FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(_swatches.Ink(colour)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                : null,
        };
        var button = new ToggleButton { Content = swatch, Tag = colour, IsChecked = chosen, Padding = new Thickness(3), Margin = new Thickness(0, 0, 4, 4) };
        button.Classes.Add("swatch");
        ToolTip.SetTip(button, $"{_kit[key]} {colour.ColorHex}");
        AutomationProperties.SetName(button, $"{_kit[key]} {colour.ColorHex}");
        button.IsCheckedChanged += (_, _) =>
        {
            if (button.IsChecked != true) return;
            foreach (var other in _colourButtons.Where(other => other != button)) other.IsChecked = false;
        };
        _colourButtons.Add(button);
        return button;
    }

    private Control Section(string titleKey, Control body) => new StackPanel
    {
        Spacing = _kit.Metrics.SpacingSmall,
        Children = { _kit.Caption(titleKey), body },
    };

    private Control Labelled(string text, int count)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = _kit.Metrics.SpacingMedium, MinWidth = 260 };
        grid.Children.Add(new TextBlock { Text = text, TextTrimming = TextTrimming.CharacterEllipsis });
        var number = new TextBlock { Text = count.ToString(System.Globalization.CultureInfo.CurrentCulture), FontSize = _kit.Metrics.FontSizeSmall, VerticalAlignment = VerticalAlignment.Center }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        Grid.SetColumn(number, 1);
        grid.Children.Add(number);
        return grid;
    }

    public IReadOnlyList<CheckBox> DateBoxes => [.. _list.Children.OfType<Grid>().SelectMany(row => row.Children.OfType<CheckBox>())];

    public IReadOnlyList<Button> DateExpanders => [.. _list.Children.OfType<Grid>().SelectMany(row => row.Children.OfType<Button>())];

    private void RebuildDates()
    {
        foreach (var year in Model.DateGroups()) AddDate(year);
    }

    private void AddDate(FilterDateGroup group)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(group.Level * 16, 0, 0, 0) };
        var box = new CheckBox { Content = Labelled(group.Label, group.Items.Sum(item => item.Value.Count)), IsChecked = Model.Chosen(group), Tag = group };
        AutomationProperties.SetName(box, group.Label);
        box.IsCheckedChanged += (_, _) =>
        {
            if (_syncing) return;
            Model.Choose(group, Model.Chosen(group) != true);
        };
        Grid.SetColumn(box, 1);
        if (group.Children.Count > 0)
        {
            var open = _expanded.Contains(group.Key);
            var toggle = new Button
            {
                Content = _kit.Icon(open ? "sort.level.down" : "series.direction.right", 0.7),
                Padding = new Thickness(2),
                VerticalAlignment = VerticalAlignment.Center,
                Tag = group.Key,
            };
            toggle.Classes.Add("dialogQuiet");
            AutomationProperties.SetName(toggle, _kit.Texts.Format(open ? "filter.date.collapse" : "filter.date.expand", group.Label));
            toggle.Click += (_, _) =>
            {
                if (!_expanded.Remove(group.Key)) _expanded.Add(group.Key);
                Rebuild();
            };
            row.Children.Add(toggle);
        }
        else
        {
            row.Children.Add(new Border { Width = 18 });
        }

        row.Children.Add(box);
        _list.Children.Add(row);
        if (_expanded.Contains(group.Key) || Model.ExpandsDatesOnSearch)
            foreach (var child in group.Children) AddDate(child);
    }

    private void Rebuild()
    {
        _syncing = true;
        _list.Children.Clear();
        if (Model.IsDateColumn) RebuildDates();
        else foreach (var item in Model.Listed)
        {
            var label = Model.Label(item);
            var box = new CheckBox { Content = Labelled(label, item.Value.Count), IsChecked = item.Chosen, Tag = item.Value.Value };
            AutomationProperties.SetName(box, label);
            box.IsCheckedChanged += (_, _) =>
            {
                if (_syncing) return;
                Model.Choose(item, box.IsChecked == true);
            };
            _list.Children.Add(box);
        }

        _all.IsChecked = Model.AllVisibleChosen;
        _all.IsVisible = Model.Visible.Count > 0;
        _empty.IsVisible = Model.Visible.Count == 0;
        _more.Text = _kit.Texts.Format("filter.moreValues", Model.Unlisted);
        _more.IsVisible = !Model.IsDateColumn && Model.Unlisted > 0;
        _blanks.IsVisible = Model.Facts.Blanks > 0 && Model.Search.Length == 0;
        _syncing = false;
        Summarize();
    }

    private void Sync()
    {
        _syncing = true;
        foreach (var (box, item) in _list.Children.OfType<CheckBox>().Zip(Model.Listed)) box.IsChecked = item.Chosen;
        foreach (var box in DateBoxes) if (box.Tag is FilterDateGroup group) box.IsChecked = Model.Chosen(group);
        _all.IsChecked = Model.AllVisibleChosen;
        _syncing = false;
        Summarize();
    }

    private void Summarize() => _summary.Text = _kit.Texts.Format("filter.summary", Model.ShownRows());

    private string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}

public interface IFilterSwatchColours
{
    Color Fill(ColorFilterCriterion colour);

    Color Ink(ColorFilterCriterion colour);
}

public sealed class FilterSwatchColours : IFilterSwatchColours
{
    public Color Fill(ColorFilterCriterion colour)
    {
        ArgumentNullException.ThrowIfNull(colour);
        return colour.Target == ColorFilterTarget.Background ? Parse(colour.ColorHex, Colors.White) : Colors.White;
    }

    public Color Ink(ColorFilterCriterion colour)
    {
        ArgumentNullException.ThrowIfNull(colour);
        return Parse(colour.ColorHex, Colors.Black);
    }

    private Color Parse(string? hex, Color fallback) => hex is not null && Color.TryParse(hex, out var parsed) ? parsed : fallback;
}

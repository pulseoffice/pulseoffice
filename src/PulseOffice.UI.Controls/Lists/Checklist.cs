using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls.Lists;

public sealed class ChecklistItem(string name, bool isChecked)
{
    public string Name { get; } = name;

    public bool IsChecked { get; set; } = isChecked;
}

public sealed class ChecklistModel
{
    private readonly List<ChecklistItem> _items;

    public ChecklistModel(IEnumerable<string> options, IEnumerable<string> checkedOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        var selected = new HashSet<string>(checkedOptions ?? [], StringComparer.Ordinal);
        _items = [.. options.Select(option => new ChecklistItem(option, selected.Contains(option)))];
        Visible = _items;
    }

    public IReadOnlyList<ChecklistItem> Items => _items;

    public IReadOnlyList<ChecklistItem> Visible { get; private set; }

    public string Query { get; private set; } = string.Empty;

    public int CheckedCount => _items.Count(item => item.IsChecked);

    public IReadOnlyList<string> Checked => [.. _items.Where(item => item.IsChecked).Select(item => item.Name)];

    public void Filter(string? query)
    {
        Query = (query ?? string.Empty).Trim();
        Visible = Query.Length == 0
            ? _items
            : [.. _items.Where(item => CultureInfo.CurrentCulture.CompareInfo.IndexOf(
                item.Name, Query, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0)];
    }

    public void SetVisible(bool isChecked)
    {
        foreach (var item in Visible) item.IsChecked = isChecked;
    }
}

public sealed record ChecklistField(
    string Key, string Label, IReadOnlyList<string> Options, IReadOnlyCollection<string> Checked)
    : CustomFormField(Key, Label)
{
    public const int SearchThreshold = 8;

    public override FormFieldEditor Create(ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        var control = new ChecklistControl(new ChecklistModel(Options, Checked), texts, metrics);
        return new FormFieldEditor(control, () => control.Model.Checked);
    }
}

public sealed class ChecklistControl : UserControl
{
    private const double ListHeight = 240;

    private readonly ITextCatalog _texts;
    private readonly DesignTokens.Metrics _metrics;
    private readonly ListBox _list;
    private readonly TextBlock _count;
    private readonly TextBlock _empty;

    public ChecklistControl(ChecklistModel model, ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _metrics = metrics;

        _list = new ListBox
        {
            ItemTemplate = new FuncDataTemplate<ChecklistItem?>((item, _) => item is null ? new Panel() : Row(item), supportsRecycling: false),
            Background = Avalonia.Media.Brushes.Transparent,
            BorderThickness = default,
            SelectionMode = SelectionMode.Single,
        };
        _list.Classes.Add("checklist");

        _empty = new TextBlock
        {
            Text = texts["list.noMatch"],
            FontSize = metrics.FontSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        _count = new TextBlock
        {
            FontSize = metrics.FontSizeSmall,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        var toolbar = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*"),
            ColumnSpacing = metrics.SpacingSmall,
            Margin = new Thickness(metrics.SpacingSmall, metrics.SpacingSmall / 2),
        };
        toolbar.Children.Add(Quiet(texts["list.selectAll"], () => SetVisible(true)));
        var none = Quiet(texts["list.selectNone"], () => SetVisible(false));
        Grid.SetColumn(none, 1);
        toolbar.Children.Add(none);
        Grid.SetColumn(_count, 2);
        toolbar.Children.Add(_count);

        var layout = new DockPanel();

        if (model.Items.Count > ChecklistField.SearchThreshold)
        {
            var search = new TextBox
            {
                PlaceholderText = texts["list.search"],
                Margin = new Thickness(metrics.SpacingSmall, metrics.SpacingSmall, metrics.SpacingSmall, 0),
            };
            Avalonia.Automation.AutomationProperties.SetName(search, texts["list.search"]);
            search.TextChanged += (_, _) =>
            {
                Model.Filter(search.Text);
                Show();
            };
            DockPanel.SetDock(search, Dock.Top);
            layout.Children.Add(search);
        }

        var divider = new Border { Height = 1 }.Themed(Border.BackgroundProperty, Palette.Keys.Border);
        DockPanel.SetDock(toolbar, Dock.Top);
        DockPanel.SetDock(divider, Dock.Top);
        layout.Children.Add(toolbar);
        layout.Children.Add(divider);
        layout.Children.Add(new Panel
        {
            Height = Math.Min(ListHeight, Math.Max(1, model.Items.Count) * (metrics.ControlHeight + 2) + metrics.SpacingSmall),
            Children = { _list, _empty },
        });

        Content = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(metrics.RadiusSoft),
            ClipToBounds = true,
            Child = layout,
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border)
         .Themed(Border.BackgroundProperty, Palette.Keys.Surface);

        Show();
    }

    public ChecklistModel Model { get; }

    public event EventHandler? Changed;

    public int RealizedRows => _list.GetRealizedContainers().Count();

    private void SetVisible(bool isChecked)
    {
        Model.SetVisible(isChecked);
        Show();
    }

    private void Show()
    {
        _list.ItemsSource = Model.Visible.ToList();
        _empty.IsVisible = Model.Visible.Count == 0;
        Count();
    }

    private void Count()
    {
        _count.Text = _texts.Format("list.checkedCount", Model.CheckedCount, Model.Items.Count);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private Control Row(ChecklistItem item)
    {
        var box = new CheckBox
        {
            Content = item.Name,
            IsChecked = item.IsChecked,
            MinHeight = 0,
            Margin = new Thickness(_metrics.SpacingSmall, 0),
        };
        box.IsCheckedChanged += (_, _) =>
        {
            item.IsChecked = box.IsChecked == true;
            Count();
        };
        return box;
    }

    private Button Quiet(string text, Action run)
    {
        var button = new Button { Content = text, FontSize = _metrics.FontSizeSmall, Padding = new Thickness(_metrics.SpacingSmall, 2) };
        button.Classes.Add("dialogQuiet");
        button.Click += (_, _) => run();
        return button;
    }
}

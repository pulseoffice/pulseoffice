using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Localization;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI;

public sealed class FunctionWizardView : UserControl, IDialogContent, PulseOffice.UI.Controls.IDialogFooterOwner
{
    private readonly IReadOnlyList<FunctionDefinition> _functions;
    private readonly ITextCatalog _texts;
    private readonly DesignTokens.Metrics _metrics = DesignTokens.For(InterfaceDensity.Compact);

    private readonly ListBox _categories = new();
    private readonly ListBox _list = new();
    private Button? _confirm;
    private readonly TextBox _search = new();
    private readonly TextBlock _description = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _signature = new() { TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _arguments = new() { Spacing = 4 };
    private readonly TextBlock _preview = new() { TextWrapping = TextWrapping.Wrap };

    private readonly List<TextBox> _argumentBoxes = [];
    private List<FunctionDefinition> _shown = [];

    public event EventHandler<object?>? Completed;

    public Avalonia.Controls.Control? Footer { get; private set; }

    private readonly IFormulaLocalization _localization;

    private readonly IFunctionCategoryLooks _looks;
    private readonly PulseOffice.UI.Icons.IIconCatalog _icons;

    public FunctionWizardView(
        IFunctionRegistry registry, ITextCatalog texts, IFormulaLocalization localization, IFunctionCategoryLooks looks, PulseOffice.UI.Icons.IIconCatalog icons)
    {
        _looks = looks ?? throw new ArgumentNullException(nameof(looks));
        _icons = icons ?? throw new ArgumentNullException(nameof(icons));
        ArgumentNullException.ThrowIfNull(registry);
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));

        _functions = [.. registry.All.OrderBy(function => function.Name, StringComparer.Ordinal)];
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));

        Content = Build(registry);
        Reload();
    }

    private Control Build(IFunctionRegistry registry)
    {
        Footer = new PulseOffice.UI.Controls.DialogFooter()
            .WithCancel(_texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(_texts["prompt.confirm"], () => { if (Chosen is not null) Completed?.Invoke(this, Formula()); }, out _confirm, primary: true);

        _search.PlaceholderText = _texts["functions.search"];
        _search.TextChanged += (_, _) => Reload();

        _categories.ItemsSource = _looks.Entries(registry.Categories,
            category => category is null ? _functions.Count : _functions.Count(function => function.Category == category));
        _categories.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<FunctionCategoryEntry?>((entry, _) => entry is null ? new Panel() : CategoryRow(entry), supportsRecycling: false);

        _categories.SelectedIndex = 0;
        _categories.Classes.Add("compact");
        _categories.SelectionChanged += (_, _) => Reload();

        _list.Classes.Add("compact");
        _list.SelectionChanged += (_, _) => Describe();

        var layout = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("170,190,*"),
            RowDefinitions = new RowDefinitions("Auto,*"),
            ColumnSpacing = _metrics.SpacingMedium,
            Height = 360,
        };

        _search.Margin = new Thickness(0, 0, 0, _metrics.SpacingMedium);
        Grid.SetColumnSpan(_search, 3);

        var categories = Column(_texts["functions.categories"], Plate(_categories));
        var functions = Column(_texts["functions.list"], Plate(_list));
        var detail = Detail();

        Grid.SetRow(categories, 1);
        Grid.SetRow(functions, 1);
        Grid.SetRow(detail, 1);

        Grid.SetColumn(categories, 0);
        Grid.SetColumn(functions, 1);
        Grid.SetColumn(detail, 2);

        layout.Children.Add(_search);
        layout.Children.Add(categories);
        layout.Children.Add(functions);
        layout.Children.Add(detail);

        return layout;
    }

    public IReadOnlyList<FunctionCategoryEntry> Categories => _categories.ItemsSource as IReadOnlyList<FunctionCategoryEntry> ?? [];

    public void SelectCategory(int index) => _categories.SelectedIndex = index;

    public int ShownCount => _shown.Count;

    public string DescriptionText => _description.Text ?? string.Empty;

    private Control CategoryRow(FunctionCategoryEntry entry)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = _metrics.SpacingSmall };
        if (_icons.Find(entry.Icon) is { } icon)
            row.Children.Add(new PulseOffice.UI.Controls.IconView(icon, _metrics.IconSize * 0.85) { VerticalAlignment = VerticalAlignment.Center });
        var label = new TextBlock { Text = entry.Label, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        Grid.SetColumn(label, 1);
        row.Children.Add(label);
        var count = new TextBlock
        {
            Text = entry.Count.ToString(System.Globalization.CultureInfo.CurrentCulture),
            FontSize = _metrics.FontSizeSmall,
            VerticalAlignment = VerticalAlignment.Center,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        Grid.SetColumn(count, 2);
        row.Children.Add(count);
        Avalonia.Automation.AutomationProperties.SetName(row, entry.Label);
        return row;
    }

    private Control Detail()
    {
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*,Auto,Auto") };

        _signature.FontWeight = FontWeight.SemiBold;
        _signature.FontSize = _metrics.FontSize;
        _signature.FontFamily = new FontFamily("Menlo, Consolas, monospace");

        var signaturePlate = new Border
        {
            Child = _signature,
            Padding = new Thickness(_metrics.SpacingMedium, _metrics.SpacingSmall),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
        };

        signaturePlate.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken);

        _description.FontSize = _metrics.FontSizeSmall;
        _description.Margin = new Thickness(0, _metrics.SpacingSmall, 0, 0);
        _description.LineHeight = _metrics.FontSizeSmall * 1.5;
        _description.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        var argumentsCaption = Caption(_texts["functions.arguments"]);
        argumentsCaption.Margin = new Thickness(0, _metrics.SpacingMedium, 0, _metrics.SpacingSmall / 2);

        _preview.FontFamily = new FontFamily("Menlo, Consolas, monospace");
        _preview.FontSize = _metrics.FontSizeSmall;

        var previewPlate = new Border
        {
            Child = _preview,
            Padding = new Thickness(_metrics.SpacingMedium, _metrics.SpacingSmall),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
        };

        previewPlate.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken);

        var resultCaption = Caption(_texts["functions.result"]);
        resultCaption.Margin = new Thickness(0, _metrics.SpacingMedium, 0, _metrics.SpacingSmall / 2);

        var scroller = new ScrollViewer
        {
            Content = _arguments,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };

        var caption = Caption(_texts["functions.list"]);
        caption.Opacity = 0;

        Grid.SetRow(caption, 0);
        Grid.SetRow(signaturePlate, 1);
        Grid.SetRow(_description, 2);
        Grid.SetRow(argumentsCaption, 3);
        Grid.SetRow(scroller, 4);
        Grid.SetRow(resultCaption, 5);
        Grid.SetRow(previewPlate, 6);

        layout.Children.Add(caption);
        layout.Children.Add(signaturePlate);
        layout.Children.Add(_description);
        layout.Children.Add(argumentsCaption);
        layout.Children.Add(scroller);
        layout.Children.Add(resultCaption);
        layout.Children.Add(previewPlate);

        return layout;
    }

    private Control Column(string caption, Control content)
    {
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };

        var title = Caption(caption);

        Grid.SetRow(title, 0);
        Grid.SetRow(content, 1);

        layout.Children.Add(title);
        layout.Children.Add(content);

        return layout;
    }

    private Control Plate(Control content)
    {
        var plate = new Border
        {
            Child = content,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
        };

        plate.Themed(Border.BorderBrushProperty, Palette.Keys.Border);
        plate.Themed(Border.BackgroundProperty, Palette.Keys.Surface);

        if (content is ListBox list)
        {
            list.BorderThickness = default;
            list.Background = Brushes.Transparent;
        }

        return plate;
    }

    private TextBlock Caption(string text) =>
        new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontSize = Math.Max(9, _metrics.FontSizeSmall - 1),
            LetterSpacing = 0.6,
            Margin = new Thickness(0, 0, 0, _metrics.SpacingSmall / 2),
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

    private void Reload()
    {
        var query = _search.Text ?? string.Empty;

        var category = (_categories.SelectedItem as FunctionCategoryEntry)?.Category;

        _shown = [.. _functions
            .Where(function => category is null || function.Category == category)
            .Where(function => query.Length == 0
                || function.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || Shown(function).Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || (function.Description?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false))
            .OrderBy(function => Closeness(Shown(function), query))
            .ThenBy(Shown, StringComparer.CurrentCultureIgnoreCase)];

        _list.ItemsSource = _shown.Select(Shown).ToList();
        _list.SelectedIndex = _shown.Count > 0 ? 0 : -1;

        Describe();
    }

    private static int Closeness(string name, string query) =>
        query.Length == 0 ? 0
        : name.Equals(query, StringComparison.CurrentCultureIgnoreCase) ? 0
        : name.StartsWith(query, StringComparison.CurrentCultureIgnoreCase) ? 1
        : name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ? 2
        : 3;

    private FunctionDefinition? Chosen =>
        _list.SelectedIndex >= 0 && _list.SelectedIndex < _shown.Count ? _shown[_list.SelectedIndex] : null;

    private void Describe()
    {
        _arguments.Children.Clear();
        _argumentBoxes.Clear();
        if (_confirm is not null) _confirm.IsEnabled = Chosen is not null;

        if (Chosen is not { } function)
        {
            _signature.Text = _texts["functions.choose"];
            _description.Text = _texts["functions.none"];
            _preview.Text = string.Empty;
            return;
        }

        var slots = function.MaxArguments is int.MaxValue or > 6
            ? Math.Max(function.MinArguments, 3)
            : function.MaxArguments;

        _signature.Text = $"{Shown(function)}({string.Join(ArgumentSeparator,
            Enumerable.Range(1, slots).Select(index => _texts.Format("functions.argument", index)))})";

        _description.Text = function.Description ?? _texts.Format("functions.category", CategoryLabel(function.Category));

        for (var index = 0; index < slots; index++)
        {
            var box = new TextBox { PlaceholderText = _texts.Format("functions.argument", index + 1) };
            box.TextChanged += (_, _) => UpdatePreview();

            _argumentBoxes.Add(box);
            _arguments.Children.Add(box);
        }

        if (slots == 0) _arguments.Children.Add(new TextBlock
        {
            Text = "—",
            FontSize = _metrics.FontSizeSmall,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));

        UpdatePreview();
    }

    private string CategoryLabel(string category) =>
        Categories.FirstOrDefault(entry => entry.Category == category)?.Label ?? category;

    private void UpdatePreview() => _preview.Text = Formula();

    private string Formula()
    {
        if (Chosen is not { } function) return string.Empty;

        var arguments = _argumentBoxes
            .Select(box => box.Text?.Trim() ?? string.Empty)
            .ToList();

        while (arguments.Count > 0 && arguments[^1].Length == 0) arguments.RemoveAt(arguments.Count - 1);

        return $"={Shown(function)}({string.Join(ArgumentSeparator, arguments)})";
    }

    private string Language => _texts.Language.ToCode();

    private string ArgumentSeparator => _localization.Punctuation(Language).Argument + " ";

    private string Shown(FunctionDefinition function) => _localization.FunctionName(function.Name, Language);
}

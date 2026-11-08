using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using PulseOffice.UI.Controls.Fields;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.CellFormat;

public sealed class FormatCellsServices(
    INumberFormatDesigner designer,
    INumberFormatParser parser,
    INumberFormatter formatter,
    IMeasureUnits units,
    IIconCatalog icons,
    IAngleDialGeometry angles,
    PulseOffice.UI.Controls.TextFormat.ICharacterFormatTabs characters,
    ICellCharacterFormat cellCharacters)
{
    public INumberFormatDesigner Designer { get; } = designer ?? throw new ArgumentNullException(nameof(designer));

    public INumberFormatParser Parser { get; } = parser ?? throw new ArgumentNullException(nameof(parser));

    public INumberFormatter Formatter { get; } = formatter ?? throw new ArgumentNullException(nameof(formatter));

    public IMeasureUnits Units { get; } = units ?? throw new ArgumentNullException(nameof(units));

    public IIconCatalog Icons { get; } = icons ?? throw new ArgumentNullException(nameof(icons));

    public IAngleDialGeometry Angles { get; } = angles ?? throw new ArgumentNullException(nameof(angles));

    public PulseOffice.UI.Controls.TextFormat.ICharacterFormatTabs Characters { get; } = characters ?? throw new ArgumentNullException(nameof(characters));

    public ICellCharacterFormat CellCharacters { get; } = cellCharacters ?? throw new ArgumentNullException(nameof(cellCharacters));
}

public sealed record FormattedSample(string Text, string? ColorName, bool Valid);

public sealed class NumberFormatPage : UserControl
{
    private readonly FormatCellsServices _services;
    private readonly NumberFormatCatalog _catalog;
    private readonly ITextCatalog _texts;
    private readonly DesignTokens.Metrics _metrics;
    private readonly CellValue _cellValue;
    private readonly string _cellName;

    private readonly ListBox _categories = new();
    private readonly StackPanel _options;
    private readonly TextBox _code = new();
    private readonly TextBlock _sample = new();
    private readonly TextBlock _sampleSource = new();
    private readonly TextBlock _codeMessage = new();
    private NumberFormatChoice _choice;
    private bool _syncing;

    public NumberFormatPage(
        string code, NumberFormatCatalog catalog, CellValue cellValue, string cellName,
        FormatCellsServices services, ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _metrics = metrics;
        _cellValue = cellValue;
        _cellName = cellName;
        _choice = _services.Designer.Recognize(code);

        _categories.Classes.Add("compact");
        _categories.ItemsSource = _services.Designer.Categories.Select(CategoryName).ToList();
        _categories.SelectedIndex = IndexOf(_choice.Category);
        _categories.Width = 132;
        _categories.MinHeight = 290;
        AutomationProperties.SetName(_categories, texts["format.category"]);
        _categories.SelectionChanged += (_, _) =>
        {
            if (_syncing || _categories.SelectedIndex < 0) return;
            ChangeCategory(_services.Designer.Categories[_categories.SelectedIndex]);
        };

        _options = new StackPanel { Spacing = metrics.SpacingMedium };

        _code.Text = code;
        AutomationProperties.SetName(_code, texts["format.code"]);
        _code.TextChanged += (_, _) =>
        {
            if (_syncing) return;
            _choice = _services.Designer.Recognize(_code.Text ?? string.Empty);
            SyncCategory();
            BuildOptions();
            ShowSample();
        };

        var right = new StackPanel { Spacing = metrics.SpacingMedium };
        right.Children.Add(SampleCard());
        right.Children.Add(_options);
        right.Children.Add(CodeRow());

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = metrics.SpacingLarge, MinWidth = 520 };
        grid.Children.Add(_categories);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        Content = grid;

        BuildOptions();
        ShowSample();
    }

    public string Code => _code.Text ?? string.Empty;

    public NumberFormatChoice Choice => _choice;

    public string SampleText => _sample.Text ?? string.Empty;

    public ListBox Categories => _categories;

    public void Choose(NumberFormatChoice choice)
    {
        _choice = choice ?? throw new ArgumentNullException(nameof(choice));
        SyncCategory();
        BuildOptions();
        Recompose();
    }

    public FormattedSample Render(string code, double? forcedNumber = null)
    {
        try
        {
            var format = _services.Parser.Parse(code);
            var value = forcedNumber is { } number ? CellValue.Number(number) : SampleValue();
            var formatted = _services.Formatter.Format(value, format, 40);
            return new FormattedSample(formatted.Text, formatted.ColorName, true);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or InvalidOperationException or IndexOutOfRangeException)
        {
            return new FormattedSample(string.Empty, null, false);
        }
    }

    private CellValue SampleValue()
    {
        var category = _choice.Category;
        if (category == NumberCategory.Text)
            return _cellValue.Kind == ValueKind.Text ? _cellValue : CellValue.Text(_texts["format.sample.text"]);
        if (_cellValue.Kind == ValueKind.Number) return _cellValue;
        if (category is NumberCategory.General or NumberCategory.Custom && _cellValue.Kind is ValueKind.Text or ValueKind.Boolean) return _cellValue;
        return CellValue.Number(_services.Designer.SampleNumber(category));
    }

    private bool UsesCellValue =>
        _cellValue.Kind == ValueKind.Number
        || (_cellValue.Kind == ValueKind.Text && _choice.Category is NumberCategory.Text or NumberCategory.General or NumberCategory.Custom);

    private Control SampleCard()
    {
        _sample.FontSize = _metrics.FontSize + 7;
        _sample.FontWeight = FontWeight.SemiBold;
        _sample.TextTrimming = TextTrimming.CharacterEllipsis;
        _sample.HorizontalAlignment = HorizontalAlignment.Right;
        AutomationProperties.SetName(_sample, _texts["format.sample"]);
        AutomationProperties.SetLiveSetting(_sample, Avalonia.Automation.AutomationLiveSetting.Polite);

        _sampleSource.FontSize = _metrics.FontSizeSmall;
        _sampleSource.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        var caption = new TextBlock { Text = _texts["format.sample"].ToUpper(CultureInfo.CurrentCulture), FontSize = _metrics.FontSizeSmall - 0.5, FontWeight = FontWeight.SemiBold, LetterSpacing = 0.6 }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(caption);
        Grid.SetColumn(_sampleSource, 1);
        header.Children.Add(_sampleSource);

        var cell = new Border
        {
            BorderThickness = new Thickness(1),
            Padding = new Thickness(_metrics.SpacingMedium, _metrics.SpacingSmall),
            MinHeight = 40,
            Child = _sample,
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xD1, 0xD5, 0xDB)),
        };

        return new Border
        {
            CornerRadius = new CornerRadius(_metrics.RadiusTight),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(_metrics.SpacingMedium),
            Child = new StackPanel { Spacing = _metrics.SpacingSmall, Children = { header, cell } },
        }.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceSunken)
         .Themed(Border.BorderBrushProperty, Palette.Keys.Border);
    }

    private Control CodeRow()
    {
        _codeMessage.FontSize = _metrics.FontSizeSmall;
        _codeMessage.TextWrapping = TextWrapping.Wrap;
        _codeMessage.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        return new StackPanel
        {
            Spacing = _metrics.SpacingSmall / 2,
            Children =
            {
                Caption(_texts["format.code"]),
                _code,
                _codeMessage,
            },
        };
    }

    private void ChangeCategory(NumberCategory category)
    {
        var designer = _services.Designer;
        _choice = _choice with { Category = category };
        if (designer.HasPresets(category))
        {
            var presets = designer.Presets(category, _catalog);
            if (!presets.Contains(_choice.Code, StringComparer.OrdinalIgnoreCase) && presets.Count > 0 && category != NumberCategory.Custom)
                _choice = _choice with { Code = presets[0] };
            if (category == NumberCategory.Custom) _choice = _choice with { Code = Code };
        }
        BuildOptions();
        Recompose();
    }

    private void Recompose()
    {
        var code = _services.Designer.Compose(_choice);
        _choice = _choice with { Code = code };
        _syncing = true;
        _code.Text = code;
        _syncing = false;
        ShowSample();
    }

    private void SyncCategory()
    {
        _syncing = true;
        _categories.SelectedIndex = IndexOf(_choice.Category);
        _syncing = false;
    }

    private int IndexOf(NumberCategory category)
    {
        for (var index = 0; index < _services.Designer.Categories.Count; index++)
            if (_services.Designer.Categories[index] == category) return index;
        return 0;
    }

    private void BuildOptions()
    {
        _options.Children.Clear();
        var designer = _services.Designer;
        var category = _choice.Category;

        _options.Children.Add(new TextBlock
        {
            Text = _texts[$"format.category.{Key(category)}.help"],
            FontSize = _metrics.FontSizeSmall + 0.5,
            TextWrapping = TextWrapping.Wrap,
        }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary));

        var row = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = _metrics.SpacingLarge, LineSpacing = _metrics.SpacingSmall };

        if (designer.HasDecimals(category))
        {
            var decimals = new MeasureField(
                new MeasureEntry(new MeasureOptions(string.Empty, 0, NumberFormatDesigner.MaximumDecimals, 1) { Integer = true },
                    _services.Units, CultureInfo.CurrentCulture),
                _choice.Decimals, _texts, _metrics, _services.Icons, _texts["format.decimals"], boxWidth: 44);
            decimals.ValueCommitted += (_, value) =>
            {
                _choice = _choice with { Decimals = (int)value };
                Recompose();
            };
            row.Children.Add(Labelled(_texts["format.decimals"], decimals));
        }

        if (designer.HasCurrency(category))
        {
            var symbols = designer.Currencies.ToList();
            if (!symbols.Contains(_choice.Currency)) symbols.Add(_choice.Currency);
            var currency = new ComboBox
            {
                ItemsSource = symbols.Select(symbol => symbol.Before ? symbol.Symbol + " 1.234" : "1.234 " + symbol.Symbol).ToList(),
                SelectedIndex = symbols.IndexOf(_choice.Currency),
                MinWidth = 110,
            };
            AutomationProperties.SetName(currency, _texts["format.currencySymbol"]);
            currency.SelectionChanged += (_, _) =>
            {
                if (currency.SelectedIndex < 0) return;
                _choice = _choice with { Currency = symbols[currency.SelectedIndex] };
                Recompose();
            };
            row.Children.Add(Labelled(_texts["format.currencySymbol"], currency));
        }

        if (designer.HasThousands(category))
        {
            var thousands = new CheckBox { Content = _texts["format.thousandsSeparator"], IsChecked = _choice.Thousands, VerticalAlignment = VerticalAlignment.Bottom };
            thousands.IsCheckedChanged += (_, _) =>
            {
                _choice = _choice with { Thousands = thousands.IsChecked == true };
                Recompose();
            };
            row.Children.Add(thousands);
        }

        if (row.Children.Count > 0) _options.Children.Add(row);

        if (designer.HasNegatives(category)) _options.Children.Add(NegativeList());

        if (designer.HasPresets(category)) _options.Children.Add(PresetList(designer.Presets(category, _catalog)));
    }

    private Control NegativeList()
    {
        var designer = _services.Designer;
        var list = new ListBox { MaxHeight = 120 };
        list.Classes.Add("compact");
        AutomationProperties.SetName(list, _texts["format.negatives"]);
        var items = new List<Control>();
        foreach (var style in designer.NegativeStyles)
        {
            var code = designer.Compose(_choice with { Negative = style });
            var sample = Render(code, -Math.Abs(_services.Designer.SampleNumber(_choice.Category)));
            items.Add(SampleLine(sample, null));
        }
        list.ItemsSource = items;
        list.SelectedIndex = designer.NegativeStyles.ToList().IndexOf(_choice.Negative);
        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedIndex < 0) return;
            _choice = _choice with { Negative = designer.NegativeStyles[list.SelectedIndex] };
            Recompose();
        };
        return new StackPanel { Spacing = _metrics.SpacingSmall / 2, Children = { Caption(_texts["format.negatives"]), list } };
    }

    private Control PresetList(IReadOnlyList<string> presets)
    {
        var list = new ListBox { MaxHeight = _choice.Category == NumberCategory.Custom ? 150 : 170 };
        list.Classes.Add("compact");
        AutomationProperties.SetName(list, _texts["format.presets"]);
        list.ItemsSource = presets.Select(code => SampleLine(Render(code), code)).ToList();
        var selected = -1;
        for (var index = 0; index < presets.Count; index++)
            if (string.Equals(presets[index], _choice.Code, StringComparison.OrdinalIgnoreCase)) selected = index;
        list.SelectedIndex = selected;
        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedIndex < 0) return;
            _choice = _choice with { Code = presets[list.SelectedIndex] };
            Recompose();
        };
        return new StackPanel { Spacing = _metrics.SpacingSmall / 2, Children = { Caption(_texts["format.presets"]), list } };
    }

    private Control SampleLine(FormattedSample sample, string? code)
    {
        var text = new TextBlock { Text = sample.Valid ? sample.Text : "—", FontSize = _metrics.FontSize, VerticalAlignment = VerticalAlignment.Center };
        if (sample.ColorName is { } colour && Color.TryParse(colour, out var parsed)) text.Foreground = new SolidColorBrush(parsed);
        if (code is null) return text;

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = _metrics.SpacingLarge };
        grid.Children.Add(text);
        var codeText = new TextBlock { Text = code, FontSize = _metrics.FontSizeSmall, FontFamily = new FontFamily("Menlo, Consolas, monospace"), VerticalAlignment = VerticalAlignment.Center }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        Grid.SetColumn(codeText, 1);
        grid.Children.Add(codeText);
        AutomationProperties.SetName(grid, (sample.Valid ? sample.Text : string.Empty) + " " + code);
        return grid;
    }

    private void ShowSample()
    {
        var sample = Render(Code);
        _sample.Text = sample.Valid ? sample.Text : string.Empty;
        _sample.Foreground = sample.ColorName is { } colour && Color.TryParse(colour, out var parsed)
            ? new SolidColorBrush(parsed)
            : new SolidColorBrush(Color.FromRgb(0x11, 0x18, 0x27));
        _sampleSource.Text = UsesCellValue ? _texts.Format("format.sample.fromCell", _cellName) : _texts["format.sample.typical"];
        _codeMessage.Text = sample.Valid ? _texts["format.code.explanation"] : _texts["format.code.invalid"];
        _codeMessage.Themed(TextBlock.ForegroundProperty, sample.Valid ? Palette.Keys.TextSecondary : Palette.Keys.Danger);
    }

    private Control Labelled(string caption, Control editor) =>
        new StackPanel { Spacing = _metrics.SpacingSmall / 2, Children = { Caption(caption), editor } };

    private TextBlock Caption(string text) =>
        new TextBlock { Text = text, FontSize = _metrics.FontSizeSmall }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

    private string CategoryName(NumberCategory category) => _texts[$"format.category.{Key(category)}"];

    private string Key(NumberCategory category) => char.ToLowerInvariant(category.ToString()[0]) + category.ToString()[1..];
}

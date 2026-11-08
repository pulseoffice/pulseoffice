using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.Dialogs;

public enum TextQualifier { Double, Single, None }

public sealed record TextToColumnsChoice(
    bool Tab, bool Semicolon, bool Comma, bool Space, string Other,
    bool MergeSeparators, TextQualifier Qualifier, bool DetectNumbers)
{
    public IReadOnlyList<char> Separators()
    {
        var separators = new List<char>();
        if (Tab) separators.Add('\t');
        if (Semicolon) separators.Add(';');
        if (Comma) separators.Add(',');
        if (Space) separators.Add(' ');
        foreach (var character in Other.Where(character => !separators.Contains(character))) separators.Add(character);
        return separators;
    }

    public TextToColumnsRequest Request(CellRange range) => new(range)
    {
        Separators = Separators(),
        MergeSeparators = MergeSeparators,
        Quote = Qualifier switch
        {
            TextQualifier.Double => '"',
            TextQualifier.Single => '\'',
            _ => null,
        },
        DetectNumbers = DetectNumbers,
    };
}

public sealed record TextToColumnsField(
    string Key, IReadOnlyList<string> Sample, CellRange Range, ITextToColumns Splitter)
    : CustomFormField(Key, string.Empty)
{
    public TextToColumnsChoice? Initial { get; init; }

    public Action<TextToColumnsEditor>? Attached { get; init; }

    public override FormFieldEditor Create(ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        var editor = new TextToColumnsEditor(this, texts, metrics);
        Attached?.Invoke(editor);
        return new FormFieldEditor(editor, () => editor.Choice, Labelled: false);
    }
}

public sealed class TextToColumnsEditor : UserControl
{
    private const int PreviewColumns = 12;

    private readonly TextToColumnsField _field;
    private readonly DesignTokens.Metrics _metrics;
    private readonly ITextCatalog _texts;
    private readonly CheckBox _tab;
    private readonly CheckBox _semicolon;
    private readonly CheckBox _comma;
    private readonly CheckBox _space;
    private readonly CheckBox _otherOn;
    private readonly TextBox _other;
    private readonly CheckBox _merge;
    private readonly ComboBox _qualifier;
    private readonly CheckBox _numbers;
    private readonly Grid _preview = new();
    private readonly TextBlock _summary;
    private IReadOnlyList<string> _sample;

    public TextToColumnsEditor(TextToColumnsField field, ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        _field = field ?? throw new ArgumentNullException(nameof(field));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _metrics = metrics;
        _sample = field.Sample;
        var initial = field.Initial ?? new TextToColumnsChoice(false, true, false, false, string.Empty, false, TextQualifier.Double, true);

        _tab = Check(texts["textToColumns.tab"], initial.Tab);
        _semicolon = Check(texts["textToColumns.semicolon"], initial.Semicolon);
        _comma = Check(texts["textToColumns.comma"], initial.Comma);
        _space = Check(texts["textToColumns.space"], initial.Space);
        _otherOn = Check(texts["textToColumns.other"], initial.Other.Length > 0);
        _other = new TextBox { Width = 64, MaxLength = 4, IsEnabled = initial.Other.Length > 0, Text = initial.Other };
        _other.TextChanged += (_, _) => Update();
        _otherOn.IsCheckedChanged += (_, _) =>
        {
            _other.IsEnabled = _otherOn.IsChecked == true;
            Update();
        };

        _merge = Check(texts["tools.mergeSeparators"], initial.MergeSeparators);
        _numbers = Check(texts["tools.detectNumbers"], initial.DetectNumbers);
        _qualifier = new ComboBox
        {
            ItemsSource = new[] { "\"", "'", texts["textToColumns.qualifier.none"] },
            SelectedIndex = (int)initial.Qualifier,
            MinWidth = 96,
        };
        _qualifier.SelectionChanged += (_, _) => Update();

        _summary = new TextBlock { FontSize = metrics.FontSizeSmall }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

        var separators = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var box in new[] { _tab, _semicolon, _comma, _space }) separators.Children.Add(box);
        separators.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = metrics.SpacingSmall,
            Children = { _otherOn, _other },
        });

        var qualifierRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = metrics.SpacingSmall,
            Children =
            {
                new TextBlock { Text = texts["textToColumns.qualifier"], VerticalAlignment = VerticalAlignment.Center, FontSize = metrics.FontSize },
                _qualifier,
            },
        };

        var options = new WrapPanel { Orientation = Orientation.Horizontal };
        options.Children.Add(_merge);
        options.Children.Add(_numbers);

        var preview = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(metrics.RadiusSoft),
            ClipToBounds = true,
            Child = new ScrollViewer
            {
                MaxHeight = 220,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Content = _preview,
            },
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border)
         .Themed(Border.BackgroundProperty, Palette.Keys.Surface);

        Content = new StackPanel
        {
            Spacing = metrics.SpacingSmall,
            Children =
            {
                Caption(texts["tools.separators"]),
                separators,
                qualifierRow,
                options,
                Caption(texts["textToColumns.preview"]),
                preview,
                _summary,
            },
        };

        Update();
    }

    public TextToColumnsChoice Choice => new(
        _tab.IsChecked == true,
        _semicolon.IsChecked == true,
        _comma.IsChecked == true,
        _space.IsChecked == true,
        _otherOn.IsChecked == true ? _other.Text ?? string.Empty : string.Empty,
        _merge.IsChecked == true,
        (TextQualifier)Math.Max(_qualifier.SelectedIndex, 0),
        _numbers.IsChecked == true);

    public void Resample(IReadOnlyList<string> sample)
    {
        _sample = sample ?? throw new ArgumentNullException(nameof(sample));
        Update();
    }

    private CheckBox Check(string label, bool value)
    {
        var box = new CheckBox
        {
            Content = label,
            IsChecked = value,
            Margin = new Thickness(0, 0, _metrics.SpacingMedium, 0),
            FontSize = _metrics.FontSize,
        };
        box.IsCheckedChanged += (_, _) => Update();
        return box;
    }

    private TextBlock Caption(string text) => new TextBlock
    {
        Text = text,
        FontSize = _metrics.FontSizeSmall,
        FontWeight = FontWeight.SemiBold,
    }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

    private void Update()
    {
        if (_preview is null || _summary is null || _qualifier is null || _numbers is null) return;

        var request = Choice.Request(_field.Range);
        var rows = _sample.Select(line => _field.Splitter.Split(line, request)).ToList();
        var widest = rows.Count == 0 ? 0 : rows.Max(row => row.Count);
        var width = Math.Min(PreviewColumns, widest);

        _preview.Children.Clear();
        _preview.ColumnDefinitions.Clear();
        _preview.RowDefinitions.Clear();

        for (var column = 0; column < width; column++) _preview.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        for (var row = 0; row <= rows.Count; row++) _preview.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (var column = 0; column < width; column++)
            Cell(CellAddress.ColumnName(_field.Range.Start.Column + column), 0, column, header: true);

        for (var row = 0; row < rows.Count; row++)
            for (var column = 0; column < Math.Min(width, rows[row].Count); column++)
                Cell(rows[row][column], row + 1, column, header: false);

        _summary.Text = rows.Count == 0 ? _texts["textToColumns.noText"] : _texts.Format("textToColumns.columns", widest);
    }

    private void Cell(string text, int row, int column, bool header)
    {
        var block = new Border
        {
            BorderThickness = new Thickness(0, 0, 1, 1),
            Padding = new Thickness(_metrics.SpacingSmall, 2),
            MinWidth = 56,
            Child = new TextBlock
            {
                Text = text,
                FontSize = _metrics.FontSizeSmall + 0.5,
                FontWeight = header ? FontWeight.SemiBold : FontWeight.Normal,
                MaxWidth = 200,
                TextTrimming = TextTrimming.CharacterEllipsis,
            },
        }.Themed(Border.BorderBrushProperty, Palette.Keys.Border);

        if (header) block.Themed(Border.BackgroundProperty, Palette.Keys.SurfaceRaised);

        Grid.SetRow(block, row);
        Grid.SetColumn(block, column);
        _preview.Children.Add(block);
    }
}

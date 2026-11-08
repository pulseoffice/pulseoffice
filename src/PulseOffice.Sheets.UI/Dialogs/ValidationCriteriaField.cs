using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Lists;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;
using Path = Avalonia.Controls.Shapes.Path;

namespace PulseOffice.Sheets.UI.Dialogs;

public sealed record ValidationCriteriaField(string Key, ValidationDraft Initial, IValidationRules Rules, IReferenceSource Ranges, IIconCatalog Icons)
    : CustomFormField(Key, string.Empty)
{
    public override FormFieldEditor Create(ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        var editor = new ValidationCriteriaEditor(this, texts, metrics);
        return new FormFieldEditor(editor, () => editor.Draft, Labelled: false);
    }
}

public sealed class ValidationCriteriaEditor : UserControl
{
    private const int DropdownPreviewItems = 6;

    private readonly ValidationCriteriaField _field;
    private readonly ITextCatalog _texts;
    private readonly DesignTokens.Metrics _metrics;
    private readonly IconChoiceGroup _kind;
    private readonly ComboBox _operator;
    private readonly DialogRow _operatorRow;
    private readonly TextBox _first;
    private readonly DialogRow _firstRow;
    private readonly TextBox _second;
    private readonly DialogRow _secondRow;
    private readonly DialogSection _listSection;
    private readonly DialogSection _optionsSection;
    private readonly IconChoiceGroup _listSource;
    private readonly EditableList _values;
    private readonly ListEditorControl _valuesEditor;
    private readonly RangeFieldControl _range;
    private readonly CheckBox _blank;
    private readonly CheckBox _dropdown;
    private readonly TextBlock _hint;
    private readonly StackPanel _dropdownItems = new();
    private readonly StackPanel _dropdownPreview = new();
    private readonly TextBlock _dropdownMore;

    public ValidationCriteriaEditor(ValidationCriteriaField field, ITextCatalog texts, DesignTokens.Metrics metrics)
    {
        _field = field ?? throw new ArgumentNullException(nameof(field));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _metrics = metrics;
        var initial = field.Initial;
        var rules = field.Rules;

        _kind = new IconChoiceGroup(
            [.. rules.Kinds.Select(kind => new IconChoice(Camel(kind.ToString()), "validation.kind." + Camel(kind.ToString()), texts["tools.validation." + Camel(kind.ToString())]))],
            field.Icons, metrics, texts["tools.validationKind"], Math.Max(0, IndexOf(rules.Kinds, initial.Kind)), showLabels: true, columns: 2);
        _operator = new ComboBox
        {
            ItemsSource = rules.Operators.Select(op => texts["tools.operator." + Camel(op.ToString())]).ToList(),
            SelectedIndex = Math.Max(0, IndexOf(rules.Operators, initial.Operator)),
            MinWidth = 180,
        };

        _first = new TextBox { Text = initial.First, MinWidth = 200 };
        _second = new TextBox { Text = initial.Second, MinWidth = 200 };

        _listSource = new IconChoiceGroup(
            [
                new IconChoice("values", "validation.kind.list", texts["validation.listValues"]),
                new IconChoice("range", "names.scope.sheet", texts["validation.listRange"]),
            ],
            field.Icons, metrics, texts["validation.listSource"], initial.ListFromRange ? 1 : 0, showLabels: true);
        _values = new EditableList(initial.ListValues, unique: true);
        _valuesEditor = new ListEditorControl(_values, ListEditMode.Editable, texts, metrics, texts["validation.listPlaceholder"]);
        _range = new RangeFieldControl(new RangeField("listRange", texts["validation.listRange"], initial.ListRange, field.Ranges), texts, metrics);

        _blank = new CheckBox { Content = texts["tools.allowBlank"], IsChecked = initial.AllowBlank };
        _dropdown = new CheckBox { Content = texts["validation.showDropdown"], IsChecked = initial.ShowDropdown };

        _hint = new TextBlock { FontSize = metrics.FontSizeSmall, TextWrapping = TextWrapping.Wrap }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);
        _dropdownMore = new TextBlock { FontSize = metrics.FontSizeSmall, Foreground = Ink(0x6B, 0x72, 0x80), Margin = new Thickness(6, 2) };

        var layout = new TwoPaneDialogLayout(metrics) { Preview = BuildPreview() };
        layout.AddSection(texts["tools.validationKind"]).Add(null, _kind, DialogRowKind.Wide);
        var data = layout.AddSection(texts["validation.section.data"]);
        _operatorRow = data.Add(texts["tools.validationOperator"], _operator);
        _firstRow = data.Add(string.Empty, _first);
        _secondRow = data.Add(string.Empty, _second);
        _listSection = layout.AddSection(texts["validation.listSource"]);
        _listSection.Add(null, _listSource, DialogRowKind.Wide);
        _listSection.Add(null, _valuesEditor, DialogRowKind.Wide);
        _listSection.Add(null, _range, DialogRowKind.Wide);
        _optionsSection = layout.AddSection(texts["form.section.options"]);
        _optionsSection.Add(null, _blank, DialogRowKind.Indented);
        _optionsSection.Add(null, _dropdown, DialogRowKind.Indented);
        Layout = layout;

        _kind.SelectionChanged += (_, _) => Arrange();
        _operator.SelectionChanged += (_, _) => Arrange();
        _listSource.SelectionChanged += (_, _) => Arrange();
        _values.Changed += (_, _) => ShowDropdown();
        _dropdown.IsCheckedChanged += (_, _) => ShowDropdown();

        Content = layout;
        Arrange();
    }

    public TwoPaneDialogLayout Layout { get; }

    public IconChoiceGroup Kind => _kind;

    public EditableList Values => _values;

    public IReadOnlyList<string> DropdownShown => [.. _dropdownItems.Children.OfType<TextBlock>().Select(item => item.Text ?? string.Empty)];

    public string Hint => _hint.Text ?? string.Empty;

    public bool DropdownVisible => _dropdownPreview.IsVisible;

    public ValidationDraft Draft => _field.Initial with
    {
        Kind = _field.Rules.Kinds[Math.Max(_kind.SelectedIndex, 0)],
        Operator = _field.Rules.Operators[Math.Max(_operator.SelectedIndex, 0)],
        First = _first.Text ?? string.Empty,
        Second = _second.Text ?? string.Empty,
        ListValues = _values.Items,
        ListFromRange = _listSource.SelectedIndex == 1,
        ListRange = _range.Text,
        AllowBlank = _blank.IsChecked == true,
        ShowDropdown = _dropdown.IsChecked == true,
    };

    private Control BuildPreview()
    {
        var cell = new Border
        {
            Width = 150,
            Height = 22,
            Background = Brushes.White,
            BorderBrush = Ink(0x16, 0xA3, 0x4A),
            BorderThickness = new Thickness(2),
            Child = new Grid
            {
                Children =
                {
                    new Border
                    {
                        Width = 18,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Background = Ink(0xF3, 0xF4, 0xF6),
                        BorderBrush = Ink(0xD1, 0xD5, 0xDB),
                        BorderThickness = new Thickness(1, 0, 0, 0),
                        Child = new Path
                        {
                            Data = Geometry.Parse("M0 0h7l-3.5 4z"),
                            Fill = Ink(0x37, 0x41, 0x51),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                        },
                    },
                },
            },
        };

        var list = new Border
        {
            Width = 150,
            Background = Brushes.White,
            BorderBrush = Ink(0xD1, 0xD5, 0xDB),
            BorderThickness = new Thickness(1),
            BoxShadow = BoxShadows.Parse("0 2 6 0 #33000000"),
            Child = new StackPanel { Children = { _dropdownItems, _dropdownMore } },
        };

        _hint.MaxWidth = 170;
        _dropdownPreview.Children.Add(cell);
        _dropdownPreview.Children.Add(list);
        return new StackPanel
        {
            Width = 180,
            Spacing = _metrics.SpacingMedium,
            Children =
            {
                new TextBlock { Text = _texts["validation.preview"], FontSize = _metrics.FontSizeSmall, FontWeight = FontWeight.SemiBold }
                    .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
                _dropdownPreview,
                _hint,
            },
        };
    }

    private void Arrange()
    {
        var draft = Draft;
        var layout = _field.Rules.Layout(draft.Kind, draft.Operator);

        _operatorRow.IsVisible = layout.Operator;
        _firstRow.IsVisible = layout.FirstLabelKey is not null;
        _secondRow.IsVisible = layout.SecondLabelKey is not null;
        if (layout.FirstLabelKey is { } first) _firstRow.Label!.Text = _texts[first];
        if (layout.SecondLabelKey is { } second) _secondRow.Label!.Text = _texts[second];
        _first.PlaceholderText = layout.Formula ? "A1>0" : string.Empty;
        Layout.Sections[1].IsVisible = layout.Operator || layout.FirstLabelKey is not null;

        _listSection.IsVisible = layout.List;
        _valuesEditor.IsVisible = layout.List && !draft.ListFromRange;
        _range.IsVisible = layout.List && draft.ListFromRange;
        _dropdown.IsVisible = layout.Dropdown;
        _blank.IsVisible = draft.Kind != ValidationKind.Any;
        _optionsSection.IsVisible = _blank.IsVisible || _dropdown.IsVisible;

        _hint.Text = _texts["validation.hint." + Camel(draft.Kind.ToString())];
        ShowDropdown();
    }

    private void ShowDropdown()
    {
        var draft = Draft;
        var shown = draft.Kind == ValidationKind.List && draft.ShowDropdown;
        _dropdownPreview.IsVisible = shown;

        _dropdownItems.Children.Clear();
        var items = draft.ListFromRange
            ? [_texts.Format("validation.preview.fromRange", draft.ListRange.Length == 0 ? "…" : draft.ListRange)]
            : draft.ListValues.Take(DropdownPreviewItems).ToList();
        foreach (var item in items)
            _dropdownItems.Children.Add(new TextBlock
            {
                Text = item,
                FontSize = _metrics.FontSizeSmall,
                Foreground = Ink(0x1F, 0x29, 0x37),
                Margin = new Thickness(6, 2),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        var hidden = draft.ListFromRange ? 0 : draft.ListValues.Count - DropdownPreviewItems;
        _dropdownMore.Text = hidden > 0 ? _texts.Format("validation.preview.more", hidden) : string.Empty;
        _dropdownMore.IsVisible = hidden > 0;
        if (items.Count == 0)
            _dropdownItems.Children.Add(new TextBlock
            {
                Text = _texts["validation.preview.empty"],
                FontSize = _metrics.FontSizeSmall,
                FontStyle = FontStyle.Italic,
                Foreground = Ink(0x6B, 0x72, 0x80),
                Margin = new Thickness(6, 2),
            });
    }

    private IBrush Ink(byte red, byte green, byte blue) => new SolidColorBrush(Color.FromRgb(red, green, blue));

    private int IndexOf<T>(IReadOnlyList<T> items, T value) => items.ToList().IndexOf(value);

    private string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}

using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Choices;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Fields;
using PulseOffice.UI.Controls.Galleries;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.Conditional;

public sealed record ConditionalRuleDraft(ConditionalRule Rule, string RangeText, IReadOnlyList<string> TypedOperands, string TypedFormula);

public sealed class ConditionalEditorServices(
    IConditionalRuleCatalog catalog, ConditionalPainting painting, IGalleryNavigator navigator, IMeasureUnits units, IIconCatalog icons)
{
    public IConditionalRuleCatalog Catalog { get; } = catalog ?? throw new ArgumentNullException(nameof(catalog));

    public ConditionalPainting Painting { get; } = painting ?? throw new ArgumentNullException(nameof(painting));

    public IGalleryNavigator Navigator { get; } = navigator ?? throw new ArgumentNullException(nameof(navigator));

    public IMeasureUnits Units { get; } = units ?? throw new ArgumentNullException(nameof(units));

    public IIconCatalog Icons { get; } = icons ?? throw new ArgumentNullException(nameof(icons));
}

public sealed class ConditionalRuleEditorView : UserControl, IDialogContent, IDialogFooterOwner
{
    private readonly ConditionalEditorServices _services;
    private readonly ITextCatalog _texts;
    private readonly DesignTokens.Metrics _metrics = DesignTokens.For(InterfaceDensity.Compact);
    private readonly ThumbnailGallery<ConditionalRuleKind> _kinds;
    private readonly StackPanel _parameters = new();
    private readonly StackPanel _appearance = new();
    private readonly ConditionalRulePreview _preview;
    private readonly RangeFieldControl _range;
    private readonly TextBlock _message = new();
    private readonly DialogFooter _footer;
    private readonly Dictionary<ConditionalRuleKind, ConditionalRule> _byKind = [];
    private ConditionalRule _rule;
    private ConditionalRuleKind _kind;
    private string[] _operands;
    private string _formula;

    public ConditionalRuleEditorView(ConditionalRule initial, string displayFormula, IReferenceSource ranges, ConditionalEditorServices services, ITextCatalog texts)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
        _rule = initial ?? throw new ArgumentNullException(nameof(initial));
        _kind = services.Catalog.KindOf(initial);
        _operands = [.. initial.Operands.Select(operand => DisplayOperand(_kind, operand))];
        _formula = displayFormula ?? string.Empty;
        _byKind[_kind] = initial;

        _kinds = new ThumbnailGallery<ConditionalRuleKind>(
            [.. services.Catalog.Kinds.Select(kind => new ThumbnailGalleryItem<ConditionalRuleKind>(
                kind, texts[$"conditional.kind.{Camel(kind.ToString())}"],
                texts[services.Catalog.IsStyled(kind) ? "conditional.group.highlight" : "conditional.group.visual"]))],
            new ConditionalKindPainter(services.Painting, services.Catalog),
            services.Navigator,
            new ThumbnailGalleryOptions
            {
                TileWidth = 96,
                ThumbnailHeight = 52,
                FrameRadius = _metrics.RadiusTight,
                Columns = 6,
                AccessibleName = texts["conditional.kind"],
            },
            _metrics);
        _kinds.Select(_kind);
        _kinds.SelectionChanged += (_, kind) => SwitchKind(kind);

        _preview = new ConditionalRulePreview(services.Painting, texts["conditional.sample"]) { Width = 140, Height = 96 };
        AutomationProperties.SetName(_preview, texts["format.preview"]);
        _preview.Rule = _rule;

        _range = new RangeFieldControl(new RangeField("range", texts["conditional.appliesTo"], initial.RangeText, ranges), texts, _metrics);

        _parameters.Spacing = _metrics.SpacingSmall;
        _appearance.Spacing = _metrics.SpacingSmall;

        _message.FontSize = _metrics.FontSizeSmall;
        _message.TextWrapping = TextWrapping.Wrap;
        _message.IsVisible = false;
        _message.Themed(TextBlock.ForegroundProperty, Palette.Keys.Danger);

        var layout = new TwoPaneDialogLayout(_metrics) { Preview = _preview };
        var type = layout.AddSection(texts["conditional.kind"]);
        type.Add(null, _kinds, DialogRowKind.Wide, editorWidth: 600);
        var condition = layout.AddSection(texts["conditional.section.condition"]);
        condition.Add(texts["conditional.appliesTo"], _range, editorWidth: 260);
        condition.Add(null, _parameters, DialogRowKind.Wide, editorWidth: 460);
        var look = layout.AddSection(texts["conditional.section.format"]);
        look.Add(null, _appearance, DialogRowKind.Wide, editorWidth: 460);
        look.Add(null, _message, DialogRowKind.Wide, editorWidth: 460);

        _footer = new DialogFooter()
            .WithCancel(texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(texts["prompt.confirm"], Confirm, primary: true);

        Build();
        Content = new ScrollViewer { Content = layout, MaxHeight = 640, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    }

    public event EventHandler<object?>? Completed;

    public Control? Footer => _footer;

    public ConditionalRule Rule => _rule;

    public ConditionalRuleKind Kind => _kind;

    public ThumbnailGallery<ConditionalRuleKind> Kinds => _kinds;

    public ConditionalRulePreview Preview => _preview;

    public RangeFieldControl Range => _range;

    public ConditionalRuleDraft Draft => new(_rule, _range.Text.Trim(), _operands, _formula);

    public void SwitchKind(ConditionalRuleKind kind)
    {
        _byKind[_kind] = _rule;
        _kind = kind;
        var style = _services.Catalog.IsStyled(kind) ? _rule.Style : null;
        _rule = _byKind.TryGetValue(kind, out var known) ? known : _services.Catalog.Default(kind, _rule.Range) with { Priority = _rule.Priority };
        if (style is not null && _services.Catalog.IsStyled(kind)) _rule = _rule with { Style = style };
        _operands = [.. _rule.Operands.Select(operand => DisplayOperand(kind, operand))];
        if (kind != ConditionalRuleKind.Formula) _formula = string.Empty;
        _kinds.Select(kind);
        Build();
        Update(_rule);
    }

    private void Confirm()
    {
        if (_range.Text.Trim().Length == 0)
        {
            Show(_texts["conditional.error.range"]);
            return;
        }
        if (_kind == ConditionalRuleKind.Formula && _formula.Trim().Length == 0)
        {
            Show(_texts["conditional.error.formula"]);
            return;
        }
        if (NeedsValues() is var needed && needed > 0 && _operands.Take(needed).Any(operand => operand.Trim().Length == 0))
        {
            Show(_texts["conditional.error.value"]);
            return;
        }
        Completed?.Invoke(this, Draft);
    }

    private int NeedsValues() => _kind switch
    {
        ConditionalRuleKind.CellValue => _rule.Operator is ConditionOperator.Between or ConditionOperator.NotBetween ? 2 : 1,
        ConditionalRuleKind.Text or ConditionalRuleKind.TopBottom => 1,
        _ => 0,
    };

    private void Show(string message)
    {
        _message.Text = message;
        _message.IsVisible = true;
    }

    private void Update(ConditionalRule rule)
    {
        _rule = rule;
        _preview.Rule = rule;
        _message.IsVisible = false;
    }

    private void Build()
    {
        _parameters.Children.Clear();
        _appearance.Children.Clear();
        var catalog = _services.Catalog;

        var operators = catalog.Operators(_kind);
        if (operators.Count > 1)
        {
            var box = new ComboBox
            {
                ItemsSource = operators.Select(op => _texts["condition.operator." + Camel(op.ToString())]).ToList(),
                SelectedIndex = Math.Max(0, operators.ToList().IndexOf(_rule.Operator)),
                MinWidth = 220,
            };
            AutomationProperties.SetName(box, _texts["condition.operator"]);
            box.SelectionChanged += (_, _) =>
            {
                if (box.SelectedIndex < 0) return;
                Update(_rule with { Operator = operators[box.SelectedIndex] });
                if (_kind == ConditionalRuleKind.CellValue) Build();
            };
            _parameters.Children.Add(box);
        }

        switch (_kind)
        {
            case ConditionalRuleKind.CellValue:
                _parameters.Children.Add(Values(NeedsValues()));
                break;
            case ConditionalRuleKind.Text:
                _parameters.Children.Add(Values(1));
                break;
            case ConditionalRuleKind.TopBottom:
                var count = new MeasureField(
                    new MeasureEntry(new MeasureOptions(string.Empty, 1, 1000, 1) { Integer = true }, _services.Units, CultureInfo.CurrentCulture),
                    double.TryParse(_operands.FirstOrDefault(), NumberStyles.Float, CultureInfo.CurrentCulture, out var n) ? n : 10,
                    _texts, _metrics, _services.Icons, _texts["condition.count"], boxWidth: 56);
                Operand(0, count.Value.ToString(CultureInfo.CurrentCulture));
                count.ValueCommitted += (_, value) => Operand(0, value.ToString(CultureInfo.CurrentCulture));
                _parameters.Children.Add(Labelled(_texts["condition.count"], count));
                break;
            case ConditionalRuleKind.Average:
                var deviations = new ComboBox
                {
                    ItemsSource = Enumerable.Range(0, 4).Select(level => level == 0 ? _texts["condition.deviations.none"] : _texts.Format("condition.deviations.count", level)).ToList(),
                    SelectedIndex = Math.Clamp(_rule.StandardDeviations, 0, 3),
                    MinWidth = 220,
                };
                AutomationProperties.SetName(deviations, _texts["condition.deviations"]);
                deviations.SelectionChanged += (_, _) =>
                {
                    if (deviations.SelectedIndex >= 0) Update(_rule with { StandardDeviations = deviations.SelectedIndex });
                };
                _parameters.Children.Add(Labelled(_texts["condition.deviations"], deviations));
                break;
            case ConditionalRuleKind.Date:
                var periods = Enum.GetValues<DatePeriod>();
                var period = new ComboBox
                {
                    ItemsSource = periods.Select(item => _texts["condition.period." + Camel(item.ToString())]).ToList(),
                    SelectedIndex = Array.IndexOf(periods, _rule.DatePeriod),
                    MinWidth = 220,
                };
                AutomationProperties.SetName(period, _texts["condition.period"]);
                period.SelectionChanged += (_, _) =>
                {
                    if (period.SelectedIndex >= 0) Update(_rule with { DatePeriod = periods[period.SelectedIndex] });
                };
                _parameters.Children.Add(period);
                break;
            case ConditionalRuleKind.Formula:
                var formula = new TextBox { Text = _formula, PlaceholderText = _texts["conditional.formula.placeholder"], FontFamily = new FontFamily("Menlo, Consolas, monospace") };
                AutomationProperties.SetName(formula, _texts["condition.formula"]);
                formula.TextChanged += (_, _) => _formula = formula.Text ?? string.Empty;
                _parameters.Children.Add(Labelled(_texts["condition.formula"], formula));
                break;
        }

        if (_parameters.Children.Count == 0)
            _parameters.Children.Add(Note(_texts[$"conditional.kind.{Camel(_kind.ToString())}.help"]));

        if (catalog.IsStyled(_kind)) BuildStyle();
        else if (_kind == ConditionalRuleKind.DataBar) BuildBars();
        else if (_kind == ConditionalRuleKind.ColorScale) BuildScale();
        else BuildIcons();
    }

    private Control Values(int count)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = _metrics.SpacingSmall };
        for (var index = 0; index < count; index++)
        {
            var position = index;
            if (index > 0) row.Children.Add(new TextBlock { Text = _texts["conditional.and"], VerticalAlignment = VerticalAlignment.Center });
            var box = new TextBox { Text = position < _operands.Length ? _operands[position] : string.Empty, MinWidth = 120 };
            AutomationProperties.SetName(box, _texts[position == 0 ? "condition.first" : "condition.second"]);
            box.TextChanged += (_, _) => Operand(position, box.Text ?? string.Empty);
            row.Children.Add(box);
        }
        return row;
    }

    private void Operand(int position, string value)
    {
        if (_operands.Length <= position) Array.Resize(ref _operands, position + 1);
        _operands[position] = value;
    }

    private void BuildStyle()
    {
        var catalog = _services.Catalog;
        var swatches = catalog.StylePresets.Select(preset => new Swatch(preset.Key, _texts[$"conditional.style.{preset.Key}"], preset.BackgroundHex ?? "#FFFFFF")
        {
            Foreground = preset.FontHex,
            Sample = "AaBb",
        }).ToList();
        var current = catalog.PresetOf(_rule.Style);
        var strip = new SwatchStrip(swatches, _texts, _metrics, _texts["conditional.style"], current?.Key, allowCustom: false);
        _appearance.Children.Add(strip);

        var background = new ColorFieldControl(_rule.Style.BackgroundHex ?? string.Empty, true, _texts, _metrics);
        var font = new ColorFieldControl(_rule.Style.Font.ColorHex, false, _texts, _metrics);
        var bold = new CheckBox { Content = _texts["condition.bold"], IsChecked = _rule.Style.Font.Bold };
        var custom = new StackPanel
        {
            Spacing = _metrics.SpacingSmall,
            IsVisible = current is null,
            Children = { Labelled(_texts["condition.background"], background), Labelled(_texts["condition.fontColor"], font), bold },
        };
        var toggle = new Button { Content = _texts["conditional.style.custom"], HorizontalAlignment = HorizontalAlignment.Left };
        toggle.Classes.Add("dialogQuiet");
        toggle.Click += (_, _) => custom.IsVisible = !custom.IsVisible;

        void Custom()
        {
            var style = new CellStyle
            {
                BackgroundHex = background.Value is { Length: > 0 } fill ? fill : null,
                Font = CellFont.Default with { ColorHex = font.Value is { Length: > 0 } ink ? ink : CellFont.Default.ColorHex, Bold = bold.IsChecked == true },
            };
            Update(_rule with { Style = style });
        }

        strip.Changed += (_, _) =>
        {
            if (strip.SelectedKey is not { } key || catalog.StylePresets.FirstOrDefault(preset => preset.Key == key) is not { } preset) return;
            var style = catalog.StyleOf(preset);
            Update(_rule with { Style = style });
        };
        background.Changed += (_, _) => Custom();
        font.Changed += (_, _) => Custom();
        bold.IsCheckedChanged += (_, _) => Custom();

        _appearance.Children.Add(toggle);
        _appearance.Children.Add(custom);

        var stop = new CheckBox { Content = _texts["condition.stopIfTrue"], IsChecked = _rule.StopIfTrue };
        stop.IsCheckedChanged += (_, _) => Update(_rule with { StopIfTrue = stop.IsChecked == true });
        _appearance.Children.Add(stop);
    }

    private void BuildBars()
    {
        var catalog = _services.Catalog;
        var swatches = catalog.BarColors.Select(hex => new Swatch(hex, hex, hex)).ToList();
        var strip = new SwatchStrip(swatches, _texts, _metrics, _texts["conditional.bar.colour"], _rule.BarColorHex);
        strip.Changed += (_, _) =>
        {
            if (strip.Value is { } colour) Update(_rule with { BarColorHex = colour });
        };
        _appearance.Children.Add(Labelled(_texts["conditional.bar.colour"], strip));

        var fill = new IconChoiceGroup(
        [
            new IconChoice("gradient", string.Empty, _texts["conditional.bar.gradient"]),
            new IconChoice("solid", string.Empty, _texts["conditional.bar.solid"]),
        ], null, _metrics, _texts["conditional.bar.fill"], _rule.Bar.Gradient ? 0 : 1, showLabels: true);
        fill.SelectionChanged += (_, index) => Update(_rule with { Bar = _rule.Bar with { Gradient = index == 0 } });
        _appearance.Children.Add(Labelled(_texts["conditional.bar.fill"], fill));
        _appearance.Children.Add(ShowValue());
    }

    private void BuildScale()
    {
        var presets = _services.Catalog.ScalePresets;
        var gallery = new ThumbnailGallery<ColorScalePreset>(
            [.. presets.Select(preset => new ThumbnailGalleryItem<ColorScalePreset>(preset, _texts[$"conditional.scale.{preset.Key}"]))],
            new ColorScalePainter(_services.Painting),
            _services.Navigator,
            new ThumbnailGalleryOptions { TileWidth = 92, ThumbnailHeight = 54, Columns = 4, FrameRadius = _metrics.RadiusTight, AccessibleName = _texts["conditional.kind.colorScale"] },
            _metrics);
        var current = presets.FirstOrDefault(preset => preset.Colors.SequenceEqual(_rule.ScaleColors, StringComparer.OrdinalIgnoreCase));
        if (current is not null) gallery.Select(current);
        gallery.SelectionChanged += (_, preset) => Update(_rule with { ScaleColors = preset.Colors, Thresholds = [] });
        _appearance.Children.Add(gallery);
    }

    private void BuildIcons()
    {
        var names = _services.Painting.Icons.Names.Where(name => name != ConditionalIconChoice.NoIcons).ToList();
        var gallery = new ThumbnailGallery<string>(
            [.. names.Select(name => new ThumbnailGalleryItem<string>(name, _texts.Contains($"conditional.iconSet.{name}") ? _texts[$"conditional.iconSet.{name}"] : name))],
            new IconSetPainter(_services.Painting),
            _services.Navigator,
            new ThumbnailGalleryOptions { TileWidth = 104, ThumbnailHeight = 26, Columns = 5, FrameRadius = _metrics.RadiusTight, Scrolls = true, MaxViewportHeight = 200, AccessibleName = _texts["conditional.kind.iconSet"] },
            _metrics);
        if (names.Contains(_rule.IconSetName)) gallery.Select(_rule.IconSetName);
        gallery.SelectionChanged += (_, name) => Update(_rule with { IconSetName = name, CustomIcons = [], Thresholds = [] });
        _appearance.Children.Add(gallery);

        var reverse = new CheckBox { Content = _texts["conditional.icons.reverse"], IsChecked = _rule.ReverseIcons };
        reverse.IsCheckedChanged += (_, _) => Update(_rule with { ReverseIcons = reverse.IsChecked == true });
        _appearance.Children.Add(reverse);
        _appearance.Children.Add(ShowValue());
    }

    private CheckBox ShowValue()
    {
        var box = new CheckBox { Content = _texts["conditional.showValue"], IsChecked = _rule.ShowValue };
        box.IsCheckedChanged += (_, _) => Update(_rule with { ShowValue = box.IsChecked == true });
        return box;
    }

    private Control Labelled(string caption, Control editor) =>
        new StackPanel
        {
            Spacing = _metrics.SpacingSmall / 2,
            Children =
            {
                new TextBlock { Text = caption, FontSize = _metrics.FontSizeSmall }.Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary),
                editor,
            },
        };

    private TextBlock Note(string text) =>
        new TextBlock { Text = text, FontSize = _metrics.FontSizeSmall + 0.5, TextWrapping = TextWrapping.Wrap }
            .Themed(TextBlock.ForegroundProperty, Palette.Keys.TextSecondary);

    private string DisplayOperand(ConditionalRuleKind kind, string operand) =>
        kind != ConditionalRuleKind.Text && double.TryParse(operand, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number.ToString(CultureInfo.CurrentCulture)
            : operand;

    private string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}

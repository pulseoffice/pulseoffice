using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Dialogs;
using PulseOffice.UI.Controls.Fields;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;

namespace PulseOffice.UI.Controls;

public abstract record FormField(string Key, string Label)
{
    public Func<IReadOnlyDictionary<string, object>, bool>? ShownWhen { get; init; }

    public string? Section { get; init; }
}

public sealed record FormSection(string? Title, IReadOnlyList<FormField> Fields)
{
    public IEnumerable<FormField> Declared => Fields.Select(declared => declared with { Section = Title ?? string.Empty });
}

public sealed record TextField(string Key, string Label, string Initial = "", string? Placeholder = null)
    : FormField(Key, Label)
{
    public bool Multiline { get; init; }

    public bool Secret { get; init; }
}

public sealed record NumberField(
    string Key, string Label, double Initial = 0, double Minimum = double.MinValue,
    double Maximum = double.MaxValue, double Step = 1) : FormField(Key, Label)
{
    public string? Unit { get; init; }

    public bool Integer { get; init; }

    public double? LargeStep { get; init; }

    public IReadOnlyList<double> Presets { get; init; } = [];

    public bool ShowSlider { get; init; }
}

public sealed record FormServices(IMeasureUnits Units, IMeasureLabelReader Labels, IHelpTextSplitter Help, IIconCatalog? Icons = null)
{
    public IAngleDialGeometry Angles { get; init; } = new AngleDialGeometry();

    public FormServices()
        : this(new MeasureUnits(), new HelpTextSplitter())
    {
    }

    public FormServices(IMeasureUnits units, IHelpTextSplitter help)
        : this(units, new MeasureLabelReader(units), help)
    {
    }
}

public sealed record ChoiceField(
    string Key, string Label, IReadOnlyList<string> Options, int Initial = 0) : FormField(Key, Label);

public sealed record FontField(string Key, string Label, string Initial, PulseOffice.UI.Controls.Fonts.IFontPickerFactory Fonts)
    : FormField(Key, Label);

public sealed record PathField(string Key, string Label, string Initial, Func<CancellationToken, Task<string?>> Browse, string? Placeholder = null)
    : FormField(Key, Label);

public sealed record FormFieldEditor(Control Editor, Func<object> Read, bool Labelled = true)
{
    public Action<Action>? Observe { get; init; }
}

public abstract record CustomFormField(string Key, string Label) : FormField(Key, Label)
{
    public abstract FormFieldEditor Create(ITextCatalog texts, DesignTokens.Metrics metrics);
}

public sealed record CheckField(string Key, string Label, bool Initial = false) : FormField(Key, Label);

public sealed class FormPromptView : UserControl, IDialogContent, IDialogFooterOwner, IDialogSizeHint
{
    private readonly List<(FormField Field, Func<object> Read)> _fields = [];
    private readonly List<(Control Row, Func<IReadOnlyDictionary<string, object>, bool> Shown)> _conditional = [];
    private readonly TwoPaneDialogLayout _layout;
    private readonly ITextCatalog _texts;
    private readonly FormServices _services;
    private readonly DialogFooter _footer;
    private bool _sizedByCaller;
    private readonly Button _confirm;

    public event EventHandler<object?>? Completed;

    public event EventHandler? ValuesChanged;

    public FormPromptView(
        string explanation, ITextCatalog texts, IReadOnlyList<FormSection> sections,
        InterfaceDensity density = InterfaceDensity.Compact)
        : this(explanation, [.. (sections ?? throw new ArgumentNullException(nameof(sections))).SelectMany(section => section.Declared)], texts, density)
    {
    }

    public FormPromptView(
        string explanation, IReadOnlyList<FormField> fields, ITextCatalog texts,
        InterfaceDensity density = InterfaceDensity.Compact, FormServices? services = null)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(texts);
        _texts = texts;
        _services = services ?? new FormServices();

        var metrics = DesignTokens.For(density);
        _layout = new TwoPaneDialogLayout(metrics) { Explanation = explanation };
        if (explanation.Length > 0)
        {
            _layout.Header = new HelpTip(explanation, texts, metrics, _services.Help);
        }
        AutomationProperties.SetName(_layout.PreviewHost, texts["form.preview"]);

        foreach (var group in new FormSectionGrouping(texts["form.section.options"]).Group(fields))
        {
            var section = _layout.AddSection(group.Title);
            if (group.Fields.All(field => field is CheckField)) section.RowSpacing = metrics.SpacingSmall;

            foreach (var field in group.Fields)
            {
                var row = section.Add(Build(field, metrics));
                if (field.ShownWhen is { } shown) _conditional.Add((row, shown));
            }
        }

        RefreshVisibility();

        _footer = new DialogFooter(density)
            .WithCancel(texts["prompt.cancel"], () => Completed?.Invoke(this, null))
            .WithAction(texts["prompt.confirm"], () => Completed?.Invoke(this, Collect()), out _confirm, primary: true);

        Content = _layout;
    }

    public string ConfirmLabel
    {
        get => _confirm.Content as string ?? string.Empty;
        set => _confirm.Content = value;
    }

    public bool ShowButtons { get; set; } = true;

    public Control? Footer => ShowButtons ? _footer : null;

    public Control? Preview
    {
        get => _layout.Preview;
        set => _layout.Preview = value;
    }

    public TwoPaneDialogLayout Layout => _layout;

    public IReadOnlyDictionary<string, object> Values => Collect();

    public double? PreferredWidth => _sizedByCaller && Preview is null ? null : _layout.SuggestedWidth;

    public double SuggestedWidth => _layout.SuggestedWidth;

    private Dictionary<string, object> Collect() =>
        _fields.ToDictionary(entry => entry.Field.Key, entry => entry.Read());

    private void Touched()
    {
        RefreshVisibility();
        ValuesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshVisibility()
    {
        if (_conditional.Count == 0) return;

        var values = Collect();
        foreach (var (row, shown) in _conditional) row.IsVisible = shown(values);
    }

    private DialogRow Build(FormField field, DesignTokens.Metrics metrics)
    {
        switch (field)
        {
            case TextField text:
                var box = new TextBox
                {
                    Text = text.Initial,
                    PlaceholderText = text.Placeholder ?? string.Empty,
                    AcceptsReturn = text.Multiline,
                    TextWrapping = text.Multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
                    PasswordChar = text.Secret ? '\u2022' : default,
                    RevealPassword = false,
                };

                if (text.Multiline) box.MinHeight = 96;
                box.TextChanged += (_, _) => Touched();

                _fields.Add((field, () => box.Text ?? string.Empty));
                return new DialogRow(field.Label, box, metrics, editorWidth: text.Multiline ? 320 : 240, alignTop: text.Multiline);

            case PathField path:
                var pathBox = new TextBox
                {
                    Text = path.Initial,
                    PlaceholderText = path.Placeholder ?? string.Empty,
                };
                var browse = new Button { Content = _texts["form.browse"], VerticalAlignment = VerticalAlignment.Stretch };
                browse.Click += async (_, _) =>
                {
                    browse.IsEnabled = false;
                    try
                    {
                        if (await path.Browse(CancellationToken.None).ConfigureAwait(true) is { Length: > 0 } chosen)
                            pathBox.Text = chosen;
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                        or InvalidOperationException or NotSupportedException or OperationCanceledException)
                    {
                    }
                    finally
                    {
                        browse.IsEnabled = true;
                    }
                };
                AutomationProperties.SetName(pathBox, field.Label);
                var pathRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = metrics.SpacingSmall };
                Grid.SetColumn(browse, 1);
                pathRow.Children.Add(pathBox);
                pathRow.Children.Add(browse);

                pathBox.TextChanged += (_, _) => Touched();
                _fields.Add((field, () => pathBox.Text ?? string.Empty));
                return new DialogRow(field.Label, pathRow, metrics, editorWidth: 340);

            case NumberField number when MeasureUnitOf(number) is { } measured:
                var measure = new MeasureField(
                    new MeasureEntry(new MeasureOptions(measured.Symbol, number.Minimum, number.Maximum, number.Step)
                    {
                        Integer = number.Integer,
                        LargeStep = number.LargeStep,
                        Presets = number.Presets,
                        ShowSlider = number.ShowSlider,
                    }, _services.Units, System.Globalization.CultureInfo.CurrentCulture),
                    number.Initial, _texts, metrics, _services.Icons, measured.Label, boxWidth: 80)
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                };
                Control measureEditor = measure;
                if (measured.Symbol == "°" && number.Minimum >= -360 && number.Maximum <= 360 && number.Maximum > number.Minimum)
                    measureEditor = new AngleField(measure, _services.Angles, metrics.SpacingMedium, measured.Label) { HorizontalAlignment = HorizontalAlignment.Left };
                var measureRow = new DialogRow(measured.Label, measureEditor, metrics, editorWidth: Natural(measureEditor, 320));
                if (measureRow.Label is { } scrubbed) measure.AttachScrubber(scrubbed);
                measure.ValueCommitted += (_, _) => Touched();
                _fields.Add((field, () => measure.Read()));
                return measureRow;

            case NumberField number:
                var numberBox = new NumberBox(
                    new NumberEntry(new NumericBounds(number.Minimum, number.Maximum, number.Step), number.Integer, System.Globalization.CultureInfo.CurrentCulture),
                    number.Initial, metrics, unit: number.Unit, boxWidth: 96)
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                };
                _fields.Add((field, () => numberBox.Read()));
                numberBox.ValueCommitted += (_, _) => Touched();
                return new DialogRow(field.Label, numberBox, metrics, editorWidth: 170);

            case ChoiceField choice:
                var combo = new ComboBox
                {
                    ItemsSource = choice.Options,
                    SelectedIndex = choice.Initial,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };

                _fields.Add((field, () => Math.Max(combo.SelectedIndex, 0)));
                combo.SelectionChanged += (_, _) => Touched();
                return new DialogRow(field.Label, combo, metrics, editorWidth: ChoiceWidth(choice, metrics));

            case FontField font:
                var picker = font.Fonts.Create(font.Initial);
                _fields.Add((field, () => picker.Family));
                picker.FamilyChanged += (_, _) => Touched();
                return new DialogRow(field.Label, picker, metrics, editorWidth: 240);

            case CheckField check:
                var toggle = new CheckBox
                {
                    IsChecked = check.Initial,
                    Content = new TextBlock
                    {
                        Text = field.Label,
                        FontSize = metrics.FontSize,
                        TextWrapping = TextWrapping.Wrap,
                    },
                };
                AutomationProperties.SetName(toggle, field.Label);

                _fields.Add((field, () => toggle.IsChecked == true));
                toggle.IsCheckedChanged += (_, _) => Touched();

                return new DialogRow(null, toggle, metrics, DialogRowKind.Indented, editorWidth: Natural(toggle, 360));

            case CustomFormField custom:
                var built = custom.Create(_texts, metrics);
                _fields.Add((field, built.Read));
                built.Observe?.Invoke(Touched);
                _sizedByCaller = true;
                return built.Labelled
                    ? new DialogRow(field.Label, built.Editor, metrics, editorWidth: Natural(built.Editor, 420))
                    : new DialogRow(null, built.Editor, metrics, DialogRowKind.Wide, editorWidth: Natural(built.Editor, 560));

            default:
                return new DialogRow(field.Label, new TextBlock(), metrics);
        }
    }

    private LabelledUnit? MeasureUnitOf(NumberField number) =>
        number.Unit is { Length: > 0 } unit
            ? new LabelledUnit(_services.Labels.Read(number.Label) is { } named && named.Symbol == unit ? named.Label : number.Label, unit)
            : _services.Labels.Read(number.Label);

    private static double ChoiceWidth(ChoiceField choice, DesignTokens.Metrics metrics)
    {
        var longest = choice.Options.Count == 0 ? 0 : choice.Options.Max(option => option.Length);
        return Math.Clamp(longest * metrics.FontSize * 0.6 + 48, DialogLayoutMeasures.MinimumEditorWidth, 320);
    }

    private static double Natural(Control control, double cap)
    {
        control.Measure(Size.Infinity);
        return Math.Clamp(control.DesiredSize.Width, DialogLayoutMeasures.MinimumEditorWidth, cap);
    }
}

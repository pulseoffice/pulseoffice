namespace PulseOffice.Document.Forms;

public enum FormControlKind
{
    Label,
    TextBox,
    CheckBox,
    OptionButton,
    ListBox,
    ComboBox,
    PushButton,
    ImageButton,
    FormattedField,
    DateField,
    TimeField,
    NumericField,
    CurrencyField,
    PatternField,
    GroupBox,
    ImageControl,
    FileSelection,
    TableControl,
    NavigationBar,
    SpinButton,
    ScrollBar,
}

public enum CheckState
{
    Unchecked,
    Checked,
    Indeterminate,
}

public sealed record NumericRange(double Minimum, double Maximum, double Step, double Page)
{
    public double Lower => Math.Min(Minimum, Maximum);

    public double Upper => Math.Max(Minimum, Maximum);

    public double Clamp(double value) => Math.Clamp(value, Lower, Upper);

    public double Snap(double value)
    {
        if (Step <= 0 || !double.IsFinite(value)) return Clamp(value);

        var snapped = Lower + (Math.Round((value - Lower) / Step) * Step);
        if (snapped > Upper) snapped -= Step;

        return Clamp(snapped);
    }
}

public sealed record FormControlValue
{
    public string? Text { get; init; }

    public double? Number { get; init; }

    public CheckState Checked { get; init; } = CheckState.Unchecked;

    public IReadOnlyList<int> SelectedIndices { get; init; } = [];
}

public enum FormControlEvent
{
    Clicked,
    DoubleClicked,
    ValueChanged,
    TextChanged,
    ItemSelected,
    FocusGained,
    FocusLost,
    KeyPressed,
    MouseEntered,
    MouseExited,
    Approve,
    Reset,
}

public enum FormBorderKind
{
    None,
    Flat,
    ThreeDimensional,
}

public enum FormTextAlignment
{
    Left,
    Centre,
    Right,
}

public enum FormImageScale
{
    None,
    Fit,
    Stretch,
}

public enum FormButtonAction
{
    None,
    Submit,
    Reset,
    OpenUrl,
}

public enum FormOrientation
{
    Horizontal,
    Vertical,
}

public sealed record FormTextStyle
{
    public string Family { get; init; } = "Helvetica";

    public double Size { get; init; } = 11;

    public bool Bold { get; init; }

    public bool Italic { get; init; }

    public bool Underline { get; init; }

    public string ColorHex { get; init; } = "#0F172A";

    public FormTextAlignment Alignment { get; init; } = FormTextAlignment.Left;
}

public sealed record FormControlProperties
{
    public string Name { get; init; } = string.Empty;

    public string? Label { get; init; }

    public bool Enabled { get; init; } = true;

    public bool Visible { get; init; } = true;

    public bool Printable { get; init; } = true;

    public bool ReadOnly { get; init; }

    public bool TabStop { get; init; } = true;

    public int TabIndex { get; init; }

    public string? LinkedCell { get; init; }

    public string? SourceRange { get; init; }

    public IReadOnlyList<string> Items { get; init; } = [];

    public bool MultiSelect { get; init; }

    public bool TriState { get; init; }

    public string? Group { get; init; }

    public string? Tooltip { get; init; }

    public string? FormatCode { get; init; }

    public string? ImagePath { get; init; }

    public NumericRange? Range { get; init; }

    public FormControlValue Value { get; init; } = new();

    public FormControlValue Default { get; init; } = new();

    public FormTextStyle Text { get; init; } = new();

    public string? BackgroundHex { get; init; }

    public string? BorderHex { get; init; }

    public FormBorderKind Border { get; init; } = FormBorderKind.Flat;

    public int MaxTextLength { get; init; }

    public bool MultiLine { get; init; }

    public char? PasswordCharacter { get; init; }

    public int DecimalPlaces { get; init; } = 2;

    public string? CurrencySymbol { get; init; }

    public bool ThousandsSeparator { get; init; }

    public bool Spin { get; init; }

    public bool Dropdown { get; init; } = true;

    public int DropdownLines { get; init; } = 8;

    public int BoundColumn { get; init; } = 1;

    public FormImageScale ImageScale { get; init; } = FormImageScale.Fit;

    public FormButtonAction ButtonAction { get; init; } = FormButtonAction.None;

    public bool Toggle { get; init; }

    public string? TargetUrl { get; init; }

    public FormOrientation Orientation { get; init; } = FormOrientation.Horizontal;

    public int RepeatDelayMilliseconds { get; init; } = 50;

    public string? DataField { get; init; }

    public string? HelpText { get; init; }

    public string? HelpUrl { get; init; }

    public IReadOnlyDictionary<FormControlEvent, string> Events { get; init; } =
        new Dictionary<FormControlEvent, string>();

    public FormControlProperties Bind(FormControlEvent handled, string? macro)
    {
        var events = new Dictionary<FormControlEvent, string>(Events);

        if (string.IsNullOrWhiteSpace(macro)) events.Remove(handled);
        else events[handled] = macro.Trim();

        return this with { Events = events };
    }

    public string? MacroFor(FormControlEvent raised) =>
        Events.TryGetValue(raised, out var macro) ? macro : null;
}

public sealed record FormDocumentSettings(bool OpenInDesignMode = false, bool AutomaticFocus = false, string? DataSource = null, string? Command = null);

public interface IFormDocument
{
    FormDocumentSettings FormSettings { get; set; }
}

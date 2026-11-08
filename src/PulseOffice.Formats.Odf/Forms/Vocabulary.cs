using PulseOffice.Formats;
using PulseOffice.Document.Forms;

namespace PulseOffice.Formats.Odf;

internal sealed record OdfControlKind(FormControlKind Kind, string Service, string Element);

internal sealed class OdfControlKinds
{
    private const string ServicePrefix = "ooo:com.sun.star.form.component.";

    private readonly IReadOnlyList<OdfControlKind> _kinds =
    [
        new(FormControlKind.Label, "FixedText", "fixed-text"),
        new(FormControlKind.TextBox, "TextField", "text"),
        new(FormControlKind.CheckBox, "CheckBox", "checkbox"),
        new(FormControlKind.OptionButton, "RadioButton", "radio"),
        new(FormControlKind.ListBox, "ListBox", "listbox"),
        new(FormControlKind.ComboBox, "ComboBox", "combobox"),
        new(FormControlKind.PushButton, "CommandButton", "button"),
        new(FormControlKind.ImageButton, "ImageButton", "image"),
        new(FormControlKind.FormattedField, "FormattedField", "formatted-text"),
        new(FormControlKind.DateField, "DateField", "date"),
        new(FormControlKind.TimeField, "TimeField", "time"),
        new(FormControlKind.NumericField, "NumericField", "number"),
        new(FormControlKind.CurrencyField, "CurrencyField", "number"),
        new(FormControlKind.PatternField, "PatternField", "text"),
        new(FormControlKind.GroupBox, "GroupBox", "generic-control"),
        new(FormControlKind.ImageControl, "DatabaseImageControl", "image-frame"),
        new(FormControlKind.FileSelection, "FileControl", "file"),
        new(FormControlKind.TableControl, "GridControl", "grid"),
        new(FormControlKind.NavigationBar, "NavigationToolBar", "generic-control"),
        new(FormControlKind.SpinButton, "SpinButton", "generic-control"),
        new(FormControlKind.ScrollBar, "ScrollBar", "generic-control"),
    ];

    private readonly IReadOnlyDictionary<string, FormControlKind> _byElement = new Dictionary<string, FormControlKind>(StringComparer.Ordinal)
    {
        ["checkbox"] = FormControlKind.CheckBox, ["radio"] = FormControlKind.OptionButton,
        ["listbox"] = FormControlKind.ListBox, ["combobox"] = FormControlKind.ComboBox,
        ["button"] = FormControlKind.PushButton, ["image"] = FormControlKind.ImageButton,
        ["image-frame"] = FormControlKind.ImageControl, ["file"] = FormControlKind.FileSelection,
        ["grid"] = FormControlKind.TableControl, ["fixed-text"] = FormControlKind.Label,
        ["frame"] = FormControlKind.GroupBox, ["value-range"] = FormControlKind.ScrollBar,
        ["formatted-text"] = FormControlKind.FormattedField, ["date"] = FormControlKind.DateField,
        ["time"] = FormControlKind.TimeField, ["number"] = FormControlKind.NumericField,
        ["text"] = FormControlKind.TextBox,
    };

    public OdfControlKind Of(FormControlKind kind) =>
        _kinds.FirstOrDefault(entry => entry.Kind == kind) ?? new OdfControlKind(kind, "TextField", "text");

    public string Implementation(FormControlKind kind) => ServicePrefix + Of(kind).Service;

    public FormControlKind? FromImplementation(string? implementation) =>
        _kinds.Where(entry => ServicePrefix + entry.Service == implementation).Select(entry => (FormControlKind?)entry.Kind).FirstOrDefault();

    public FormControlKind? FromElement(string localName) =>
        _byElement.TryGetValue(localName, out var kind) ? kind : null;
}

internal sealed class OdfFormVocabulary
{
    public TokenMap<FormControlEvent> Events { get; } = new(FormControlEvent.Reset, "form:resetted",
        (FormControlEvent.Clicked, "form:performaction"), (FormControlEvent.DoubleClicked, "dom:dblclick"),
        (FormControlEvent.ValueChanged, "form:changed"), (FormControlEvent.TextChanged, "form:textchanged"),
        (FormControlEvent.ItemSelected, "form:itemstatechange"), (FormControlEvent.FocusGained, "dom:focus"),
        (FormControlEvent.FocusLost, "dom:blur"), (FormControlEvent.KeyPressed, "dom:keydown"),
        (FormControlEvent.MouseEntered, "dom:mouseover"), (FormControlEvent.MouseExited, "dom:mouseout"),
        (FormControlEvent.Approve, "form:approveaction"), (FormControlEvent.Reset, "form:resetted"));

    public TokenMap<CheckState> States { get; } = new(CheckState.Unchecked, "unchecked",
        (CheckState.Checked, "checked"), (CheckState.Indeterminate, "unknown"));
}

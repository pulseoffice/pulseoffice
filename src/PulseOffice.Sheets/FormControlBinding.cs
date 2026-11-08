using PulseOffice.Document.Forms;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets;

public sealed record CellWrite(CellAddress Address, CellValue Value)
{
    public Worksheet? Sheet { get; init; }
}

public interface IFormControlBinding
{
    System.Globalization.CultureInfo Culture { get; }

    IReadOnlyList<string> ItemsFor(Worksheet sheet, FormControlProperties control, Workbook? workbook = null);

    CellWrite? Publish(FormControlKind kind, FormControlProperties control, Workbook? workbook = null);

    FormControlProperties Absorb(Worksheet sheet, FormControlKind kind, FormControlProperties control, Workbook? workbook = null);
}

public sealed class FormControlBinding : IFormControlBinding
{
    private readonly CellDisplay _display;
    private readonly SheetReferenceText _references = new();
    private readonly IReadOnlyList<ControlLink> _links;
    private readonly ControlLink _text;
    private readonly Formatting.ISpreadsheetLocale _locale;

    public FormControlBinding(Formatting.ISpreadsheetLocale locale)
    {
        _locale = locale ?? throw new ArgumentNullException(nameof(locale));
        _display = new CellDisplay(locale ?? throw new ArgumentNullException(nameof(locale)));
        _text = new TextLink(_display);
        _links = [new ChoiceLink(), new ListLink(), new NumberLink()];
    }

    public System.Globalization.CultureInfo Culture => _locale.Culture;

    public IReadOnlyList<string> ItemsFor(Worksheet sheet, FormControlProperties control, Workbook? workbook = null)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(control);

        if (control.SourceRange is not { } text || _references.Split(text) is not { } reference
            || Target(reference.Sheet, sheet, workbook) is not { } source || !CellRange.TryParseA1(reference.Local, out var range))
            return control.Items;

        var items = new List<string>();
        foreach (var (_, cell) in source.NonEmptyCellsIn(range))
            if (cell.Value is { IsEmpty: false } value)
                items.Add(_display.Of(value));
        return items;
    }

    public CellWrite? Publish(FormControlKind kind, FormControlProperties control, Workbook? workbook = null)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (control.LinkedCell is not { } text || _references.Split(text) is not { } reference
            || !CellAddress.TryParseA1(reference.Local, out var address))
            return null;
        if (reference.Sheet is null) return new CellWrite(address, Link(kind).Publish(control));
        return workbook?.FindSheet(reference.Sheet) is { } target
            ? new CellWrite(address, Link(kind).Publish(control)) { Sheet = target }
            : null;
    }

    public FormControlProperties Absorb(Worksheet sheet, FormControlKind kind, FormControlProperties control, Workbook? workbook = null)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(control);
        return control.LinkedCell is { } text && _references.Split(text) is var reference
            && Target(reference.Sheet, sheet, workbook) is { } source && CellAddress.TryParseA1(reference.Local, out var address)
            ? control with { Value = Link(kind).Absorb(control, source[address].Value) }
            : control;
    }

    private Worksheet? Target(string? name, Worksheet own, Workbook? workbook) =>
        name is null ? own : workbook?.FindSheet(name);

    private ControlLink Link(FormControlKind kind) => _links.FirstOrDefault(link => link.Handles(kind)) ?? _text;
}

internal sealed class CellDisplay(Formatting.ISpreadsheetLocale locale)
{
    public string Of(CellValue value) => value.Kind switch
    {
        ValueKind.Text => value.AsText(),
        ValueKind.Error => value.ErrorText,
        ValueKind.Empty => string.Empty,
        ValueKind.Boolean => new Formatting.CultureBooleanNames(locale.Culture).Name(value.AsBoolean()),
        _ => value.AsNumber().ToString("G15", locale.Culture),
    };
}

internal abstract class ControlLink
{
    public abstract bool Handles(FormControlKind kind);

    public abstract CellValue Publish(FormControlProperties control);

    public abstract FormControlValue Absorb(FormControlProperties control, CellValue value);
}

internal sealed class ChoiceLink : ControlLink
{
    public override bool Handles(FormControlKind kind) => kind is FormControlKind.CheckBox or FormControlKind.OptionButton;

    public override CellValue Publish(FormControlProperties control) => control.Value.Checked switch
    {
        CheckState.Checked => CellValue.True,
        CheckState.Unchecked => CellValue.False,
        _ => CellValue.Empty,
    };

    public override FormControlValue Absorb(FormControlProperties control, CellValue value) => control.Value with
    {
        Checked = value.IsEmpty && control.TriState
            ? CheckState.Indeterminate
            : value.IsNumeric && value.AsBoolean() ? CheckState.Checked : CheckState.Unchecked,
    };
}

internal sealed class ListLink : ControlLink
{
    public override bool Handles(FormControlKind kind) => kind is FormControlKind.ListBox;

    public override CellValue Publish(FormControlProperties control) =>
        control.Value.SelectedIndices.Count == 0 ? CellValue.Empty : CellValue.Number(control.Value.SelectedIndices[0] + 1);

    public override FormControlValue Absorb(FormControlProperties control, CellValue value) =>
        control.Value with { SelectedIndices = value.TryCoerceToNumber(out var index) && index >= 1 && index <= int.MaxValue ? [(int)index - 1] : [] };
}

internal sealed class NumberLink : ControlLink
{
    public override bool Handles(FormControlKind kind) => kind is FormControlKind.SpinButton or FormControlKind.ScrollBar
        or FormControlKind.NumericField or FormControlKind.CurrencyField or FormControlKind.FormattedField;

    public override CellValue Publish(FormControlProperties control) =>
        control.Value.Number is { } number ? CellValue.Number(number) : CellValue.Empty;

    public override FormControlValue Absorb(FormControlProperties control, CellValue value) =>
        control.Value with { Number = value.TryCoerceToNumber(out var number) ? control.Range is { } range ? range.Clamp(number) : number : null };
}

internal sealed class TextLink(CellDisplay display) : ControlLink
{
    public override bool Handles(FormControlKind kind) => true;

    public override CellValue Publish(FormControlProperties control) =>
        string.IsNullOrEmpty(control.Value.Text) ? CellValue.Empty : CellValue.Text(control.Value.Text);

    public override FormControlValue Absorb(FormControlProperties control, CellValue value) =>
        control.Value with { Text = value.IsEmpty ? null : display.Of(value) };
}

using PulseOffice.Documents.Controls;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxLegacyFormFields(ILegacyFormFieldConverter converter)
{
    public LegacyFormFieldControl? Read(string instruction, W.FormFieldData? data)
    {
        var word = instruction.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToUpperInvariant();
        var kind = word switch
        {
            "FORMCHECKBOX" => LegacyFormFieldKind.CheckBox,
            "FORMDROPDOWN" => LegacyFormFieldKind.DropDown,
            "FORMTEXT" => data?.GetFirstChild<W.TextInput>()?.GetFirstChild<W.TextBoxFormFieldType>()?.Val?.Value == W.TextBoxFormFieldValues.Date
                ? LegacyFormFieldKind.Date
                : LegacyFormFieldKind.Text,
            _ => (LegacyFormFieldKind?)null,
        };
        if (kind is null) return null;

        var box = data?.GetFirstChild<W.CheckBox>();
        var list = data?.GetFirstChild<W.DropDownListFormField>();
        var checkedState = box?.GetFirstChild<W.Checked>() is { } explicitState
            ? explicitState.Val?.Value ?? true
            : box?.GetFirstChild<W.DefaultCheckBoxFormFieldState>()?.Val?.Value ?? false;

        return converter.Convert(new LegacyFormField(kind.Value)
        {
            Name = data?.GetFirstChild<W.FormFieldName>()?.Val?.Value,
            Checked = checkedState,
            Entries = [.. list?.Elements<W.ListEntryFormField>().Select(entry => entry.Val?.Value ?? string.Empty) ?? []],
            Selected = list?.GetFirstChild<W.DropDownListSelection>()?.Val?.Value
                ?? list?.GetFirstChild<W.DefaultDropDownListItemIndex>()?.Val?.Value ?? 0,
            DateFormat = data?.GetFirstChild<W.TextInput>()?.GetFirstChild<W.Format>()?.Val?.Value,
        });
    }
}

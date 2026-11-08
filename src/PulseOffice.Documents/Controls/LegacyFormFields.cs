namespace PulseOffice.Documents.Controls;

public enum LegacyFormFieldKind
{
    Text,
    CheckBox,
    DropDown,
    Date,
}

public sealed record LegacyFormField(LegacyFormFieldKind Kind)
{
    public string? Name { get; init; }

    public bool Checked { get; init; }

    public IReadOnlyList<string> Entries { get; init; } = [];

    public int Selected { get; init; }

    public string? DateFormat { get; init; }

    public string? DateLanguage { get; init; }
}

public sealed record LegacyFormFieldControl(ContentControl Control, string? Shown);

public interface ILegacyFormFieldConverter
{
    LegacyFormFieldControl Convert(LegacyFormField field);
}

public sealed class LegacyFormFieldConverter : ILegacyFormFieldConverter
{
    public LegacyFormFieldControl Convert(LegacyFormField field)
    {
        var title = field.Name is { Length: > 0 } name && !name.StartsWith("__Fieldmark__", StringComparison.Ordinal) ? name : null;
        var control = new ContentControl { Title = title };
        switch (field.Kind)
        {
            case LegacyFormFieldKind.CheckBox:
                var box = control with { Kind = ContentControlKind.CheckBox, Checked = field.Checked };
                return new LegacyFormFieldControl(box, box.CurrentSymbol);
            case LegacyFormFieldKind.DropDown:
                var items = field.Entries.Select(entry => new ContentControlItem(entry, entry)).ToList();
                var shown = field.Selected >= 0 && field.Selected < items.Count ? items[field.Selected].DisplayText : items.FirstOrDefault()?.DisplayText;
                return new LegacyFormFieldControl(control with { Kind = ContentControlKind.DropDownList, Items = items }, shown);
            case LegacyFormFieldKind.Date:
                return new LegacyFormFieldControl(control with { Kind = ContentControlKind.Date, DateFormat = field.DateFormat, DateLanguage = field.DateLanguage }, null);
            default:
                return new LegacyFormFieldControl(control with { Kind = ContentControlKind.PlainText }, null);
        }
    }
}

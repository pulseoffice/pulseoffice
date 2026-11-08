using System.Globalization;
using DocumentFormat.OpenXml;
using PulseOffice.Documents.Controls;
using W = DocumentFormat.OpenXml.Wordprocessing;
using W14 = DocumentFormat.OpenXml.Office2010.Word;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxContentControls(ContentControlLockTokens locks)
{
    public W.SdtProperties Properties(ContentControl control)
    {
        var properties = new W.SdtProperties();
        if (control.Title is { Length: > 0 } title) properties.Append(new W.SdtAlias { Val = title });
        if (control.Tag is { Length: > 0 } tag) properties.Append(new W.Tag { Val = tag });
        if (control.NumericId is { } id) properties.Append(new W.SdtId { Val = id });
        if (locks.Token(control) is { } token) properties.Append(new W.Lock { Val = new EnumValue<W.LockingValues>(Locking(token)) });
        if (control.PlaceholderPart is { Length: > 0 } part) properties.Append(new W.SdtPlaceholder(new W.DocPartReference { Val = part }));
        if (control.ShowingPlaceholder) properties.Append(new W.ShowingPlaceholder());
        switch (control.Kind)
        {
            case ContentControlKind.PlainText:
                properties.Append(new W.SdtContentText { MultiLine = control.MultiLine ? true : null });
                break;
            case ContentControlKind.DropDownList:
                properties.Append(List(new W.SdtContentDropDownList(), control));
                break;
            case ContentControlKind.ComboBox:
                properties.Append(List(new W.SdtContentComboBox(), control));
                break;
            case ContentControlKind.Date:
                var date = new W.SdtContentDate();
                if (control.Date is { } full) date.FullDate = DateTime.SpecifyKind(full, DateTimeKind.Utc);
                date.Append(new W.DateFormat { Val = control.DateFormat ?? "dd/MM/yyyy" });
                if (control.DateLanguage is { Length: > 0 } language) date.Append(new W.LanguageId { Val = language });
                date.Append(new W.SdtDateMappingType { Val = W.DateFormatValues.DateTime });
                date.Append(new W.Calendar { Val = W.CalendarValues.Gregorian });
                properties.Append(date);
                break;
            case ContentControlKind.Picture:
                properties.Append(new W.SdtContentPicture());
                break;
            case ContentControlKind.CheckBox:
                properties.Append(new W14.SdtContentCheckBox(
                    new W14.Checked { Val = control.Checked ? W14.OnOffValues.One : W14.OnOffValues.Zero },
                    new W14.CheckedState { Font = control.CheckedSymbol.Font, Val = Hex(control.CheckedSymbol.Character) },
                    new W14.UncheckedState { Font = control.UncheckedSymbol.Font, Val = Hex(control.UncheckedSymbol.Character) }));
                break;
            default:
                properties.Append(new W.SdtContentRichText());
                break;
        }
        return properties;
    }

    public bool IsContentControl(W.SdtProperties? properties) =>
        properties is null || !properties.ChildElements.Any(child => child is W.SdtContentDocPartObject or W.SdtContentDocPartList or W.SdtContentCitation
            or W.SdtContentBibliography or W.SdtContentEquation or W.SdtContentGroup);

    public ContentControl Read(W.SdtProperties? properties, string content)
    {
        var control = new ContentControl();
        if (properties is null) return control;
        control = control with
        {
            Title = properties.GetFirstChild<W.SdtAlias>()?.Val?.Value,
            Tag = properties.GetFirstChild<W.Tag>()?.Val?.Value,
            NumericId = properties.GetFirstChild<W.SdtId>()?.Val?.Value,
            PlaceholderPart = properties.GetFirstChild<W.SdtPlaceholder>()?.GetFirstChild<W.DocPartReference>()?.Val?.Value,
            ShowingPlaceholder = properties.GetFirstChild<W.ShowingPlaceholder>() is { } showing && (showing.Val?.Value ?? true),
        };
        if (properties.GetFirstChild<W.Lock>()?.Val is { } locking && locking.HasValue) control = locks.Apply(control, Token(locking.Value));
        if (control.ShowingPlaceholder) control = control with { PlaceholderText = content };
        if (properties.GetFirstChild<W14.SdtContentCheckBox>() is { } box)
        {
            return control with
            {
                Kind = ContentControlKind.CheckBox,
                Checked = box.Checked?.Val?.Value is { } on && (on == W14.OnOffValues.One || on == W14.OnOffValues.True),
                CheckedSymbol = Symbol(box.CheckedState?.Val?.Value, box.CheckedState?.Font?.Value, control.CheckedSymbol),
                UncheckedSymbol = Symbol(box.UncheckedState?.Val?.Value, box.UncheckedState?.Font?.Value, control.UncheckedSymbol),
            };
        }
        foreach (var child in properties.ChildElements)
        {
            switch (child)
            {
                case W.SdtContentText plain:
                    return control with { Kind = ContentControlKind.PlainText, MultiLine = plain.MultiLine?.Value ?? false };
                case W.SdtContentDropDownList drop:
                    return control with { Kind = ContentControlKind.DropDownList, Items = Items(drop) };
                case W.SdtContentComboBox combo:
                    return control with { Kind = ContentControlKind.ComboBox, Items = Items(combo) };
                case W.SdtContentDate date:
                    return control with
                    {
                        Kind = ContentControlKind.Date,
                        Date = date.FullDate?.Value,
                        DateFormat = date.GetFirstChild<W.DateFormat>()?.Val?.Value,
                        DateLanguage = date.GetFirstChild<W.LanguageId>()?.Val?.Value,
                    };
                case W.SdtContentPicture:
                    return control with { Kind = ContentControlKind.Picture };
                case W.SdtContentRichText:
                    return control with { Kind = ContentControlKind.RichText };
            }
        }
        return control;
    }

    private IReadOnlyList<ContentControlItem> Items(OpenXmlElement list) =>
        [.. list.Elements<W.ListItem>().Select(item => new ContentControlItem(item.DisplayText?.Value ?? item.Value?.Value ?? string.Empty, item.Value?.Value ?? item.DisplayText?.Value ?? string.Empty))];

    private T List<T>(T list, ContentControl control) where T : OpenXmlCompositeElement
    {
        foreach (var item in control.Items) list.Append(new W.ListItem { DisplayText = item.DisplayText, Value = item.Value });
        return list;
    }

    private string Hex(string symbol) =>
        (System.Text.Rune.DecodeFromUtf16(symbol, out var rune, out _) == System.Buffers.OperationStatus.Done ? rune.Value : 0x2610).ToString("X4", CultureInfo.InvariantCulture);

    private CheckBoxSymbol Symbol(string? hex, string? font, CheckBoxSymbol fallback)
    {
        if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code) || code <= 0 || code > 0x10FFFF) return fallback;
        if (code >= 0xF000 && code <= 0xF0FF) code -= 0xF000;
        if (!System.Text.Rune.IsValid(code) || code == 0) return fallback;
        return new CheckBoxSymbol(char.ConvertFromUtf32(code), font);
    }

    private W.LockingValues Locking(string token) => token switch
    {
        "sdtContentLocked" => W.LockingValues.SdtContentLocked,
        "contentLocked" => W.LockingValues.ContentLocked,
        "sdtLocked" => W.LockingValues.SdtLocked,
        _ => W.LockingValues.Unlocked,
    };

    private string Token(W.LockingValues value) =>
        value == W.LockingValues.SdtContentLocked ? "sdtContentLocked"
        : value == W.LockingValues.ContentLocked ? "contentLocked"
        : value == W.LockingValues.SdtLocked ? "sdtLocked"
        : "unlocked";
}

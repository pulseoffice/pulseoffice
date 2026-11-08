using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Documents.Controls;

namespace PulseOffice.Formats.Odf.Odt;

internal sealed class OdtContentControls(ContentControlLockTokens locks) : OdtXml
{
    private const string CurrentDateFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    public XName Name => Loext + "content-control";

    public XElement Element(string id, ContentControl control)
    {
        var element = new XElement(Name);
        Flag(element, Loext + "showing-place-holder", control.ShowingPlaceholder);
        switch (control.Kind)
        {
            case ContentControlKind.CheckBox:
                Flag(element, Loext + "checkbox", true);
                Flag(element, Loext + "checked", control.Checked);
                element.Add(new XAttribute(Loext + "checked-state", control.CheckedSymbol.Character));
                element.Add(new XAttribute(Loext + "unchecked-state", control.UncheckedSymbol.Character));
                break;
            case ContentControlKind.Picture:
                Flag(element, Loext + "picture", true);
                break;
            case ContentControlKind.Date:
                Flag(element, Loext + "date", true);
                break;
            case ContentControlKind.PlainText:
                Flag(element, Loext + "plain-text", true);
                break;
            case ContentControlKind.ComboBox:
                Flag(element, Loext + "combobox", true);
                break;
            case ContentControlKind.DropDownList:
                Flag(element, Loext + "dropdown", true);
                break;
        }
        Optional(element, Loext + "date-format", control.Kind == ContentControlKind.Date ? control.DateFormat : null);
        Optional(element, Loext + "date-rfc-language-tag", control.DateLanguage);
        if (control.Date is { } date) element.Add(new XAttribute(Loext + "current-date", date.ToString(CurrentDateFormat, CultureInfo.InvariantCulture)));
        Optional(element, Loext + "alias", control.Title);
        Optional(element, Loext + "tag", control.Tag);
        if (control.NumericId is { } numeric) element.Add(new XAttribute(Loext + "id", numeric.ToString(CultureInfo.InvariantCulture)));
        Optional(element, Loext + "lock", locks.Token(control));
        element.Add(new XAttribute(Pt + "control-id", id));
        Optional(element, Pt + "placeholder-text", control.PlaceholderText);
        Optional(element, Pt + "placeholder-part", control.PlaceholderPart);
        if (control.Kind == ContentControlKind.CheckBox)
        {
            Optional(element, Pt + "checked-font", control.CheckedSymbol.Font);
            Optional(element, Pt + "unchecked-font", control.UncheckedSymbol.Font);
        }
        Flag(element, Pt + "multi-line", control.MultiLine);
        Flag(element, Pt + "block", control.Block);
        foreach (var item in control.Items)
            element.Add(new XElement(Loext + "list-item", new XAttribute(Loext + "display-text", item.DisplayText), new XAttribute(Loext + "value", item.Value)));
        return element;
    }

    public string? Id(XElement element) => (string?)element.Attribute(Pt + "control-id");

    public bool IsItem(XElement element) => element.Name == Loext + "list-item";

    public ContentControl Read(XElement element)
    {
        var kind = Bool(element, Loext + "checkbox") ? ContentControlKind.CheckBox
            : Bool(element, Loext + "picture") ? ContentControlKind.Picture
            : Bool(element, Loext + "date") ? ContentControlKind.Date
            : Bool(element, Loext + "plain-text") ? ContentControlKind.PlainText
            : Bool(element, Loext + "combobox") ? ContentControlKind.ComboBox
            : Bool(element, Loext + "dropdown") ? ContentControlKind.DropDownList
            : ContentControlKind.RichText;
        var fallback = new ContentControl();
        var control = new ContentControl
        {
            Kind = kind,
            ShowingPlaceholder = Bool(element, Loext + "showing-place-holder"),
            Checked = Bool(element, Loext + "checked"),
            CheckedSymbol = Symbol(element, "checked", fallback.CheckedSymbol),
            UncheckedSymbol = Symbol(element, "unchecked", fallback.UncheckedSymbol),
            DateFormat = (string?)element.Attribute(Loext + "date-format"),
            DateLanguage = (string?)element.Attribute(Loext + "date-rfc-language-tag"),
            Date = Date((string?)element.Attribute(Loext + "current-date")),
            Title = (string?)element.Attribute(Loext + "alias"),
            Tag = (string?)element.Attribute(Loext + "tag"),
            NumericId = int.TryParse((string?)element.Attribute(Loext + "id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric) ? numeric : null,
            PlaceholderText = (string?)element.Attribute(Pt + "placeholder-text"),
            PlaceholderPart = (string?)element.Attribute(Pt + "placeholder-part"),
            MultiLine = Bool(element, Pt + "multi-line"),
            Block = Bool(element, Pt + "block"),
            Items = [.. element.Elements(Loext + "list-item").Select(item => new ContentControlItem(
                (string?)item.Attribute(Loext + "display-text") ?? (string?)item.Attribute(Loext + "value") ?? string.Empty,
                (string?)item.Attribute(Loext + "value") ?? (string?)item.Attribute(Loext + "display-text") ?? string.Empty))],
        };
        return locks.Apply(control, (string?)element.Attribute(Loext + "lock"));
    }

    private CheckBoxSymbol Symbol(XElement element, string state, CheckBoxSymbol fallback)
    {
        var character = (string?)element.Attribute(Loext + state + "-state");
        if (string.IsNullOrEmpty(character)) return fallback;
        return new CheckBoxSymbol(character, (string?)element.Attribute(Pt + state + "-font"));
    }

    private DateTime? Date(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        var trimmed = value.Length >= 10 ? value[..10] : value;
        return DateTime.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
    }

    private bool Bool(XElement element, XName name) => string.Equals((string?)element.Attribute(name), "true", StringComparison.OrdinalIgnoreCase);

    private void Flag(XElement element, XName name, bool value)
    {
        if (value) element.Add(new XAttribute(name, "true"));
    }

    private void Optional(XElement element, XName name, string? value)
    {
        if (!string.IsNullOrEmpty(value)) element.Add(new XAttribute(name, value));
    }
}

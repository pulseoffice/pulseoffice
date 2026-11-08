using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PulseOffice.Documents.Controls;

namespace PulseOffice.Formats.Odf.Odt;

internal sealed partial class OdtLegacyFormFields(ILegacyFormFieldConverter converter) : OdtXml
{
    private static readonly XNamespace Field = "urn:openoffice:names:experimental:ooo-ms-interop:xmlns:field:1.0";

    private const string TypePrefix = "vnd.oasis.opendocument.field.";

    public XName Point => Field + "fieldmark";

    public XName Start => Field + "fieldmark-start";

    public XName End => Field + "fieldmark-end";

    public LegacyFormFieldControl? Read(XElement fieldmark)
    {
        var type = (string?)fieldmark.Attribute(Field + "type");
        var kind = type switch
        {
            TypePrefix + "FORMCHECKBOX" => LegacyFormFieldKind.CheckBox,
            TypePrefix + "FORMDROPDOWN" => LegacyFormFieldKind.DropDown,
            TypePrefix + "FORMTEXT" => LegacyFormFieldKind.Text,
            TypePrefix + "FORMDATE" => LegacyFormFieldKind.Date,
            _ => (LegacyFormFieldKind?)null,
        };
        if (kind is null) return null;

        var parameters = fieldmark.Elements(Field + "param")
            .Select(param => ((string?)param.Attribute(Field + "name") ?? string.Empty, (string?)param.Attribute(Field + "value") ?? string.Empty))
            .ToList();
        string? Value(string name) => parameters.Where(pair => pair.Item1 == name).Select(pair => pair.Item2).FirstOrDefault();

        return converter.Convert(new LegacyFormField(kind.Value)
        {
            Name = (string?)fieldmark.Attribute(Text + "name"),
            Checked = string.Equals(Value("Checkbox_Checked"), "true", StringComparison.OrdinalIgnoreCase),
            Entries = [.. parameters.Where(pair => pair.Item1 == "Dropdown_ListEntry").Select(pair => pair.Item2)],
            Selected = int.TryParse(Value("Dropdown_Selected"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var selected) ? selected : 0,
            DateFormat = Value("DateField_DateFormat") is { Length: > 0 } format ? DateFormat(format) : null,
            DateLanguage = Value("DateField_DateFormat_Language"),
        });
    }

    private string DateFormat(string odf) => Modifier().Replace(odf, string.Empty).Replace("\"", string.Empty, StringComparison.Ordinal)
        .Replace('D', 'd').Replace('Y', 'y');

    [GeneratedRegex(@"\[[^\]]*\]")]
    private static partial Regex Modifier();
}

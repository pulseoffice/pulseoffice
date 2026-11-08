using System.Xml.Linq;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Styling;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal sealed class OdsDefaultCellStyle
{
    private readonly LanguageProperty _language = new();

    public XElement Build(Workbook workbook)
    {
        var element = new XElement(Namespaces.Style + "default-style", new XAttribute(Namespaces.Style + "family", "table-cell"));
        var properties = new XElement(Namespaces.Style + "text-properties");
        if (_language.Write(new CellStyle { Language = workbook.DefaultLanguage }, properties)) element.Add(properties);
        return element;
    }

    public void Read(XDocument? styles, Workbook workbook)
    {
        var properties = styles?.Root?.Element(Namespaces.Office + "styles")?.Elements(Namespaces.Style + "default-style")
            .FirstOrDefault(style => (string?)style.Attribute(Namespaces.Style + "family") == "table-cell")
            ?.Element(Namespaces.Style + "text-properties");
        if (properties is null) return;
        workbook.DefaultLanguage = _language.Read(properties, new CellStyle()).Language ?? workbook.DefaultLanguage;
    }
}

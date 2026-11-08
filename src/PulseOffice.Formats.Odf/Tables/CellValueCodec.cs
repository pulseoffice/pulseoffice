using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Sheets.Values;
using static PulseOffice.Formats.Packaging.OdfPackage;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfCellValueCodec
{
    public void Write(XElement cell, CellValue value)
    {
        switch (value.Kind)
        {
            case ValueKind.Number:
                cell.Add(new XAttribute(Namespaces.Office + "value-type", "float"),
                    new XAttribute(Namespaces.Office + "value", value.AsNumber().ToString("R", CultureInfo.InvariantCulture)),
                    new XElement(Namespaces.Text + "p", value.ToString()));
                break;
            case ValueKind.Boolean:
                cell.Add(new XAttribute(Namespaces.Office + "value-type", "boolean"),
                    new XAttribute(Namespaces.Office + "boolean-value", value.AsBoolean() ? "true" : "false"));
                break;
            default:
                cell.Add(new XAttribute(Namespaces.Office + "value-type", "string"), new XElement(Namespaces.Text + "p", value.ToString()));
                break;
        }
    }

    public CellValue Read(XElement cell) => cell.Attribute(Namespaces.Office + "value-type")?.Value switch
    {
        "float" or "percentage" or "currency" =>
            double.TryParse(cell.Attribute(Namespaces.Office + "value")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)
                ? CellValue.Number(number)
                : CellValue.Empty,
        "boolean" => CellValue.Boolean(OdfBooleanValue.Parse(cell.Attribute(Namespaces.Office + "boolean-value")?.Value)),
        "string" => CellValue.Text(string.Concat(cell.Elements(Namespaces.Text + "p").Select(paragraph => paragraph.Value))),
        _ => CellValue.Empty,
    };
}

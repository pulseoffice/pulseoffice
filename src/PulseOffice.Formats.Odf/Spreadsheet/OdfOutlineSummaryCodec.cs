using System.Xml.Linq;
using PulseOffice.Sheets;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfOutlineSummaryCodec
{
    public XNamespace Namespace { get; } = "urn:pulseoffice:outline";

    public void Write(Worksheet sheet, XElement table)
    {
        if (!sheet.OutlineSummary.Below) table.Add(new XAttribute(Namespace + "summary-below", "false"));
        if (!sheet.OutlineSummary.Right) table.Add(new XAttribute(Namespace + "summary-right", "false"));
    }

    public OutlineSummary Read(XElement table) => new(
        (string?)table.Attribute(Namespace + "summary-below") != "false",
        (string?)table.Attribute(Namespace + "summary-right") != "false");
}

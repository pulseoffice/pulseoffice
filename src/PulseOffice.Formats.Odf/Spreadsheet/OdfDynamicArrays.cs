using System.Xml.Linq;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

public sealed class OdfDynamicArrays
{
    private readonly XName _spill = Namespaces.Loext + "spill";
    private readonly XName _legacy = XNamespace.Get("urn:pulseoffice:spreadsheet") + "dynamic-array";

    public XAttribute Mark() => new(_spill, "true");

    public bool IsMarked(XElement cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        return (string?)cell.Attribute(_spill) == "true" || (string?)cell.Attribute(_legacy) == "true";
    }
}

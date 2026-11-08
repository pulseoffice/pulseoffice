using System.Xml.Linq;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfCellEnd(string sheet, CellPoint end, Func<Twip, string> length)
{
    public IEnumerable<XAttribute> Attributes() =>
    [
        new XAttribute(Namespaces.Table + "end-cell-address", new OdfQualifiedRanges().WriteAddress(sheet, end.Cell)),
        new XAttribute(Namespaces.Table + "end-x", length(end.X)),
        new XAttribute(Namespaces.Table + "end-y", length(end.Y)),
    ];
}

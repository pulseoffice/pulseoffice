using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Documents.Blocks;

namespace PulseOffice.Formats.Odf.Odt;

internal sealed class OdtCellDirections : OdtXml
{
    public IEnumerable<XAttribute> Write(CellTextDirection direction) => direction switch
    {
        CellTextDirection.TopToBottom => [new XAttribute(Style + "writing-mode", "tb-rl")],
        CellTextDirection.BottomToTop => [new XAttribute(Style + "rotation-angle", "90"), new XAttribute(Pt + "text-direction", "bottom-to-top")],
        _ => [],
    };

    public CellTextDirection Read(XElement? properties)
    {
        if ((string?)properties?.Attribute(Pt + "text-direction") == "bottom-to-top") return CellTextDirection.BottomToTop;
        var mode = (string?)properties?.Attribute(Style + "writing-mode");
        if (mode == "bt-lr") return CellTextDirection.BottomToTop;
        if (mode is not null && mode.StartsWith("tb", StringComparison.Ordinal)) return CellTextDirection.TopToBottom;
        if (!double.TryParse((string?)properties?.Attribute(Style + "rotation-angle"), NumberStyles.Float, CultureInfo.InvariantCulture, out var angle) || !double.IsFinite(angle)) return CellTextDirection.Horizontal;
        var turned = (Math.Round(angle) % 360 + 360) % 360;
        return turned switch
        {
            90 => CellTextDirection.BottomToTop,
            270 => CellTextDirection.TopToBottom,
            _ => CellTextDirection.Horizontal,
        };
    }
}

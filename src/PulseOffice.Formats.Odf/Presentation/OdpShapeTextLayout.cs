using System.Xml.Linq;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Formats.Odf;

internal sealed class OdpShapeTextLayout
{
    private static readonly XNamespace Style = OdfPackage.Namespaces.Style;

    public Diagnostic Loss { get; } = Diagnostic.Warning("ODP217",
        "Some shapes have vertical text written from left to right: Deck shows it as vertical text written from right to left.");

    public bool Present(XContainer content) =>
        content.Descendants(Style + "graphic-properties").Any(properties => (string?)properties.Attribute(Style + "writing-mode") == "tb-lr");
}

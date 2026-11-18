using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Formats.Odf;

internal sealed class OdpShapeTextLayout
{
    private static readonly XNamespace Style = OdfPackage.Namespaces.Style;
    private static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;
    private static readonly XNamespace Fo = OdfPackage.Namespaces.Fo;

    public Diagnostic Loss { get; } = Diagnostic.Warning("ODP217",
        "Some shapes have vertical text, text rotated inside the shape or text in columns: Deck lays shape text out horizontally in one column, so these settings are not imported.");

    public bool Present(XContainer content) =>
        content.Descendants(Style + "graphic-properties").Any(Laid)
        || content.Descendants(Draw + "enhanced-geometry").Any(geometry => Angle((string?)geometry.Attribute(Draw + "text-rotate-angle")));

    private bool Laid(XElement properties) =>
        ((string?)properties.Attribute(Style + "writing-mode"))?.StartsWith("tb", StringComparison.Ordinal) == true
        || properties.Elements(Style + "columns").Any(columns =>
            int.TryParse((string?)columns.Attribute(Fo + "column-count"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && count > 1);

    private bool Angle(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var degrees) && double.IsFinite(degrees) && degrees % 360 != 0;
}

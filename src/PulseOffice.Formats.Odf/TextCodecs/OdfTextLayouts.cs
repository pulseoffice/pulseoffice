using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Text.Rich;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf.TextCodecs;

internal sealed class OdfTextLayouts
{
    private readonly XNamespace _pulse = "urn:pulseoffice:shape-text";
    private readonly OdfMeasure _measure = new();

    public IEnumerable<XObject> Write(TextLayout layout)
    {
        if (layout.Flow == TextFlow.MongolianVertical)
            yield return new XAttribute(Namespaces.Style + "writing-mode", "tb-lr");
        else if (layout.IsVertical)
            yield return new XAttribute(Namespaces.Style + "writing-mode", "tb-rl");
        if (layout.Flow == TextFlow.Vertical270)
            yield return new XAttribute(Namespaces.Loext + "writing-mode", "bt-lr");
        if (layout.Flow == TextFlow.EastAsianVertical)
            yield return new XAttribute(_pulse + "text-flow", "east-asian");
        if (layout.IsStacked)
            yield return new XAttribute(Namespaces.Style + "direction", "ttb");
        if (layout.Flow == TextFlow.StackedRightToLeft)
            yield return new XAttribute(_pulse + "text-flow", "stacked-rl");
        if (layout.Upright)
            yield return new XAttribute(_pulse + "upright", "true");
        if (layout.Columns > 1 || layout.ColumnSpacing > 0)
            yield return new XElement(Namespaces.Style + "columns",
                new XAttribute(Namespaces.Fo + "column-count", layout.Columns.ToString(CultureInfo.InvariantCulture)),
                new XAttribute(Namespaces.Fo + "column-gap", _measure.Length(PulseOffice.Foundation.Units.Twip.FromPoints(layout.ColumnSpacing))));
    }

    public TextLayout? Read(XElement? properties)
    {
        if (properties is null) return null;
        var mode = (string?)properties.Attribute(Namespaces.Loext + "writing-mode") ?? (string?)properties.Attribute(Namespaces.Style + "writing-mode");
        var flow = (string?)properties.Attribute(_pulse + "text-flow");
        var direction = (string?)properties.Attribute(Namespaces.Style + "direction");
        var upright = (string?)properties.Attribute(_pulse + "upright");
        var columns = properties.Element(Namespaces.Style + "columns");
        if (mode is null && flow is null && direction is null && upright is null && columns is null) return null;

        var count = int.TryParse((string?)columns?.Attribute(Namespaces.Fo + "column-count"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? Math.Clamp(parsed, 1, 16) : 1;
        var gap = (string?)columns?.Attribute(Namespaces.Fo + "column-gap") is { Length: > 0 } text ? _measure.ParseLength(text, PulseOffice.Foundation.Units.Twip.Zero).Value / 20.0 : 0;
        return new TextLayout
        {
            Flow = flow == "vertical-270" || mode == "bt-lr" ? TextFlow.Vertical270
                : direction == "ttb" ? flow == "stacked-rl" ? TextFlow.StackedRightToLeft : TextFlow.Stacked
                : mode == "tb-lr" ? TextFlow.MongolianVertical
                : flow == "east-asian" && mode?.StartsWith("tb", StringComparison.Ordinal) == true ? TextFlow.EastAsianVertical
                : mode?.StartsWith("tb", StringComparison.Ordinal) == true ? TextFlow.Vertical
                : TextFlow.Horizontal,
            Upright = upright == "true",
            Columns = count,
            ColumnSpacing = Math.Max(0, gap),
        };
    }

    public IEnumerable<XAttribute> FrameRotation(double degrees)
    {
        if (Math.Abs(degrees) > 0.001)
            yield return new XAttribute(_pulse + "text-rotate-angle", Normalized(degrees).ToString("0.###", CultureInfo.InvariantCulture));
    }

    public double? FrameRotation(XElement? properties) =>
        double.TryParse((string?)properties?.Attribute(_pulse + "text-rotate-angle"), NumberStyles.Float, CultureInfo.InvariantCulture, out var degrees)
        && double.IsFinite(degrees) ? Normalized(degrees) : null;

    public double? GeometryRotation(XElement? geometry) =>
        double.TryParse((string?)geometry?.Attribute(Namespaces.Draw + "text-rotate-angle"), NumberStyles.Float, CultureInfo.InvariantCulture, out var degrees)
        && double.IsFinite(degrees) ? Normalized(-degrees) : null;

    public void WriteGeometryRotation(XElement geometry, double degrees)
    {
        if (Math.Abs(degrees) > 0.001)
            geometry.SetAttributeValue(Namespaces.Draw + "text-rotate-angle", Normalized(-degrees).ToString("0.###", CultureInfo.InvariantCulture));
    }

    private double Normalized(double degrees) => (degrees % 360 + 360) % 360;
}

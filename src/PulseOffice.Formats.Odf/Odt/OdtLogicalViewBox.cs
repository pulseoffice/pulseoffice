using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Presentations;

namespace PulseOffice.Formats.Odf.Odt;

internal sealed class OdtLogicalViewBox : OdtXml
{
    private const double TwipsPerHundredth = 1440.0 / 2540.0;
    private const double Tolerance = 1.0;

    public XElement Write(XElement node, SlideElement element)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(element);
        if (node.Name != Draw + "custom-shape" || node.Element(Draw + "enhanced-geometry") is not { } geometry) return node;
        if (!geometry.Elements(Draw + "equation").Any(Logical)) return node;
        var view = ((string?)geometry.Attribute(Svg + "viewBox") ?? string.Empty).Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(token => double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : double.NaN).ToList();
        if (view.Count != 4 || view.Any(double.IsNaN)) return node;
        var width = element.Bounds.Width.Value / TwipsPerHundredth;
        var height = element.Bounds.Height.Value / TwipsPerHundredth;
        if (Math.Abs(view[2] - width) > Tolerance || Math.Abs(view[3] - height) > Tolerance) return node;
        double[] twips = [view[0] * TwipsPerHundredth, view[1] * TwipsPerHundredth, element.Bounds.Width.Value, element.Bounds.Height.Value];
        geometry.SetAttributeValue(Svg + "viewBox", string.Join(' ', twips.Select(value => value.ToString("0.###", CultureInfo.InvariantCulture))));
        return node;
    }

    private bool Logical(XElement equation) =>
        ((string?)equation.Attribute(Draw + "formula") ?? string.Empty) is var formula
            && (formula.Contains("logwidth", StringComparison.Ordinal) || formula.Contains("logheight", StringComparison.Ordinal));
}

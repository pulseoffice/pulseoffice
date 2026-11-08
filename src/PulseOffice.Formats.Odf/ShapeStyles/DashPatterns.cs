using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfDashPatterns
{
    private const double MostDots = 100;

    private const double LongestRelative = 100;

    public ShapeDashPattern Preset(ShapeStrokeStyle dash) => new()
    {
        Dots1 = 1,
        Dots1Length = Twip.FromCentimeters(dash switch
        {
            ShapeStrokeStyle.Dotted => 0.05,
            ShapeStrokeStyle.LongDash => 0.6,
            _ => 0.25,
        }).Value,
        Dots2 = dash switch
        {
            ShapeStrokeStyle.DashDot => 1,
            ShapeStrokeStyle.DashDotDot => 2,
            _ => 0,
        },
        Dots2Length = Twip.FromCentimeters(0.1).Value,
        Distance = Twip.FromCentimeters(0.1).Value,
        Relative = false,
    };

    public ShapeStrokeStyle Nearest(ShapeDashPattern pattern) => pattern.NearestPreset();

    public ShapeDashPattern Read(XElement dash, OdfMeasure measure)
    {
        string? Attr(string name) => (string?)dash.Attribute(Namespaces.Draw + name);
        var relative = new[] { "dots1-length", "dots2-length", "distance" }.Any(name => Attr(name)?.EndsWith('%') == true);
        double Length(string name, double fallback) => Attr(name) is not { Length: > 0 } text ? fallback
            : relative ? Math.Clamp(measure.ParsePercent(text, fallback), 0, LongestRelative)
            : Math.Max(0, measure.ParseLength(text, new Twip((int)Math.Round(fallback))).Value);
        int Count(string name, double fallback) => (int)Math.Clamp(measure.ParseNumber(Attr(name), fallback), 0, MostDots);
        return new ShapeDashPattern
        {
            Dots1 = Count("dots1", 1),
            Dots1Length = Length("dots1-length", relative ? 1 : 0),
            Dots2 = Count("dots2", 0),
            Dots2Length = Length("dots2-length", relative ? 1 : 0),
            Distance = Length("distance", relative ? 1 : Twip.FromCentimeters(0.1).Value),
            Relative = relative,
            Round = Attr("style") == "round",
        };
    }

    public object[] Attributes(ShapeDashPattern pattern, OdfMeasure measure)
    {
        string Length(double value) => pattern.Relative
            ? $"{Math.Round(value * 100, 3).ToString(CultureInfo.InvariantCulture)}%"
            : measure.Length(new Twip((int)Math.Round(value)));
        var attributes = new List<object>
        {
            new XAttribute(Namespaces.Draw + "style", pattern.Round ? "round" : "rect"),
            new XAttribute(Namespaces.Draw + "dots1", pattern.Dots1.ToString(CultureInfo.InvariantCulture)),
            new XAttribute(Namespaces.Draw + "dots1-length", Length(pattern.Dots1Length)),
        };
        if (pattern.Dots2 > 0 || !pattern.Relative)
        {
            attributes.Add(new XAttribute(Namespaces.Draw + "dots2", pattern.Dots2.ToString(CultureInfo.InvariantCulture)));
            attributes.Add(new XAttribute(Namespaces.Draw + "dots2-length", Length(pattern.Dots2Length)));
        }
        attributes.Add(new XAttribute(Namespaces.Draw + "distance", Length(pattern.Distance)));
        return [.. attributes];
    }
}

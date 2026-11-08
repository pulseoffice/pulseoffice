using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Drawing.Fontwork;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfFontworkGeometry
{
    private const int Box = 21600;

    private readonly FontworkWarps _warps = new();
    private readonly FontworkPath _path = new();

    public XElement Geometry(FontworkWarp warp) =>
        new(Namespaces.Draw + "enhanced-geometry",
            new XAttribute(Namespaces.Svg + "viewBox", $"0 0 {Box} {Box}"),
            new XAttribute(Namespaces.Draw + "type", _warps.Odf(warp)),
            new XAttribute(Namespaces.Draw + "enhanced-path", Path(warp)),
            new XAttribute(Namespaces.Draw + "text-path", "true"),
            new XAttribute(Namespaces.Draw + "text-path-mode", "path"),
            new XAttribute(Namespaces.Draw + "text-path-scale", "shape"),
            new XAttribute(Namespaces.Draw + "text-areas", $"0 0 {Box} {Box}"));

    public XElement Lettering(XElement style, int heightTwips)
    {
        var points = Math.Max(24, Math.Round(heightTwips / 20.0 * 0.6));
        var text = style.Element(Namespaces.Style + "text-properties");
        if (text is null)
        {
            text = new XElement(Namespaces.Style + "text-properties");
            style.Add(text);
        }

        text.SetAttributeValue(Namespaces.Fo + "font-size", string.Create(CultureInfo.InvariantCulture, $"{points}pt"));
        style.Element(Namespaces.Style + "graphic-properties")?.SetAttributeValue(Namespaces.Fo + "wrap-option", "no-wrap");
        return style;
    }

    public bool IsFontwork(XElement node) =>
        (string?)node.Element(Namespaces.Draw + "enhanced-geometry")?.Attribute(Namespaces.Draw + "text-path") == "true";

    public FontworkWarp WarpOf(XElement node) =>
        _warps.FromOdf((string?)node.Element(Namespaces.Draw + "enhanced-geometry")?.Attribute(Namespaces.Draw + "type"));

    public string TextOf(XElement node) =>
        string.Join(" ", node.Elements(Namespaces.Text + "p").Select(paragraph => paragraph.Value));

    private string Path(FontworkWarp warp)
    {
        var points = Enumerable.Range(0, 41).Select(step => _path.At(warp, step / 40.0))
            .Select(point => string.Create(CultureInfo.InvariantCulture, $"{Math.Round(point.X * Box)} {Math.Round(point.Y * Box)}"));
        return "M " + string.Join(" L ", points) + " N";
    }
}

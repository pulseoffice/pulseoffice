using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PulseOffice.Documents.Styles;

namespace PulseOffice.Formats.Odf.Odt;

internal sealed partial class OdtWatermarks(OdtMeasure measure) : OdtXml
{
    public const string ShapeName = "PowerPlusWaterMarkObject";

    private const double Reach = 0.8;
    private const double Aspect = 1.6;

    public XElement Paragraph(PageWatermark watermark, PageStyle page, OdtStyleSink sink)
    {
        var areaWidth = Math.Max(1, page.Width.ToPoints() - page.Margins.Left.ToPoints() - page.Margins.Right.ToPoints());
        var areaHeight = Math.Max(1, page.Height.ToPoints() - page.Margins.Top.ToPoints() - page.Margins.Bottom.ToPoints());
        var radians = watermark.Angle * Math.PI / 180;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var across = Math.Abs(cos) < 0.01 ? double.MaxValue : areaWidth / Math.Abs(cos);
        var down = Math.Abs(sin) < 0.01 ? double.MaxValue : areaHeight / Math.Abs(sin);
        var width = Math.Min(across, down) * Reach;
        var height = Math.Max(12, width * Aspect / Math.Max(1, watermark.Text.Length));
        var (cx, cy) = (width / 2, height / 2);
        var tx = cx - (cx * cos + cy * sin);
        var ty = cy - (-cx * sin + cy * cos);
        var style = sink.Register("graphic", null,
        [
            new XElement(Style + "graphic-properties",
                new XAttribute(Draw + "stroke", "none"),
                new XAttribute(Draw + "fill", "solid"),
                new XAttribute(Draw + "fill-color", measure.Hex(watermark.Color)),
                new XAttribute(Draw + "opacity", measure.Percent(Math.Clamp(100 - watermark.Transparency, 0, 100) / 100)),
                new XAttribute(Style + "wrap", "run-through"),
                new XAttribute(Style + "run-through", "background"),
                new XAttribute(Style + "vertical-pos", "middle"),
                new XAttribute(Style + "vertical-rel", "page-content"),
                new XAttribute(Style + "horizontal-pos", "center"),
                new XAttribute(Style + "horizontal-rel", "page-content")),
            new XElement(Style + "text-properties", new XAttribute(Fo + "font-family", watermark.Font)),
        ], "gr");
        var shape = new XElement(Draw + "custom-shape",
            new XAttribute(Text + "anchor-type", "char"),
            new XAttribute(Draw + "z-index", "0"),
            new XAttribute(Draw + "name", ShapeName + "1"),
            new XAttribute(Draw + "style-name", style),
            new XAttribute(Svg + "width", measure.Points(width)),
            new XAttribute(Svg + "height", measure.Points(height)),
            new XAttribute(Draw + "transform", string.Create(CultureInfo.InvariantCulture, $"rotate ({radians:0.######}) translate ({tx:0.###}pt {ty:0.###}pt)")),
            new XElement(Text + "p", watermark.Text),
            new XElement(Draw + "enhanced-geometry",
                new XAttribute(Svg + "viewBox", "0 0 21600 21600"),
                new XAttribute(Draw + "text-areas", "0 0 21600 21600"),
                new XAttribute(Draw + "text-path", "true"),
                new XAttribute(Draw + "text-path-mode", "shape"),
                new XAttribute(Draw + "text-path-scale", "path"),
                new XAttribute(Draw + "text-path-same-letter-heights", "false"),
                new XAttribute(Draw + "type", "fontwork-plain-text"),
                new XAttribute(Draw + "enhanced-path", "M 0 10800 L 21600 10800 N")));
        return new XElement(Text + "p", new XAttribute(Pt + "watermark", "true"), shape);
    }

    public PageWatermark? Extract(XElement? region, OdtAutomaticStyles styles)
    {
        if (region is null) return null;
        var shape = region.Descendants(Draw + "custom-shape")
            .FirstOrDefault(element => ((string?)element.Attribute(Draw + "name"))?.StartsWith(ShapeName, StringComparison.Ordinal) == true);
        if (shape is null) return null;
        var text = string.Concat(shape.Elements(Text + "p").Select(paragraph => paragraph.Value));
        var style = styles.Find("graphic", (string?)shape.Attribute(Draw + "style-name"));
        var graphic = style?.Element(Style + "graphic-properties");
        var font = (string?)style?.Element(Style + "text-properties")?.Attribute(Fo + "font-family")
            ?? (string?)style?.Element(Style + "text-properties")?.Attribute(Style + "font-name");
        var opacity = measure.Percent((string?)graphic?.Attribute(Draw + "opacity"));
        var watermark = new PageWatermark(text)
        {
            Font = font?.Trim('\'', '"') is { Length: > 0 } family ? family : "Liberation Sans",
            Color = measure.Color((string?)graphic?.Attribute(Draw + "fill-color")) ?? 0xFFC0C0C0,
            Transparency = opacity is { } value ? Math.Round(100 - value * 100) : 50,
            Angle = Angle((string?)shape.Attribute(Draw + "transform")),
        };
        var paragraph = shape.Ancestors(Text + "p").FirstOrDefault();
        shape.Remove();
        if (paragraph is not null && !paragraph.Nodes().Any(node => node is XElement || node is XText { Value.Length: > 0 })) paragraph.Remove();
        return watermark;
    }

    private double Angle(string? transform)
    {
        if (transform is null || Rotation().Match(transform) is not { Success: true } match) return 0;
        return double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var radians)
            ? Math.Round(radians * 180 / Math.PI, 2)
            : 0;
    }

    [GeneratedRegex(@"rotate\s*\(\s*([-+0-9.eE]+)\s*\)")]
    private static partial Regex Rotation();
}

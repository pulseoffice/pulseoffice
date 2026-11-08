using System.Globalization;
using System.Security;
using DocumentFormat.OpenXml;
using PulseOffice.Documents.Styles;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxWatermarks(DocxValues values)
{
    private const string ShapeName = "PowerPlusWaterMarkObject";
    private const string VmlNamespace = "urn:schemas-microsoft-com:vml";
    private const string OfficeNamespace = "urn:schemas-microsoft-com:office:office";
    private const double Reach = 0.8;
    private const double Aspect = 1.6;

    public W.Paragraph Paragraph(PageWatermark watermark, PageStyle page, int index)
    {
        var areaWidth = Math.Max(1, page.Width.ToPoints() - page.Margins.Left.ToPoints() - page.Margins.Right.ToPoints());
        var areaHeight = Math.Max(1, page.Height.ToPoints() - page.Margins.Top.ToPoints() - page.Margins.Bottom.ToPoints());
        var radians = watermark.Angle * Math.PI / 180;
        var across = Math.Abs(Math.Cos(radians)) < 0.01 ? double.MaxValue : areaWidth / Math.Abs(Math.Cos(radians));
        var down = Math.Abs(Math.Sin(radians)) < 0.01 ? double.MaxValue : areaHeight / Math.Abs(Math.Sin(radians));
        var width = Math.Min(across, down) * Reach;
        var height = Math.Max(12, width * Aspect / Math.Max(1, watermark.Text.Length));
        var rotation = ((360 - watermark.Angle) % 360 + 360) % 360;
        var opacity = Math.Clamp(100 - watermark.Transparency, 0, 100) / 100;
        var xml = string.Create(CultureInfo.InvariantCulture,
            $"""<w:pict xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:v="{VmlNamespace}" xmlns:o="{OfficeNamespace}" xmlns:w10="urn:schemas-microsoft-com:office:word"><v:shapetype id="_x0000_t136" coordsize="21600,21600" o:spt="136" adj="10800" path="m@7,l@8,m@5,21600l@6,21600e"><v:formulas><v:f eqn="sum #0 0 10800"/><v:f eqn="prod #0 2 1"/><v:f eqn="sum 21600 0 @1"/><v:f eqn="sum 0 0 @2"/><v:f eqn="sum 21600 0 @3"/><v:f eqn="if @0 @3 0"/><v:f eqn="if @0 21600 @1"/><v:f eqn="if @0 0 @2"/><v:f eqn="if @0 @4 21600"/><v:f eqn="mid @5 @6"/><v:f eqn="mid @8 @5"/><v:f eqn="mid @7 @8"/><v:f eqn="mid @6 @7"/><v:f eqn="sum @6 0 @5"/></v:formulas><v:path textpathok="t" o:connecttype="custom" o:connectlocs="@9,0;@10,10800;@11,21600;@12,10800" o:connectangles="270,180,90,0"/><v:textpath on="t" fitshape="t"/><o:lock v:ext="edit" text="t" shapetype="t"/></v:shapetype><v:shape id="{ShapeName}{index}" o:spid="_x0000_s{2049 + index}" type="#_x0000_t136" style="position:absolute;margin-left:0;margin-top:0;width:{width:0.##}pt;height:{height:0.##}pt;rotation:{rotation:0.##};z-index:-251657216;mso-position-horizontal:center;mso-position-horizontal-relative:margin;mso-position-vertical:center;mso-position-vertical-relative:margin" o:allowincell="f" fillcolor="#{values.Color(watermark.Color)}" stroked="f"><v:fill opacity="{opacity:0.##}"/><v:textpath style="font-family:&quot;{SecurityElement.Escape(watermark.Font)}&quot;;font-size:1pt" string="{SecurityElement.Escape(watermark.Text)}"/><w10:wrap anchorx="margin" anchory="margin"/></v:shape></w:pict>""");
        return new W.Paragraph(new W.Run(new W.RunProperties(new W.NoProof()), new W.Picture(xml)));
    }

    public PageWatermark? Extract(OpenXmlElement root)
    {
        foreach (var picture in root.Descendants<W.Picture>().ToList())
        {
            var shape = picture.ChildElements.FirstOrDefault(child => child.LocalName == "shape" && child.NamespaceUri == VmlNamespace
                && Attribute(child, "id")?.StartsWith(ShapeName, StringComparison.Ordinal) == true);
            var path = shape?.ChildElements.FirstOrDefault(child => child.LocalName == "textpath");
            if (shape is null || path is null) continue;
            var style = Declarations(Attribute(shape, "style"));
            var text = Declarations(Attribute(path, "style"));
            var opacity = shape.ChildElements.FirstOrDefault(child => child.LocalName == "fill") is { } fill ? Attribute(fill, "opacity") : null;
            var rotation = style.TryGetValue("rotation", out var turn) && double.TryParse(turn, NumberStyles.Float, CultureInfo.InvariantCulture, out var degrees) && double.IsFinite(degrees) ? degrees : 0;
            var angle = -rotation % 360;
            if (angle <= -180) angle += 360;
            if (angle > 180) angle -= 360;
            var watermark = new PageWatermark(Attribute(path, "string") ?? string.Empty)
            {
                Font = text.TryGetValue("font-family", out var family) ? family.Trim('"', '\'') : "Liberation Sans",
                Color = Color(Attribute(shape, "fillcolor")),
                Transparency = Opacity(opacity) is { } value ? Math.Clamp(Math.Round(100 - value * 100), 0, 100) : 0,
                Angle = angle,
            };
            var run = picture.Ancestors<W.Run>().FirstOrDefault();
            var paragraph = picture.Ancestors<W.Paragraph>().FirstOrDefault();
            (run as OpenXmlElement ?? picture).Remove();
            if (paragraph is not null && !paragraph.Descendants<W.Text>().Any() && !paragraph.Descendants<W.Drawing>().Any() && !paragraph.Descendants<W.Picture>().Any()) paragraph.Remove();
            return watermark;
        }
        return null;
    }

    private string? Attribute(OpenXmlElement element, string name) =>
        element.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == name).Value;

    private Dictionary<string, string> Declarations(string? style)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in (style ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = part.IndexOf(':');
            if (colon > 0) map[part[..colon].Trim()] = part[(colon + 1)..].Trim().Replace("&quot;", "\"", StringComparison.Ordinal);
        }
        return map;
    }

    private double? Opacity(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (value.EndsWith('f') && double.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var fixedPoint) && double.IsFinite(fixedPoint)) return fixedPoint / 65536;
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number) ? number : null;
    }

    private uint Color(string? value)
    {
        if (string.IsNullOrEmpty(value)) return 0xFFC0C0C0;
        var hex = value.Split(' ')[0].TrimStart('#');
        if (hex.Length == 6 && values.Color(hex) is { } parsed) return parsed;
        return hex.ToLowerInvariant() switch
        {
            "silver" => 0xFFC0C0C0,
            "gray" or "grey" => 0xFF808080,
            "black" => 0xFF000000,
            "red" => 0xFFFF0000,
            "blue" => 0xFF0000FF,
            "green" => 0xFF008000,
            _ => 0xFFC0C0C0,
        };
    }
}

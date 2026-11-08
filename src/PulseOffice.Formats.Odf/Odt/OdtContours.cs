using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Documents.Objects;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Formats.Odf.Odt;

internal sealed class OdtContours(OdtMeasure measure) : OdtXml
{
    private const int Span = 10000;

    public string? Mirror(AnchoredObject item) => (item.FlipHorizontal, item.FlipVertical) switch
    {
        (true, true) => "horizontal vertical",
        (true, false) => "horizontal",
        (false, true) => "vertical",
        _ => null,
    };

    public bool Horizontal(string? mirror) => mirror is not null && mirror.Split(' ').Any(token => token.StartsWith("horizontal", StringComparison.Ordinal));

    public bool Vertical(string? mirror) => mirror is not null && mirror.Split(' ').Contains("vertical");

    public (double Rotation, Twip X, Twip Y)? Transform(XElement frame, DocumentSize size)
    {
        if ((string?)frame.Attribute(Draw + "transform") is not { Length: > 0 } transform) return null;
        var rotate = System.Text.RegularExpressions.Regex.Match(transform, @"rotate\s*\(\s*([-+0-9.eE]+)\s*\)");
        var translate = System.Text.RegularExpressions.Regex.Match(transform, @"translate\s*\(\s*([^ )]+)\s+([^ )]+)\s*\)");
        if (!rotate.Success || !double.TryParse(rotate.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var radians)) return null;
        var tx = translate.Success ? measure.Twips(translate.Groups[1].Value)?.ToPoints() ?? 0 : 0;
        var ty = translate.Success ? measure.Twips(translate.Groups[2].Value)?.ToPoints() ?? 0 : 0;
        var angle = -radians;
        var halfWidth = size.Width.ToPoints() / 2;
        var halfHeight = size.Height.ToPoints() / 2;
        var cx = tx + halfWidth * Math.Cos(angle) - halfHeight * Math.Sin(angle);
        var cy = ty + halfWidth * Math.Sin(angle) + halfHeight * Math.Cos(angle);
        var degrees = (angle * 180 / Math.PI % 360 + 360) % 360;
        return (Math.Round(degrees, 4) % 360, Twip.FromPoints(cx - halfWidth), Twip.FromPoints(cy - halfHeight));
    }

    public XElement? Write(AnchoredObject item)
    {
        if (item.Content is not EmbeddedContent || item.Anchor is AsCharacterAnchor || item.Contour is not { Points.Count: >= 3 } contour) return null;
        var points = string.Join(' ', contour.Points.Select(point =>
            Number(point.X * Span) + "," + Number(point.Y * Span)));
        return new XElement(Draw + "contour-polygon",
            new XAttribute(Svg + "width", measure.Length(item.Size.Width)),
            new XAttribute(Svg + "height", measure.Length(item.Size.Height)),
            new XAttribute(Svg + "viewBox", $"0 0 {Span} {Span}"),
            new XAttribute(Draw + "points", points),
            new XAttribute(Draw + "recreate-on-edit", "false"));
    }

    public ObjectContour? Read(XElement frame)
    {
        if (frame.Element(Draw + "contour-polygon") is not { } polygon) return null;
        var box = ((string?)polygon.Attribute(Svg + "viewBox") ?? string.Empty).Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)
            .Select(token => double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0).ToArray();
        if (box.Length != 4 || box[2] <= 0 || box[3] <= 0) return null;
        var coordinates = ((string?)polygon.Attribute(Draw + "points") ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var points = new List<ContourPoint>();
        foreach (var pair in coordinates)
        {
            var parts = pair.Split(',');
            if (parts.Length != 2
                || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)) continue;
            points.Add(new ContourPoint(Math.Round((x - box[0]) / box[2], 4), Math.Round((y - box[1]) / box[3], 4)));
        }
        return points.Count >= 3 ? new ObjectContour(points) : null;
    }

    private string Number(double value) => Math.Round(value).ToString(CultureInfo.InvariantCulture);
}

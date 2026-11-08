using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfDrawUnits
{
    public string Length(Twip value) => $"{value.ToCentimeters().ToString("0.###", CultureInfo.InvariantCulture)}cm";

    public Twip Parse(string? text, double fallbackCentimeters = 0)
    {
        return new OdfLength().Parse(text, Twip.FromCentimeters(fallbackCentimeters));
    }

    public int Order(XElement element) =>
        int.TryParse((string?)element.Attribute(Namespaces.Draw + "z-index"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var order) ? order : -1;
}

internal readonly record struct DrawPosition(Twip X, Twip Y, double Degrees);

internal sealed class OdfDrawRotation
{
    private readonly OdfDrawUnits _units = new();

    public void Place(XElement element, Twip x, Twip y, Twip width, Twip height, double degrees)
    {
        if (Math.Abs(degrees) <= 0.001)
        {
            element.SetAttributeValue(Namespaces.Svg + "x", _units.Length(x));
            element.SetAttributeValue(Namespaces.Svg + "y", _units.Length(y));
            return;
        }

        var radians = -degrees * Math.PI / 180;
        var (cos, sin) = (Math.Cos(radians), Math.Sin(radians));
        var (halfWidth, halfHeight) = (width.Value / 2.0, height.Value / 2.0);
        var centreX = x.Value + halfWidth;
        var centreY = y.Value + halfHeight;
        var cornerX = new Twip((int)Math.Round(centreX - (cos * halfWidth + sin * halfHeight)));
        var cornerY = new Twip((int)Math.Round(centreY - (-sin * halfWidth + cos * halfHeight)));

        element.SetAttributeValue(Namespaces.Svg + "x", null);
        element.SetAttributeValue(Namespaces.Svg + "y", null);
        element.SetAttributeValue(Namespaces.Draw + "transform", string.Create(CultureInfo.InvariantCulture,
            $"rotate ({radians:0.##########}) translate ({_units.Length(cornerX)} {_units.Length(cornerY)})"));
    }

    public DrawPosition Read(XElement element, Twip width, Twip height)
    {
        var x = _units.Parse((string?)element.Attribute(Namespaces.Svg + "x"));
        var y = _units.Parse((string?)element.Attribute(Namespaces.Svg + "y"));
        var transform = (string?)element.Attribute(Namespaces.Draw + "transform");
        if (transform is null) return new DrawPosition(x, y, 0);

        var radians = Argument(transform, "rotate") is { } rotate && double.TryParse(rotate.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var angle) && double.IsFinite(angle) ? angle : 0;
        var degrees = (-radians * 180 / Math.PI % 360 + 360) % 360;

        if (Argument(transform, "translate") is not { } translate) return new DrawPosition(x, y, degrees);

        var parts = translate.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return new DrawPosition(x, y, degrees);

        var cornerX = _units.Parse(parts[0]).Value;
        var cornerY = parts.Length > 1 ? _units.Parse(parts[1]).Value : 0;
        var (cos, sin) = (Math.Cos(radians), Math.Sin(radians));
        var (halfWidth, halfHeight) = (width.Value / 2.0, height.Value / 2.0);
        var centreX = cornerX + cos * halfWidth + sin * halfHeight;
        var centreY = cornerY - sin * halfWidth + cos * halfHeight;

        return new DrawPosition(
            new Twip((int)Math.Round(centreX - halfWidth)),
            new Twip((int)Math.Round(centreY - halfHeight)),
            degrees);
    }

    private string? Argument(string transform, string name)
    {
        var start = transform.IndexOf(name, StringComparison.Ordinal);
        if (start < 0) return null;

        var open = transform.IndexOf('(', start);
        var close = open < 0 ? -1 : transform.IndexOf(')', open + 1);
        return open < 0 || close < 0 ? null : transform[(open + 1)..close];
    }
}

internal sealed class EnhancedPathWriter
{
    public const int ViewBox = 21600;

    private readonly PathReplay _replay = new();

    public string Write(ShapeOutline outline)
    {
        var sink = new EnhancedPathSink();
        _replay.Replay(outline.Scaled(ViewBox, ViewBox), sink);
        return string.Join(' ', sink.Parts.Append("N"));
    }

    private sealed class EnhancedPathSink : PathSink
    {
        public List<string> Parts { get; } = [];

        public override void MoveTo(ShapePoint to) => Parts.Add($"M {Point(to)}");

        public override void LineTo(ShapePoint to) => Parts.Add($"L {Point(to)}");

        public override void CubicTo(ShapePoint first, ShapePoint second, ShapePoint to) =>
            Parts.Add($"C {Point(first)} {Point(second)} {Point(to)}");

        public override void ArcTo(ShapePoint from, ArcSegment arc)
        {
        }

        public override void Close() => Parts.Add("Z");

        private string Point(ShapePoint point) => $"{(int)Math.Round(point.X)} {(int)Math.Round(point.Y)}";
    }
}

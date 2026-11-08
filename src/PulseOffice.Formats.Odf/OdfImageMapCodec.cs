using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Units;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

public sealed class OdfImageMapCodec
{

    private readonly IReadOnlyList<AreaCodec> _codecs = [new RectangleCodec(), new CircleCodec(), new PolygonCodec()];

    public XElement? Build(ImageMap map, DocumentSize frame)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (map.IsEmpty) return null;

        var size = new FrameSize(frame.Width.Value / PulseOffice.Foundation.Units.Twip.PerCentimeter, frame.Height.Value / PulseOffice.Foundation.Units.Twip.PerCentimeter);
        return new XElement(Namespaces.Draw + "image-map",
            map.Areas.Where(area => area.Points.Count >= 2)
                .Select(area => _codecs.First(codec => codec.Shape == area.Shape).Write(area, size)));
    }

    public ImageMap Read(XElement frame, DocumentSize size)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var map = new ImageMap();
        if (frame.Element(Namespaces.Draw + "image-map") is not { } element) return map;

        var frameSize = new FrameSize(size.Width.Value / PulseOffice.Foundation.Units.Twip.PerCentimeter, size.Height.Value / PulseOffice.Foundation.Units.Twip.PerCentimeter);
        var index = 0;
        foreach (var area in element.Elements())
            if (_codecs.FirstOrDefault(codec => codec.Element == area.Name) is { } codec
                && codec.Read(area, frameSize, $"area{++index}") is { } read)
                map.Add(read);

        return map;
    }

    internal readonly record struct FrameSize(double Width, double Height);

    internal abstract class AreaCodec
    {
        public abstract ImageMapShape Shape { get; }

        public abstract XName Element { get; }

        public XElement Write(ImageMapArea area, FrameSize size)
        {
            var element = new XElement(Element, Geometry(area, size));
            if (area.Target.Length > 0)
            {
                element.Add(new XAttribute(Namespaces.Xlink + "type", "simple"));
                element.Add(new XAttribute(Namespaces.Xlink + "href", area.Target));
            }
            if (area.Description.Length > 0) element.Add(new XElement(Namespaces.Svg + "desc", area.Description));
            return element;
        }

        public ImageMapArea? Read(XElement element, FrameSize size, string id)
        {
            if (size.Width <= 0 || size.Height <= 0) return null;
            var points = Points(element, size);
            return points.Count < 2 || points.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y))
                ? null
                : new ImageMapArea
                {
                    Id = id,
                    Shape = Shape,
                    Points = points,
                    Target = (string?)element.Attribute(Namespaces.Xlink + "href") ?? string.Empty,
                    Description = element.Element(Namespaces.Svg + "desc")?.Value ?? string.Empty,
                };
        }

        protected abstract IEnumerable<XAttribute> Geometry(ImageMapArea area, FrameSize size);

        protected abstract IReadOnlyList<(double X, double Y)> Points(XElement element, FrameSize size);

        protected string Centimetres(double value) => string.Create(CultureInfo.InvariantCulture, $"{value:0.####}cm");

        protected double Length(XElement element, XName name)
        {
            var text = ((string?)element.Attribute(name) ?? string.Empty).Trim();
            var (number, factor) = text.EndsWith("cm", StringComparison.Ordinal) ? (text[..^2], 1.0)
                : text.EndsWith("mm", StringComparison.Ordinal) ? (text[..^2], 0.1)
                : text.EndsWith("in", StringComparison.Ordinal) ? (text[..^2], 2.54)
                : text.EndsWith("pt", StringComparison.Ordinal) ? (text[..^2], 2.54 / 72)
                : (text, 1.0);
            return double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value * factor) ? value * factor : 0;
        }
    }

    private sealed class RectangleCodec : AreaCodec
    {
        public override ImageMapShape Shape => ImageMapShape.Rectangle;

        public override XName Element => Namespaces.Draw + "area-rectangle";

        protected override IEnumerable<XAttribute> Geometry(ImageMapArea area, FrameSize size)
        {
            var left = Math.Min(area.Points[0].X, area.Points[1].X);
            var top = Math.Min(area.Points[0].Y, area.Points[1].Y);
            yield return new XAttribute(Namespaces.Svg + "x", Centimetres(left * size.Width));
            yield return new XAttribute(Namespaces.Svg + "y", Centimetres(top * size.Height));
            yield return new XAttribute(Namespaces.Svg + "width", Centimetres(Math.Abs(area.Points[1].X - area.Points[0].X) * size.Width));
            yield return new XAttribute(Namespaces.Svg + "height", Centimetres(Math.Abs(area.Points[1].Y - area.Points[0].Y) * size.Height));
        }

        protected override IReadOnlyList<(double X, double Y)> Points(XElement element, FrameSize size)
        {
            var x = Length(element, Namespaces.Svg + "x") / size.Width;
            var y = Length(element, Namespaces.Svg + "y") / size.Height;
            return [(x, y), (x + Length(element, Namespaces.Svg + "width") / size.Width, y + Length(element, Namespaces.Svg + "height") / size.Height)];
        }
    }

    private sealed class CircleCodec : AreaCodec
    {
        public override ImageMapShape Shape => ImageMapShape.Circle;

        public override XName Element => Namespaces.Draw + "area-circle";

        protected override IEnumerable<XAttribute> Geometry(ImageMapArea area, FrameSize size)
        {
            var (cx, cy) = area.Points[0];
            var radius = Math.Sqrt(Math.Pow((area.Points[1].X - cx) * size.Width, 2) + Math.Pow((area.Points[1].Y - cy) * size.Height, 2));
            yield return new XAttribute(Namespaces.Svg + "cx", Centimetres(cx * size.Width));
            yield return new XAttribute(Namespaces.Svg + "cy", Centimetres(cy * size.Height));
            yield return new XAttribute(Namespaces.Svg + "r", Centimetres(radius));
        }

        protected override IReadOnlyList<(double X, double Y)> Points(XElement element, FrameSize size)
        {
            var cx = Length(element, Namespaces.Svg + "cx") / size.Width;
            var cy = Length(element, Namespaces.Svg + "cy") / size.Height;
            return [(cx, cy), (cx + Length(element, Namespaces.Svg + "r") / size.Width, cy)];
        }
    }

    private sealed class PolygonCodec : AreaCodec
    {
        private const double HundredthsPerCentimetre = 1000;

        public override ImageMapShape Shape => ImageMapShape.Polygon;

        public override XName Element => Namespaces.Draw + "area-polygon";

        protected override IEnumerable<XAttribute> Geometry(ImageMapArea area, FrameSize size)
        {
            var width = Math.Round(size.Width * HundredthsPerCentimetre);
            var height = Math.Round(size.Height * HundredthsPerCentimetre);
            yield return new XAttribute(Namespaces.Svg + "x", "0cm");
            yield return new XAttribute(Namespaces.Svg + "y", "0cm");
            yield return new XAttribute(Namespaces.Svg + "width", Centimetres(size.Width));
            yield return new XAttribute(Namespaces.Svg + "height", Centimetres(size.Height));
            yield return new XAttribute(Namespaces.Svg + "viewBox", string.Create(CultureInfo.InvariantCulture, $"0 0 {width} {height}"));
            yield return new XAttribute(Namespaces.Draw + "points", string.Join(' ', area.Points.Select(point =>
                string.Create(CultureInfo.InvariantCulture, $"{Math.Round(point.X * width)},{Math.Round(point.Y * height)}"))));
        }

        protected override IReadOnlyList<(double X, double Y)> Points(XElement element, FrameSize size) =>
        [
            .. ((string?)element.Attribute(Namespaces.Draw + "points") ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(pair => pair.Split(','))
                .Where(pair => pair.Length == 2)
                .Select(pair => (X: Coordinate(pair[0]), Y: Coordinate(pair[1])))
                .Where(point => point.X is not null && point.Y is not null)
                .Select(point => (
                    point.X!.Value / HundredthsPerCentimetre / size.Width,
                    point.Y!.Value / HundredthsPerCentimetre / size.Height)),
        ];

        private double? Coordinate(string text) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : null;
    }
}

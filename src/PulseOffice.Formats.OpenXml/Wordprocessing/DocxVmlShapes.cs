using System.Globalization;
using DocumentFormat.OpenXml;
using PulseOffice.Document.Embedding;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Drawing.Fontwork;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class VmlColors
{
    private readonly IReadOnlyDictionary<string, string> _named = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = "#000000", ["silver"] = "#C0C0C0", ["gray"] = "#808080", ["grey"] = "#808080", ["white"] = "#FFFFFF",
        ["maroon"] = "#800000", ["red"] = "#FF0000", ["purple"] = "#800080", ["fuchsia"] = "#FF00FF", ["green"] = "#008000",
        ["lime"] = "#00FF00", ["olive"] = "#808000", ["yellow"] = "#FFFF00", ["navy"] = "#000080", ["blue"] = "#0000FF",
        ["teal"] = "#008080", ["aqua"] = "#00FFFF",
    };

    public string? Hex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var token = value.Trim().Split([' ', '['], StringSplitOptions.RemoveEmptyEntries)[0];
        if (_named.TryGetValue(token, out var named)) return named;
        if (!token.StartsWith('#')) return null;
        var digits = token[1..];
        if (digits.Length == 3) digits = string.Concat(digits.Select(digit => new string(digit, 2)));
        return digits.Length == 6 && digits.All(Uri.IsHexDigit) ? "#" + digits.ToUpperInvariant() : null;
    }
}

public sealed record VmlExtent(Twip Left, Twip Top, Twip Width, Twip Height);

public sealed record VmlShapeRead(EmbeddedObject Value, BlockContainer? Story, VmlExtent? Extent);

public sealed record VmlGroupRead(ShapeGroupObject Group, IReadOnlyDictionary<string, BlockContainer> Stories);

public sealed class DocxVmlShapes(VmlStyle styles, VmlColors colors, IGroupStories groupStories, DocxVmlPictures pictures, IVmlPathParser paths, VmlShapePresets presets, FontworkWarps warps)
{
    private readonly IReadOnlySet<string> _members = new HashSet<string>(StringComparer.Ordinal) { "rect", "roundrect", "oval", "line", "shape", "group", "polyline", "curve" };

    private readonly IReadOnlySet<int> _connectors = new HashSet<int> { 20, 32, 33, 34, 35, 36, 37, 38, 39, 40 };

    private readonly string[] _curvePoints = ["from", "control1", "control2", "to"];

    private const string VmlNamespace = "urn:schemas-microsoft-com:vml";

    private const string OfficeNamespace = "urn:schemas-microsoft-com:office:office";

    public bool Reads(W.Picture picture) => Root(picture) is not null;

    public OpenXmlElement? Root(W.Picture picture) =>
        picture.ChildElements.FirstOrDefault(child => child.LocalName == "group" && child.NamespaceUri == VmlNamespace);

    public VmlGroupRead? Group(OpenXmlElement group, DocxReadContext context, DocxBlockSource blocks)
    {
        var stories = new Dictionary<string, BlockContainer>(StringComparer.Ordinal);
        var origin = Pair(Attribute(group, "coordorigin"), 0, 0);
        var span = Pair(Attribute(group, "coordsize"), 1000, 1000);
        var spanX = Math.Max(1, span.X);
        var spanY = Math.Max(1, span.Y);
        var children = new List<GroupChild>();
        foreach (var child in group.ChildElements.Where(child => child.NamespaceUri == VmlNamespace && _members.Contains(child.LocalName)))
        {
            var style = styles.Parse(Attribute(child, "style"));
            var box = Box(child, style);
            EmbeddedObject? member;
            if (child.LocalName == "group")
            {
                if (Group(child, context, blocks) is not { } nested) continue;
                foreach (var (id, story) in nested.Stories) stories[id] = story;
                member = nested.Group;
            }
            else if (pictures.Holds(child)) member = pictures.Read(child, context);
            else member = Shape(child, style, context, blocks, stories);
            if (member is null) continue;
            children.Add(new GroupChild(member, (box.X - origin.X) / spanX, (box.Y - origin.Y) / spanY, box.Width / spanX, box.Height / spanY)
            {
                RotationDegrees = Number(style, "rotation"),
            });
        }
        return children.Count == 0 ? null : new VmlGroupRead(new ShapeGroupObject { Id = context.Document.NextId("obj"), Children = children }, stories);
    }

    public OpenXmlElement? Drawable(W.Picture picture) =>
        picture.ChildElements.FirstOrDefault(child => child.NamespaceUri == VmlNamespace && child.LocalName != "group" && _members.Contains(child.LocalName)
            && !pictures.Holds(child) && !PlainTextBox(child));

    private bool PlainTextBox(OpenXmlElement element) =>
        element.ChildElements.Any(inner => inner.LocalName == "textbox" && inner.NamespaceUri == VmlNamespace)
        && Attribute(element, "filled") is "f" or "false" && Attribute(element, "stroked") is "f" or "false";

    public bool DefinesOnly(W.Picture picture) =>
        picture.ChildElements.All(child => child.LocalName == "shapetype" && child.NamespaceUri == VmlNamespace);

    public VmlShapeRead? Single(OpenXmlElement element, DocxReadContext context, DocxBlockSource blocks)
    {
        var stories = new Dictionary<string, BlockContainer>(StringComparer.Ordinal);
        var style = styles.Parse(Attribute(element, "style"));
        if (Shape(element, style, context, blocks, stories) is not { } shape) return null;
        var rotation = Number(style, "rotation");
        EmbeddedObject rotated = shape switch
        {
            ShapeObject plain => plain with { RotationDegrees = rotation },
            FreeformObject freeform => freeform with { RotationDegrees = rotation },
            _ => shape,
        };
        return new VmlShapeRead(rotated, stories.GetValueOrDefault(shape.Id), Extent(element));
    }

    private EmbeddedObject? Shape(OpenXmlElement element, IReadOnlyDictionary<string, string> style, DocxReadContext context, DocxBlockSource blocks, Dictionary<string, BlockContainer> stories)
    {
        var id = context.Document.NextId("obj");
        var type = Type(element);
        if (Fontwork(element, type, id) is { } fontwork) return fontwork;
        var content = element.ChildElements.FirstOrDefault(child => child.LocalName == "textbox")?.GetFirstChild<W.TextBoxContent>();
        if (Outline(element, type) is { } path)
        {
            if (content is not null) stories[id] = new BlockContainer(blocks(content.ChildElements));
            return new FreeformObject { Id = id, Path = path, Style = Style(element, type, style, !path.Filled) };
        }
        var kind = Kind(element, type, context);
        var flip = style.TryGetValue("flip", out var flipped) ? flipped : string.Empty;
        var shape = new ShapeObject
        {
            Id = id,
            Shape = kind,
            FlipHorizontal = flip.Contains('x', StringComparison.Ordinal),
            FlipVertical = flip.Contains('y', StringComparison.Ordinal) ^ (element.LocalName == "line" && Rising(element)),
            Style = Style(element, type, style, kind == ShapeKind.Line),
        };
        if (content is null) return shape;
        var story = new BlockContainer(blocks(content.ChildElements));
        var plain = groupStories.Member(id, new TextBoxContent(story));
        stories[id] = story;
        return shape with { Text = ((ShapeObject)plain.Value).Text };
    }

    private ShapeStyle Style(OpenXmlElement element, OpenXmlElement? type, IReadOnlyDictionary<string, string> style, bool open)
    {
        var stroke = element.ChildElements.FirstOrDefault(child => child.LocalName == "stroke" && child.NamespaceUri == VmlNamespace);
        var filled = !open && Switched(element, type, "filled");
        var stroked = Attribute(element, "stroked") is not ("f" or "false" or "0");
        return new ShapeStyle
        {
            Fill = filled ? new ShapeFill { ColorHex = colors.Hex(Attribute(element, "fillcolor")) ?? colors.Hex(Attribute(type, "fillcolor")) ?? "#FFFFFF" } : ShapeFill.None,
            Line = stroked
                ? new ShapeLine
                {
                    ColorHex = colors.Hex(Attribute(element, "strokecolor")) ?? colors.Hex(Attribute(type, "strokecolor")) ?? "#000000",
                    Width = styles.Length(new Dictionary<string, string> { ["weight"] = Attribute(element, "strokeweight") ?? "0.75pt" }, "weight") ?? Twip.FromPoints(0.75),
                    StartArrow = Arrow(Attribute(stroke, "startarrow")),
                    EndArrow = Arrow(Attribute(stroke, "endarrow")),
                }
                : ShapeLine.None,
            Text = new ShapeTextStyle
            {
                ColorHex = "#000000",
                Vertical = style.TryGetValue("v-text-anchor", out var anchor) && anchor.StartsWith("middle", StringComparison.Ordinal) ? TextVerticalPlacement.Middle
                    : anchor?.StartsWith("bottom", StringComparison.Ordinal) == true ? TextVerticalPlacement.Bottom : TextVerticalPlacement.Top,
                Inset = Twip.FromPoints(3.6),
            },
        };
    }

    private bool Switched(OpenXmlElement element, OpenXmlElement? type, string name) =>
        (Attribute(element, name) ?? Attribute(type, name)) is not ("f" or "false" or "0");

    private FontworkObject? Fontwork(OpenXmlElement element, OpenXmlElement? type, string id)
    {
        if (element.ChildElements.FirstOrDefault(child => child.LocalName == "textpath" && child.NamespaceUri == VmlNamespace) is not { } textpath
            || Attribute(textpath, "string") is not { Length: > 0 } text
            || element.ChildElements.Any(child => child.LocalName == "textbox" && child.NamespaceUri == VmlNamespace))
            return null;
        var font = styles.Parse(Attribute(textpath, "style"));
        var fill = colors.Hex(Attribute(element, "fillcolor"));
        return new FontworkObject
        {
            Id = id,
            Text = text,
            Warp = warps.FromOoxml(presets.TextWarp(Spt(element, type))),
            FontFamily = font.TryGetValue("font-family", out var family) && family.Trim('"', '\'', ' ') is { Length: > 0 } name ? name : "Arial",
            Bold = font.TryGetValue("font-weight", out var weight) && weight is "bold" or "bolder" or "600" or "700" or "800" or "900",
            Italic = font.TryGetValue("font-style", out var slant) && slant is "italic" or "oblique",
            Style = fill is null ? ShapeStyle.Default : ShapeStyle.Default with { Fill = ShapeStyle.Default.Fill with { ColorHex = fill } },
        };
    }

    private FreeformPath? Outline(OpenXmlElement element, OpenXmlElement? type)
    {
        var filled = Switched(element, type, "filled");
        switch (element.LocalName)
        {
            case "polyline":
                return Polyline(Points(Attribute(element, "points")), filled);
            case "curve":
                return Curve(element);
            case "shape":
                if (Attribute(element, "connectortype", OfficeNamespace) is not null) return null;
                var spt = Spt(element, type);
                if (presets.Shape(spt) is not null || _connectors.Contains(spt)) return null;
                var path = Attribute(element, "path") ?? PathChild(element) ?? Attribute(type, "path") ?? PathChild(type);
                var size = Pair(Attribute(element, "coordsize") ?? Attribute(type, "coordsize"), 1000, 1000);
                var origin = Pair(Attribute(element, "coordorigin") ?? Attribute(type, "coordorigin"), 0, 0);
                return paths.Parse(path, new VmlCoordinateSpace(origin.X, origin.Y, size.X, size.Y), filled);
            default:
                return null;
        }
    }

    private string? PathChild(OpenXmlElement? element) =>
        Attribute(element?.ChildElements.FirstOrDefault(child => child.LocalName == "path" && child.NamespaceUri == VmlNamespace), "v");

    private FreeformPath? Polyline(IReadOnlyList<(double X, double Y)> points, bool filled)
    {
        if (points.Count < 2) return null;
        var space = Space(points);
        var closed = points.Count > 2 && points[0] == points[^1];
        var nodes = (closed ? points.Take(points.Count - 1) : points).Select(point => new PathNode(space.Normalise(point.X, point.Y))).ToList();
        return new FreeformPath([new PathFigure(nodes) { Closed = closed }]) { Filled = filled && closed };
    }

    private FreeformPath? Curve(OpenXmlElement element)
    {
        var points = _curvePoints.Select(name => Points(Attribute(element, name)).FirstOrDefault()).ToList();
        var space = Space(points);
        return new FreeformPath([new PathFigure([
            new PathNode(space.Normalise(points[0].X, points[0].Y)) { After = space.Normalise(points[1].X, points[1].Y), Kind = PathNodeKind.Smooth },
            new PathNode(space.Normalise(points[3].X, points[3].Y)) { Before = space.Normalise(points[2].X, points[2].Y), Kind = PathNodeKind.Smooth },
        ])]);
    }

    private VmlCoordinateSpace Space(IReadOnlyList<(double X, double Y)> points)
    {
        var left = points.Min(point => point.X);
        var top = points.Min(point => point.Y);
        return new VmlCoordinateSpace(left, top, Math.Max(1, points.Max(point => point.X) - left), Math.Max(1, points.Max(point => point.Y) - top));
    }

    private VmlExtent? Extent(OpenXmlElement element)
    {
        List<(double X, double Y)> points = element.LocalName switch
        {
            "line" => [.. Points(Attribute(element, "from") ?? "0,0"), .. Points(Attribute(element, "to") ?? "0,0")],
            "polyline" => Points(Attribute(element, "points")),
            "curve" => [.. _curvePoints.SelectMany(name => Points(Attribute(element, name)))],
            _ => [],
        };
        if (points.Count == 0) return null;
        var left = points.Min(point => point.X);
        var top = points.Min(point => point.Y);
        return new VmlExtent(Twips(left), Twips(top), Twips(points.Max(point => point.X) - left), Twips(points.Max(point => point.Y) - top));
    }

    private Twip Twips(double value) => new((int)Math.Round(value));

    private bool Rising(OpenXmlElement element)
    {
        var from = Points(Attribute(element, "from") ?? "0,0").FirstOrDefault();
        var to = Points(Attribute(element, "to") ?? "0,0").FirstOrDefault();
        return (to.X - from.X) * (to.Y - from.Y) < 0;
    }

    private List<(double X, double Y)> Points(string? text)
    {
        var values = (text ?? string.Empty).Split([',', ' '], StringSplitOptions.RemoveEmptyEntries).Select(Measure).ToList();
        var points = new List<(double X, double Y)>(values.Count / 2);
        for (var index = 0; index + 1 < values.Count; index += 2) points.Add((values[index], values[index + 1]));
        return points;
    }

    private double Measure(string token)
    {
        var length = char.IsLetter(token[^1]) ? token : token + "px";
        return styles.Length(new Dictionary<string, string> { ["value"] = length }, "value")?.Value ?? 0;
    }

    private int Spt(OpenXmlElement element, OpenXmlElement? type) =>
        int.TryParse(Attribute(type, "spt", OfficeNamespace) ?? Attribute(element, "spt", OfficeNamespace), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : -1;

    private ShapeKind Kind(OpenXmlElement element, OpenXmlElement? type, DocxReadContext context)
    {
        switch (element.LocalName)
        {
            case "rect": return ShapeKind.Rectangle;
            case "roundrect": return ShapeKind.RoundedRectangle;
            case "oval": return ShapeKind.Ellipse;
            case "line" or "polyline" or "curve": return ShapeKind.Line;
        }
        var spt = Spt(element, type);
        if (Attribute(element, "connectortype", OfficeNamespace) is { } connector)
        {
            if (connector != "straight") context.Losses.Add("DOCX141");
            return ShapeKind.Line;
        }
        if (_connectors.Contains(spt))
        {
            if (spt != 20 && spt != 32) context.Losses.Add("DOCX141");
            return ShapeKind.Line;
        }
        if (presets.Shape(spt) is { } kind) return kind;
        context.Losses.Add("DOCX140");
        return ShapeKind.Rectangle;
    }

    private OpenXmlElement? Type(OpenXmlElement element)
    {
        if (Attribute(element, "type") is not { Length: > 1 } reference) return null;
        var name = reference.TrimStart('#');
        return element.Parent?.ChildElements.FirstOrDefault(sibling => sibling.LocalName == "shapetype" && Attribute(sibling, "id") == name)
            ?? element.Ancestors().LastOrDefault()?.Descendants().FirstOrDefault(other => other.LocalName == "shapetype" && Attribute(other, "id") == name);
    }

    private ArrowEnd Arrow(string? value) => value switch
    {
        null or "none" => ArrowEnd.None,
        "open" => ArrowEnd.OpenArrow,
        "oval" => ArrowEnd.Circle,
        "diamond" => ArrowEnd.Diamond,
        _ => ArrowEnd.Arrow,
    };

    private (double X, double Y, double Width, double Height) Box(OpenXmlElement element, IReadOnlyDictionary<string, string> style)
    {
        if (element.LocalName == "line")
        {
            var from = Pair(Attribute(element, "from"), 0, 0);
            var to = Pair(Attribute(element, "to"), 0, 0);
            return (Math.Min(from.X, to.X), Math.Min(from.Y, to.Y), Math.Abs(to.X - from.X), Math.Abs(to.Y - from.Y));
        }
        if (element.LocalName is "polyline" or "curve" && !style.ContainsKey("width"))
        {
            var numbers = (element.LocalName == "polyline" ? [Attribute(element, "points")] : _curvePoints.Select(name => Attribute(element, name)))
                .SelectMany(text => (text ?? string.Empty).Split([',', ' '], StringSplitOptions.RemoveEmptyEntries))
                .Select(token => double.TryParse(token.TrimEnd('p', 't', 'x', 'i', 'n', 'c', 'm'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : 0)
                .ToList();
            var xs = numbers.Where((_, index) => index % 2 == 0).DefaultIfEmpty(0).ToList();
            var ys = numbers.Where((_, index) => index % 2 == 1).DefaultIfEmpty(0).ToList();
            return (xs.Min(), ys.Min(), xs.Max() - xs.Min(), ys.Max() - ys.Min());
        }
        return (Number(style, "left"), Number(style, "top"), Number(style, "width"), Number(style, "height"));
    }

    private double Number(IReadOnlyDictionary<string, string> style, string name) =>
        style.TryGetValue(name, out var text) && double.TryParse(text.TrimEnd('f', 'd', 'e', 'g'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : 0;

    private (double X, double Y) Pair(string? text, double x, double y)
    {
        var parts = (text ?? string.Empty).Split(',');
        double Part(int index, double fallback) =>
            parts.Length > index && double.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : fallback;
        return (Part(0, x), Part(1, y));
    }

    private string? Attribute(OpenXmlElement? element, string name, string? namespaceUri = null) =>
        element?.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == name && (namespaceUri is null || attribute.NamespaceUri == namespaceUri)).Value;
}

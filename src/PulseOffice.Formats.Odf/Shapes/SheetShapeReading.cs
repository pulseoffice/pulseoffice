using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf.TextCodecs;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal sealed class SheetShapeReadContext(
    IReadOnlyDictionary<string, XElement> styles, IReadOnlyDictionary<string, XElement> definitions, OdfShapeStyleMapper styleMapper,
    IReadOnlyDictionary<string, XElement> textStyles, Func<string, byte[]?>? readBytes = null, IReadOnlyDictionary<string, XElement>? listStyles = null)
{
    public OdfShapeListSource Lists { get; } = new(listStyles ?? new Dictionary<string, XElement>(), readBytes is { } reader ? href => reader(href.TrimStart('.', '/')) : null);

    public byte[]? ReadBytes(string href) => readBytes?.Invoke(href.TrimStart('.', '/'));

    private int _ordinal;

    public OdfDrawUnits Units { get; } = new();
    public OdfDrawRotation Rotation { get; } = new();
    public IShapePresets Presets { get; } = new ShapePresets();

    public int Next() => ++_ordinal;

    public ShapeStyle Style(string? name) =>
        styleMapper.Read(name is not null && styles.TryGetValue(name, out var declared) ? declared : null, definitions);

    public XElement? GraphicStyle(string name) =>
        styles.TryGetValue(name, out var declared) ? declared : definitions.TryGetValue(name, out var defined) ? defined : null;

    public XElement? TextStyle(string name, string family) => textStyles.TryGetValue(family + ":" + name, out var declared) ? declared : null;

    public bool Declares(string? name, XName properties, XName attribute) =>
        name is not null && styles.TryGetValue(name, out var declared)
        && declared.Element(properties)?.Attribute(attribute) is not null;
}

internal abstract class SheetShapeReader
{
    public abstract bool Reads(XElement element);

    public abstract PlacedObject? Read(XElement element, CellAddress anchor, SheetShapeReadContext context);

    protected PlacedObject Ordered(PlacedObject placed, XElement element, SheetShapeReadContext context) =>
        context.Units.Order(element) is var order and >= 0 ? placed with { ZOrder = order } : placed;
}

internal sealed class SheetFontworkReader : SheetShapeReader
{
    private readonly OdfFontworkGeometry _geometry = new();

    public override bool Reads(XElement element) => element.Name == Namespaces.Draw + "custom-shape" && _geometry.IsFontwork(element);

    public override PlacedObject? Read(XElement element, CellAddress anchor, SheetShapeReadContext context)
    {
        var ordinal = context.Next();
        var units = context.Units;
        var fontwork = new PulseOffice.Drawing.Fontwork.FontworkObject
        {
            Id = (string?)element.Attribute(Namespaces.Draw + "name") ?? $"fontwork{ordinal}",
            Text = _geometry.TextOf(element),
            Warp = _geometry.WarpOf(element),
            Style = context.Style((string?)element.Attribute(Namespaces.Draw + "style-name")),
        };
        var size = new DocumentSize(
            units.Parse((string?)element.Attribute(Namespaces.Svg + "width"), 8),
            units.Parse((string?)element.Attribute(Namespaces.Svg + "height"), 3));
        var position = context.Rotation.Read(element, size.Width, size.Height);
        return Ordered(new PlacedObject
        {
            Id = fontwork.Id,
            Content = fontwork,
            Anchor = anchor,
            OffsetX = position.X,
            OffsetY = position.Y,
            Size = size,
        }, element, context);
    }
}

internal sealed class SheetCustomShapeReader(IOdfParagraphText paragraphs) : SheetShapeReader
{
    private readonly OdfArcGeometry _arcs = new();
    private readonly OdfComputedGeometry _computed = new();
    private readonly OdfShapeTextRuns _links = new(paragraphs);

    public override bool Reads(XElement element) => element.Name == Namespaces.Draw + "custom-shape";

    public override PlacedObject? Read(XElement element, CellAddress anchor, SheetShapeReadContext context) =>
        Ordered(Shape(element, anchor, context), element, context);

    public PlacedObject Shape(XElement custom, CellAddress anchor, SheetShapeReadContext context)
    {
        var ordinal = context.Next();
        var geometry = custom.Element(Namespaces.Draw + "enhanced-geometry");
        var text = paragraphs.Read(custom);

        var shape = _arcs.Read(_links.Read(custom, new ShapeObject
        {
            Id = (string?)custom.Attribute(Namespaces.Draw + "name") ?? (string?)custom.Parent?.Attribute(Namespaces.Draw + "name") ?? $"shape{ordinal}",
            Shape = context.Presets.KindOf((string?)geometry?.Attribute(Namespaces.Draw + "type")),
            FlipHorizontal = (string?)geometry?.Attribute(Namespaces.Draw + "mirror-horizontal") == "true",
            FlipVertical = (string?)geometry?.Attribute(Namespaces.Draw + "mirror-vertical") == "true",
            Text = text.Length > 0 ? text : null,
            Style = new OdfExtrusion().Read(geometry, context.Style((string?)custom.Attribute(Namespaces.Draw + "style-name") ?? (string?)custom.Parent?.Attribute(Namespaces.Draw + "style-name"))),
        }, context.TextStyle, context.Lists), geometry);

        var box = custom.Attribute(Namespaces.Svg + "width") is not null ? custom : custom.Parent!;
        var units = context.Units;
        var size = new DocumentSize(
            units.Parse((string?)box.Attribute(Namespaces.Svg + "width"), 4),
            units.Parse((string?)box.Attribute(Namespaces.Svg + "height"), 3));
        var position = context.Rotation.Read(box, size.Width, size.Height);
        shape = _computed.Apply(shape, geometry, size.Width, size.Height);
        return new PlacedObject
        {
            Id = shape.Id,
            Content = shape with { RotationDegrees = shape.FlipHorizontal != shape.FlipVertical ? (360 - position.Degrees) % 360 : position.Degrees },
            Anchor = anchor,
            OffsetX = position.X,
            OffsetY = position.Y,
            Size = size,
        };
    }
}

internal sealed class SheetCustomPathReader : SheetShapeReader
{
    private readonly XName _custom = Namespaces.Draw + "custom-shape";
    private readonly OdfLiteralEnhancedPath _paths = new(new ShapePresets());

    public override bool Reads(XElement element) =>
        element.Name == _custom && _paths.Unknown(element) && _paths.Outline(element) is not null;

    public override PlacedObject? Read(XElement element, CellAddress anchor, SheetShapeReadContext context)
    {
        if (_paths.Outline(element) is not { } outline) return null;

        var ordinal = context.Next();
        var path = FreeformPath.FromOutline(outline);
        var units = context.Units;
        var size = new DocumentSize(
            units.Parse((string?)element.Attribute(Namespaces.Svg + "width"), 4),
            units.Parse((string?)element.Attribute(Namespaces.Svg + "height"), 3));
        var position = context.Rotation.Read(element, size.Width, size.Height);
        var id = (string?)element.Attribute(Namespaces.Draw + "name") ?? $"shape{ordinal}";

        return Ordered(new PlacedObject
        {
            Id = id,
            Anchor = anchor,
            OffsetX = position.X,
            OffsetY = position.Y,
            Size = size,
            ZOrder = ordinal,
            Content = new FreeformObject
            {
                Id = id,
                Style = context.Style((string?)element.Attribute(Namespaces.Draw + "style-name")),
                Path = path,
                RotationDegrees = position.Degrees,
            },
        }, element, context);
    }
}

internal sealed class SheetFramedShapeReader(SheetCustomShapeReader shapes) : SheetShapeReader
{
    public override bool Reads(XElement element) =>
        element.Name == Namespaces.Draw + "frame" && element.Element(Namespaces.Draw + "custom-shape") is not null;

    public override PlacedObject? Read(XElement element, CellAddress anchor, SheetShapeReadContext context) =>
        shapes.Shape(element.Element(Namespaces.Draw + "custom-shape")!, anchor, context);
}

internal sealed class SheetFreeformShapeReader : SheetShapeReader
{
    private readonly XName[] _names = [Namespaces.Draw + "path", Namespaces.Draw + "polygon", Namespaces.Draw + "polyline"];

    public override bool Reads(XElement element) => _names.Contains(element.Name);

    public override PlacedObject? Read(XElement element, CellAddress anchor, SheetShapeReadContext context)
    {
        var ordinal = context.Next();
        var box = ViewBoxOf(element);
        var path = element.Attribute(Namespaces.Draw + "points") is { } points
            ? FromPoints(points.Value, element.Name.LocalName == "polygon", box)
            : FromOutline(new SvgPathParser().Parse((string?)element.Attribute(Namespaces.Svg + "d") ?? string.Empty), box);

        var id = (string?)element.Attribute(Namespaces.Draw + "name") ?? ordinal.ToString(CultureInfo.InvariantCulture);
        var units = context.Units;

        var size = new DocumentSize(
            units.Parse((string?)element.Attribute(Namespaces.Svg + "width")),
            units.Parse((string?)element.Attribute(Namespaces.Svg + "height")));
        var position = context.Rotation.Read(element, size.Width, size.Height);

        return Ordered(new PlacedObject
        {
            Id = id,
            Anchor = anchor,
            OffsetX = position.X,
            OffsetY = position.Y,
            Size = size,
            ZOrder = ordinal,
            Content = new FreeformObject { Id = id, Style = context.Style((string?)element.Attribute(Namespaces.Draw + "style-name")), Path = path, RotationDegrees = position.Degrees },
        }, element, context);
    }

    private FreeformPath FromPoints(string text, bool closed, double box)
    {
        var nodes = new List<PathNode>();
        foreach (var pair in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split(',');
            if (parts.Length != 2) continue;
            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)) continue;
            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)) continue;
            nodes.Add(new PathNode(new ShapePoint(x / box, y / box)));
        }
        return nodes.Count == 0 ? FreeformPath.Empty : new FreeformPath([new PathFigure(nodes) { Closed = closed }]);
    }

    private double ViewBoxOf(XElement element)
    {
        var parts = ((string?)element.Attribute(Namespaces.Svg + "viewBox"))?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts is { Length: 4 } && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var width) && width > 0
            ? width
            : EnhancedPathWriter.ViewBox;
    }

    private FreeformPath FromOutline(ShapeOutline outline, double box)
    {
        var path = FreeformPath.FromOutline(new SegmentMapper(point => new(point.X / box, point.Y / box)).Map(outline));
        return path with { Figures = [.. path.Figures.Select(WithoutRepeatedEnd)] };
    }

    private PathFigure WithoutRepeatedEnd(PathFigure figure)
    {
        var nodes = figure.Nodes;
        return nodes.Count > 1 && figure.Closed
            && Math.Abs(nodes[0].Point.X - nodes[^1].Point.X) < 1e-6 && Math.Abs(nodes[0].Point.Y - nodes[^1].Point.Y) < 1e-6
            ? figure with { Nodes = [.. nodes.Take(nodes.Count - 1)] }
            : figure;
    }
}

internal sealed class SheetConnectorShapeReader : SheetShapeReader
{
    public override bool Reads(XElement element) => element.Name == Namespaces.Draw + "connector";

    public override PlacedObject? Read(XElement element, CellAddress anchor, SheetShapeReadContext context)
    {
        var ordinal = context.Next();
        var units = context.Units;
        var id = (string?)element.Attribute(Namespaces.Draw + "name") ?? $"connettore{ordinal}";

        var x1 = units.Parse((string?)element.Attribute(Namespaces.Svg + "x1")).Value;
        var y1 = units.Parse((string?)element.Attribute(Namespaces.Svg + "y1")).Value;
        var x2 = units.Parse((string?)element.Attribute(Namespaces.Svg + "x2")).Value;
        var y2 = units.Parse((string?)element.Attribute(Namespaces.Svg + "y2")).Value;

        var left = Math.Min(x1, x2);
        var top = Math.Min(y1, y2);
        var width = Math.Max(1, Math.Abs(x2 - x1));
        var height = Math.Max(1, Math.Abs(y2 - y1));

        var connector = new ConnectorObject
        {
            Id = id,
            Shape = (string?)element.Attribute(Namespaces.Draw + "type") switch
            {
                "line" => ConnectorKind.Straight,
                "curve" => ConnectorKind.Curved,
                _ => ConnectorKind.Elbow,
            },
            Start = new ConnectorEndpoint((string?)element.Attribute(Namespaces.Draw + "start-shape")),
            End = new ConnectorEndpoint((string?)element.Attribute(Namespaces.Draw + "end-shape")),
            Style = context.Style((string?)element.Attribute(Namespaces.Draw + "style-name")),
            Path = new ShapeOutline([new ShapeFigure(
            [
                new MoveSegment(new ShapePoint((x1 - left) / (double)width, (y1 - top) / (double)height)),
                new LineSegment(new ShapePoint((x2 - left) / (double)width, (y2 - top) / (double)height)),
            ])]),
        };

        return Ordered(new PlacedObject
        {
            Id = id,
            Content = connector,
            Anchor = anchor,
            OffsetX = new Twip(left),
            OffsetY = new Twip(top),
            Size = new DocumentSize(new Twip(width), new Twip(height)),
        }, element, context);
    }
}

internal sealed class SheetGroupShapeReader(Func<IReadOnlyList<SheetShapeReader>> readers) : SheetShapeReader
{
    private const int MaximumDepth = 64;

    public override bool Reads(XElement element) => element.Name == Namespaces.Draw + "g";

    public override PlacedObject? Read(XElement element, CellAddress anchor, SheetShapeReadContext context) =>
        Group(element, anchor, context, 0) is { } group ? Ordered(group, element, context) : null;

    private PlacedObject? Group(XElement element, CellAddress anchor, SheetShapeReadContext context, int depth)
    {
        var ordinal = context.Next();
        var members = new List<(EmbeddedObject Content, PlacementBox Box)>();

        foreach (var child in element.Elements())
        {
            if (Reads(child) && depth >= MaximumDepth) continue;
            var read = Reads(child)
                ? Group(child, anchor, context, depth + 1)
                : readers().FirstOrDefault(reader => reader.Reads(child))?.Read(child, anchor, context);
            if (read is not null) members.Add((read.Content, new PlacementBox(read.OffsetX, read.OffsetY, read.Size.Width, read.Size.Height)));
        }

        if (members.Count == 0) return null;

        var left = members.Min(member => member.Box.Left.Value);
        var top = members.Min(member => member.Box.Top.Value);
        var width = Math.Max(1, members.Max(member => member.Box.Right.Value) - left);
        var height = Math.Max(1, members.Max(member => member.Box.Bottom.Value) - top);

        var children = members.Select(member => new GroupChild(
            member.Content,
            (member.Box.Left.Value - left) / (double)width,
            (member.Box.Top.Value - top) / (double)height,
            member.Box.Width.Value / (double)width,
            member.Box.Height.Value / (double)height)).ToList();

        var id = (string?)element.Attribute(Namespaces.Draw + "name") ?? $"gruppo{ordinal}";
        return new PlacedObject
        {
            Id = id,
            Content = new ShapeGroupObject { Id = id, Children = children },
            Anchor = anchor,
            OffsetX = new Twip(left),
            OffsetY = new Twip(top),
            Size = new DocumentSize(new Twip(width), new Twip(height)),
        };
    }
}

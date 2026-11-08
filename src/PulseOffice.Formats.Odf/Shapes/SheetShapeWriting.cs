using System.Xml.Linq;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf.TextCodecs;
using PulseOffice.Foundation.Units;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal readonly record struct ShapeSlot(Twip X, Twip Y, Twip Width, Twip Height, PulseOffice.Sheets.CellPoint EndCell, string Sheet)
{
    public ShapeSlot Child(GroupChild child, double groupDegrees)
    {
        var (x, y, width, height) = child.Scaled(Width.Value, Height.Value);
        var (centreX, centreY) = (X.Value + x + width / 2, Y.Value + y + height / 2);
        if (groupDegrees != 0 && double.IsFinite(groupDegrees))
        {
            var radians = groupDegrees * Math.PI / 180;
            var (dx, dy) = (centreX - (X.Value + Width.Value / 2.0), centreY - (Y.Value + Height.Value / 2.0));
            centreX = X.Value + Width.Value / 2.0 + dx * Math.Cos(radians) - dy * Math.Sin(radians);
            centreY = Y.Value + Height.Value / 2.0 + dx * Math.Sin(radians) + dy * Math.Cos(radians);
        }

        return new ShapeSlot(
            new Twip((int)Math.Round(centreX - width / 2)), new Twip((int)Math.Round(centreY - height / 2)),
            new Twip(Math.Max(1, (int)Math.Round(width))), new Twip(Math.Max(1, (int)Math.Round(height))), EndCell, Sheet);
    }
}

internal sealed class SheetShapeServices(
    OdfShapeStyleMapper styleMapper, List<XElement> styles, IShapePresets presets, IShapeGeometry geometry,
    IArcFlattener flattener, IShapePathWriter paths, IOdfParagraphText paragraphs, IOdfPictureStore? pictures = null)
{
    public IOdfPictureStore? Pictures => pictures;

    public OdfDrawUnits Units { get; } = new();
    public OdfDrawRotation Rotation { get; } = new();
    public EnhancedPathWriter EnhancedPath { get; } = new();
    public IShapePresets Presets => presets;
    public IShapeGeometry Geometry => geometry;
    public IArcFlattener Flattener => flattener;
    public IOdfParagraphText Paragraphs => paragraphs;
    public OdfShapeTextRuns TextRuns { get; } = new(paragraphs);

    public IOdfTextStyleSink TextStyles { get; } = new OdfTextStyleSink(() => $"ST{styles.Count + 1}", styles.Add);
    public IShapePathWriter Paths => paths;
    public OdfFontworkGeometry Fontwork { get; } = new();
    public OdfComputedGeometry Computed { get; } = new();

    public OdfDimensionLines Dimensions { get; } = new();

    public string DimensionStyle(ShapeStyle style, OdfPlacedDimension placed)
    {
        var name = $"gr{styles.Count + 1}";
        var built = styleMapper.Build(name, style);
        Dimensions.WriteStyle(built, placed);
        styles.Add(built);
        return name;
    }

    public string Style(ShapeStyle style)
    {
        var name = $"gr{styles.Count + 1}";
        styles.Add(styleMapper.Build(name, style));
        return name;
    }

    public string FontworkStyle(ShapeStyle style, Twip height)
    {
        var name = $"gr{styles.Count + 1}";
        styles.Add(Fontwork.Lettering(styleMapper.Build(name, style), height.Value));
        return name;
    }
}

internal sealed class SheetShapeElementVisitor(SheetShapeServices services, string id, ShapeSlot slot, double extraRotation = 0, bool inGroup = false) : DrawingObjectVisitor<XElement?>
{
    private const string TypePrefix = "ooxml-";

    private readonly OdfArcGeometry _arcs = new();

    public override XElement? VisitShape(ShapeObject shape)
    {
        if (shape.Dimension is { } dimension)
        {
            var placed = services.Dimensions.Placed(dimension, new DocumentSize(slot.Width, slot.Height), shape.RotationDegrees + extraRotation, shape.FlipHorizontal, shape.FlipVertical);
            return services.Dimensions.Build(new DocumentRect(slot.X, slot.Y, slot.Width, slot.Height), placed, services.DimensionStyle(shape.Style, placed), id,
                new XAttribute(Namespaces.Draw + "name", id), new XAttribute(Namespaces.Draw + "z-index", "1"),
                new OdfCellEnd(slot.Sheet, slot.EndCell, services.Units.Length).Attributes());
        }
        var degrees = shape.RotationDegrees + extraRotation;
        var custom = Positioned(Namespaces.Draw + "custom-shape", services.Style(shape.Style), withZ: true,
            shape.FlipHorizontal != shape.FlipVertical ? -degrees : degrees);
        if (!string.IsNullOrEmpty(shape.Text) || services.TextRuns.Formatted(shape)) custom.Add(services.TextRuns.Build(shape, services.TextStyles));
        custom.Add(new XElement(Namespaces.Draw + "enhanced-geometry",
            new XAttribute(Namespaces.Draw + "type", TypePrefix + services.Presets.OoxmlName(shape.Shape)),
            new XAttribute(Namespaces.Svg + "viewBox", $"0 0 {EnhancedPathWriter.ViewBox} {EnhancedPathWriter.ViewBox}"),
            new XAttribute(Namespaces.Draw + "enhanced-path", services.EnhancedPath.Write(services.Flattener.Flatten(services.Geometry.Outline(shape)))),
            new XAttribute(Namespaces.Draw + "mirror-horizontal", shape.FlipHorizontal ? "true" : "false"),
            new XAttribute(Namespaces.Draw + "mirror-vertical", shape.FlipVertical ? "true" : "false")));
        new OdfExtrusion().Write(custom.Elements().Last(), shape.Style.Extrusion);
        _arcs.Write(custom.Elements().Last(), shape);
        services.Computed.Write(custom.Elements().Last(), shape, slot.Width, slot.Height);
        return custom;
    }

    public override XElement? VisitFreeform(FreeformObject freeform)
    {
        var box = EnhancedPathWriter.ViewBox;
        var path = Positioned(Namespaces.Draw + "path", services.Style(freeform.Style), withZ: false, freeform.RotationDegrees + extraRotation);
        path.Add(
            new XAttribute(Namespaces.Svg + "viewBox", $"0 0 {box} {box}"),
            new XAttribute(Namespaces.Svg + "d", services.Paths.Write(freeform.Path.ToOutline().Scaled(box, box))));
        path.Add(new XElement(Namespaces.Text + "p", string.Empty));
        return path;
    }

    public override XElement? VisitGroup(ShapeGroupObject group)
    {
        var element = new XElement(Namespaces.Draw + "g",
            new XAttribute(Namespaces.Draw + "name", id),
            new XAttribute(Namespaces.Draw + "z-index", "1"),
            new OdfCellEnd(slot.Sheet, slot.EndCell, services.Units.Length).Attributes());

        var groupDegrees = group.RotationDegrees + extraRotation;
        foreach (var child in group.Children)
            if (child.Content is IDrawingObject
                && ((IDrawingObject)child.Content).Accept(new SheetShapeElementVisitor(
                    services, child.Content.Id, slot.Child(child, groupDegrees), child.RotationDegrees + groupDegrees, inGroup: true)) is { } built)
                element.Add(built);

        return element;
    }

    public override XElement? VisitConnector(ConnectorObject connector)
    {
        var points = new SegmentEnds().Of(connector.Path.Scaled(slot.Width.Value, slot.Height.Value)).ToList();

        var start = points.Count > 0 ? points[0] : new ShapePoint(0, 0);
        var end = points.Count > 0 ? points[^1] : new ShapePoint(slot.Width.Value, slot.Height.Value);
        var units = services.Units;

        var element = new XElement(Namespaces.Draw + "connector",
            new XAttribute(Namespaces.Draw + "name", id),
            new XAttribute(Namespaces.Draw + "style-name", services.Style(connector.Style)),
            new XAttribute(Namespaces.Draw + "z-index", "1"),
            new OdfCellEnd(slot.Sheet, slot.EndCell, services.Units.Length).Attributes(),
            new XAttribute(Namespaces.Draw + "type", connector.Shape switch
            {
                ConnectorKind.Straight => "line",
                ConnectorKind.Curved => "curve",
                _ => "standard",
            }),
            new XAttribute(Namespaces.Svg + "x1", units.Length(new Twip(slot.X.Value + (int)Math.Round(start.X)))),
            new XAttribute(Namespaces.Svg + "y1", units.Length(new Twip(slot.Y.Value + (int)Math.Round(start.Y)))),
            new XAttribute(Namespaces.Svg + "x2", units.Length(new Twip(slot.X.Value + (int)Math.Round(end.X)))),
            new XAttribute(Namespaces.Svg + "y2", units.Length(new Twip(slot.Y.Value + (int)Math.Round(end.Y)))));

        if (connector.Start.ObjectId is { } from) element.Add(new XAttribute(Namespaces.Draw + "start-shape", from));
        if (connector.End.ObjectId is { } to) element.Add(new XAttribute(Namespaces.Draw + "end-shape", to));
        element.Add(new XElement(Namespaces.Text + "p", string.Empty));
        return element;
    }

    public override XElement? VisitFontwork(PulseOffice.Drawing.Fontwork.FontworkObject fontwork)
    {
        var custom = Positioned(Namespaces.Draw + "custom-shape", services.FontworkStyle(fontwork.Style, slot.Height), withZ: true);
        custom.Add(new XElement(Namespaces.Text + "p", fontwork.Text));
        custom.Add(services.Fontwork.Geometry(fontwork.Warp));
        return custom;
    }

    public override XElement? VisitRaster(PulseOffice.Drawing.Assets.RasterImageObject raster)
    {
        if (!inGroup || services.Pictures is not { } pictures || raster.Data.IsEmpty && raster.LinkPath is not { Length: > 0 }) return null;
        var units = services.Units;
        var frame = new XElement(Namespaces.Draw + "frame",
            new XAttribute(Namespaces.Draw + "name", id),
            new XAttribute(Namespaces.Draw + "z-index", "1"),
            new XAttribute(Namespaces.Svg + "width", units.Length(slot.Width)),
            new XAttribute(Namespaces.Svg + "height", units.Length(slot.Height)),
            new XElement(Namespaces.Draw + "image",
                new XAttribute(Namespaces.Xlink + "href", pictures.Store(raster)),
                new XAttribute(Namespaces.Xlink + "type", "simple"),
                new XAttribute(Namespaces.Xlink + "show", "embed"),
                new XAttribute(Namespaces.Xlink + "actuate", "onLoad")));
        services.Rotation.Place(frame, slot.X, slot.Y, slot.Width, slot.Height, extraRotation);
        return frame;
    }

    protected override XElement? Otherwise(EmbeddedObject content) => null;

    private XElement Positioned(XName name, string style, bool withZ, double degrees = 0)
    {
        var units = services.Units;
        var element = new XElement(name,
            new XAttribute(Namespaces.Draw + "name", id),
            new XAttribute(Namespaces.Draw + "style-name", style));
        if (withZ) element.Add(new XAttribute(Namespaces.Draw + "z-index", "1"));
        element.Add(
            new OdfCellEnd(slot.Sheet, slot.EndCell, services.Units.Length).Attributes(),
            new XAttribute(Namespaces.Svg + "width", units.Length(slot.Width)),
            new XAttribute(Namespaces.Svg + "height", units.Length(slot.Height)));
        services.Rotation.Place(element, slot.X, slot.Y, slot.Width, slot.Height, degrees);
        return element;
    }
}

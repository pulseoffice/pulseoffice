using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf.TextCodecs;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

public sealed class OdfShapeMapper
{
    private readonly IObjectOrdering _ordering;
    private readonly List<XElement> _styles = [];
    private readonly OdfShapeStyleMapper _styleMapper = new();
    private readonly SheetShapeServices _services;
    private readonly IReadOnlyList<SheetShapeReader> _readers;
    private IReadOnlyList<SheetShapeReader>? _groupMembers;
    private readonly AnchoredFrames _frames;
    private readonly ISheetPlacement _placement;
    private readonly OdfParagraphText _paragraphs = new();
    private readonly OdfShapeTextLists _lists = new();

    public OdfShapeMapper(IShapeGeometry geometry, IArcFlattener flattener, IObjectOrdering? ordering = null, ISheetPlacement? placement = null)
        : this(geometry, flattener, ordering, placement, null)
    {
    }

    internal OdfShapeMapper(IOdfPictureStore pictures) : this(new ShapeGeometry(), new ArcFlattener(), null, null, pictures) { }

    private OdfShapeMapper(IShapeGeometry geometry, IArcFlattener flattener, IObjectOrdering? ordering, ISheetPlacement? placement, IOdfPictureStore? pictures)
    {
        _placement = placement ?? new SheetPlacement();
        _ordering = ordering ?? new ObjectOrdering();
        _services = new SheetShapeServices(_styleMapper, _styles, new ShapePresets(), geometry, flattener, new ShapePathWriter(), _paragraphs, pictures);

        var shapes = new SheetCustomShapeReader(_paragraphs);
        _readers = [new SheetFontworkReader(), new SheetCustomPathReader(), shapes, new SheetFramedShapeReader(shapes), new SheetTextFrameReader(_paragraphs), new SheetFreeformShapeReader(), new SheetConnectorShapeReader(), new SheetGroupShapeReader(() => _groupMembers ??= [.. _readers!, new SheetGroupImageReader()]), new SheetRectangleReader(_paragraphs), new SheetEllipseReader("ellipse", _paragraphs), new SheetEllipseReader("circle", _paragraphs), new SheetLineReader("line", _paragraphs), new SheetDimensionLineReader(_paragraphs)];
        _frames = new AnchoredFrames(
            Namespaces.Draw + "custom-shape", Namespaces.Draw + "frame", Namespaces.Draw + "path", Namespaces.Draw + "polygon",
            Namespaces.Draw + "polyline", Namespaces.Draw + "connector", Namespaces.Draw + "g",
            Namespaces.Draw + "rect", Namespaces.Draw + "ellipse", Namespaces.Draw + "circle", Namespaces.Draw + "line", Namespaces.Draw + "measure");
    }

    public OdfShapeMapper() : this(new ShapeGeometry(), new ArcFlattener()) { }

    public IReadOnlyList<XElement> Declarations => _styles;

    public IReadOnlyList<XElement> Definitions => _styleMapper.Definitions;

    public IReadOnlyDictionary<CellAddress, List<XElement>> BuildFrames(Worksheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var frames = new Dictionary<CellAddress, List<XElement>>();
        foreach (var placed in _ordering.Drawn(sheet))
        {
            if (placed.Content is not IDrawingObject drawing) continue;

            var slot = new ShapeSlot(placed.OffsetX, placed.OffsetY, placed.Size.Width, placed.Size.Height, _placement.End(sheet.Layout, placed), sheet.Name);
            if (drawing.Accept(new SheetShapeElementVisitor(_services, placed.Id, slot)) is not { } element) continue;

            element.SetAttributeValue(Namespaces.Draw + "z-index", placed.ZOrder.ToString(CultureInfo.InvariantCulture));
            if (!frames.TryGetValue(placed.Anchor, out var list)) frames[placed.Anchor] = list = [];
            list.Add(element);
        }
        return frames;
    }

    public IReadOnlyList<PlacedObject> ReadShapes(XElement table, XDocument content, XDocument? styleDocument = null, Func<string, byte[]?>? readBytes = null)
    {
        ArgumentNullException.ThrowIfNull(table);

        var styles = Named(content, Namespaces.Style + "style").Concat(Named(styleDocument, Namespaces.Style + "style"))
            .Where(style => (string?)style.Attribute(Namespaces.Style + "family") == "graphic")
            .GroupBy(style => (string?)style.Attribute(Namespaces.Style + "name") ?? string.Empty)
            .ToDictionary(group => group.Key, group => group.First());

        var definitions = new[] { "gradient", "hatch", "stroke-dash", "marker", "fill-image" }
            .SelectMany(name => Named(content, Namespaces.Draw + name).Concat(Named(styleDocument, Namespaces.Draw + name)))
            .GroupBy(element => (string?)element.Attribute(Namespaces.Draw + "name") ?? string.Empty)
            .ToDictionary(group => group.Key, group => group.First());

        var texts = Named(content, Namespaces.Style + "style")
            .Where(style => (string?)style.Attribute(Namespaces.Style + "family") == "text")
            .GroupBy(style => (string?)style.Attribute(Namespaces.Style + "name") ?? string.Empty)
            .ToDictionary(group => group.Key, group => group.First());

        var lists = _lists.Styles(Named(content, Namespaces.Text + "list-style").Concat(Named(styleDocument, Namespaces.Text + "list-style")));
        var context = new SheetShapeReadContext(styles, definitions, _styleMapper, texts, readBytes, lists);
        return [.. _frames.In(table)
            .Select(frame => _readers.FirstOrDefault(reader => reader.Reads(frame.Frame))?.Read(frame.Frame, frame.Anchor, context))
            .OfType<PlacedObject>()];
    }

    private IEnumerable<XElement> Named(XDocument? document, XName name) => document?.Descendants(name) ?? [];
}

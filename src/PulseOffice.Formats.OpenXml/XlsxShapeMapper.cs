using DocumentFormat.OpenXml;
using PulseOffice.Document.Embedding;
using PulseOffice.Drawing;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using A = DocumentFormat.OpenXml.Drawing;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;
using Worksheet = PulseOffice.Sheets.Worksheet;

namespace PulseOffice.Formats.OpenXml;

public sealed class XlsxShapeMapper
{
    private readonly IShapePresets _presets;
    private readonly IObjectOrdering _ordering;
    private readonly XlsxShapeStyleMapper _styles = new();
    private readonly XlsxImageMapper _images = new();
    private readonly IReadOnlyList<SheetDrawingReader> _readers;

    public XlsxShapeMapper(IObjectOrdering? ordering = null, IShapePresets? presets = null)
    {
        _presets = presets ?? new ShapePresets();
        _ordering = ordering ?? new ObjectOrdering();
        var shapes = new SheetShapeElementReader();
        var freeforms = new SheetFreeformReader();
        var connectors = new SheetConnectorReader();
        _readers = [new SheetFontworkReader(new OoxmlFontworkText(_styles)), shapes, freeforms, new SheetGroupReader([shapes, freeforms, connectors, new SheetGroupPictureReader()]), connectors];
    }

    public IEnumerable<OpenXmlCompositeElement> BuildAnchors(Worksheet sheet) => BuildAnchors(sheet, null);

    public IEnumerable<OpenXmlCompositeElement> BuildAnchors(Worksheet sheet, DocumentFormat.OpenXml.Packaging.DrawingsPart? part)
    {
        var links = new XlsxDrawingRunLinks(part);
        ArgumentNullException.ThrowIfNull(sheet);

        var ids = new DrawingIdentifiers();
        foreach (var placed in _ordering.Drawn(sheet))
        {
            var id = ids.Register(placed.Id);
            if (placed.Content is not IDrawingObject drawing) continue;

            var box = new DrawingBox(0, 0, Emu.FromTwips(placed.Size.Width).Value, Emu.FromTwips(placed.Size.Height).Value);
            var pictures = part is null || drawing is not ShapeGroupObject ? null : new XlsxGroupPictures(part, _images);
            if (drawing.Accept(new SheetDrawingVisitor(_styles, _presets, ids, placed.Id, id, box, links, 0, pictures)) is not { } element) continue;

            yield return Anchor(sheet.Layout, placed, element);
        }
    }

    public IReadOnlyList<PlacedObject> ReadShapes(Xdr.WorksheetDrawing drawing, SheetLayout layout)
    {
        ArgumentNullException.ThrowIfNull(drawing);
        ArgumentNullException.ThrowIfNull(layout);

        var anchors = new XlsxAnchorLayout(layout);

        var names = drawing.Descendants<Xdr.NonVisualDrawingProperties>()
            .Where(properties => properties.Id?.Value is not null && properties.Name?.Value is not null)
            .GroupBy(properties => properties.Id!.Value)
            .ToDictionary(group => group.Key, group => group.First().Name!.Value!);

        var context = new SheetReadContext(_styles, _presets, names, new XlsxDrawingRunLinks(drawing.DrawingsPart),
            drawing.DrawingsPart is { } drawingsPart ? new XlsxGroupPictures(drawingsPart, _images) : null);
        var placed = new List<PlacedObject>();

        foreach (var anchor in drawing.ChildElements.OfType<OpenXmlCompositeElement>())
        {
            var content = anchor.ChildElements
                .Where(child => child is Xdr.Shape or Xdr.GroupShape or Xdr.ConnectionShape)
                .Take(1)
                .Select(child => _readers.Select(reader => reader.Read(child, context)).FirstOrDefault(read => read is not null))
                .FirstOrDefault();
            if (content is null) continue;

            placed.Add(anchors.Place(anchor, new PlacedObject { Id = content.Id, Content = content, Anchor = new CellAddress(0, 0) },
                new DocumentSize(Twip.FromCentimeters(4), Twip.FromCentimeters(3))));
        }
        return placed;
    }

    private Xdr.TwoCellAnchor Anchor(SheetLayout layout, PlacedObject placed, OpenXmlElement element) =>
        _anchors.Create(layout, placed, element, new Xdr.ClientData());

    private readonly XlsxCellAnchors _anchors = new();

}

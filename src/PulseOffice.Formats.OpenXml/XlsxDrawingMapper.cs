using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Sheets;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;
using Worksheet = PulseOffice.Sheets.Worksheet;

namespace PulseOffice.Formats.OpenXml;

public sealed class XlsxDrawingMapper
{
    private readonly OoxmlAltTextMapper _altText = new();

    private readonly XlsxObjectLinks _links = new();

    private readonly XlsxChartMapper _charts;
    private readonly XlsxShapeMapper _shapes;
    private readonly XlsxVectorMapper _vectors;
    private readonly XlsxImageMapper _images;

    public XlsxDrawingMapper(XlsxChartMapper charts, XlsxShapeMapper shapes, XlsxVectorMapper vectors, XlsxImageMapper images)
    {
        _images = images;
        _charts = charts;
        _shapes = shapes;
        _vectors = vectors;
    }

    public XlsxDrawingMapper()
        : this(new XlsxChartMapper(), new XlsxShapeMapper(), new XlsxVectorMapper(), new XlsxImageMapper()) { }

    public void Write(WorksheetPart part, Worksheet sheet, Workbook workbook)
    {
        ArgumentNullException.ThrowIfNull(part);
        ArgumentNullException.ThrowIfNull(sheet);

        if (sheet.Objects.Count == 0) return;

        var drawingsPart = part.AddNewPart<DrawingsPart>();
        drawingsPart.WorksheetDrawing = new Xdr.WorksheetDrawing();

        var written = 0;

        foreach (var anchor in _charts.BuildAnchors(drawingsPart, sheet, workbook))
        {
            drawingsPart.WorksheetDrawing.AppendChild(anchor);
            written++;
        }

        foreach (var anchor in _shapes.BuildAnchors(sheet, drawingsPart))
        {
            drawingsPart.WorksheetDrawing.AppendChild(anchor);
            written++;
        }

        foreach (var anchor in _images.BuildAnchors(drawingsPart, sheet))
        {
            drawingsPart.WorksheetDrawing.AppendChild(anchor);
            written++;
        }

        if (written == 0)
        {
            part.DeletePart(drawingsPart);
            return;
        }

        _altText.WriteByName(drawingsPart.WorksheetDrawing, new PlacedObjectIndex(sheet.Objects).Content);
        _links.Write(drawingsPart, sheet.Objects);
        drawingsPart.WorksheetDrawing.Save();

        part.Worksheet!.AppendChild(new DocumentFormat.OpenXml.Spreadsheet.Drawing
        {
            Id = part.GetIdOfPart(drawingsPart),
        });
    }

    public IReadOnlyList<PlacedObject> Read(WorksheetPart part, SheetLayout layout)
    {
        ArgumentNullException.ThrowIfNull(part);
        return Read(part.DrawingsPart, layout);
    }

    public IReadOnlyList<PlacedObject> Read(DrawingsPart? drawings, SheetLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (drawings?.WorksheetDrawing is not { } drawing) return [];

        var described = _altText.ReadByName(drawing);
        var links = _links.Read(drawings);

        return
        [
            .. new[]
            {
                _charts.ReadCharts(drawings, layout),
                _shapes.ReadShapes(drawing, layout),
                _vectors.ReadVectors(drawing, layout),
                _images.ReadImages(drawings, layout),
            }.SelectMany(objects => objects).Select(placed => described.TryGetValue(placed.Id, out var text)
                ? placed with { Content = text.ApplyTo(placed.Content) }
                : placed).Select(placed => links.TryGetValue(placed.Id, out var link) ? placed with { Hyperlink = link } : placed),
        ];
    }
}

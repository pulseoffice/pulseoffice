using DocumentFormat.OpenXml;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.OpenXml.DrawingML;
using PulseOffice.Presentations;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

internal sealed class PptxShapeTextRuns
{
    private readonly DrawingShapeTextRuns _links = new();

    public ObjectElement Read(P.Shape node, ObjectElement element, IDrawingRunLinks links) =>
        element.Content is ShapeObject shape && _links.Read(node.TextBody, shape, links) is var read && !ReferenceEquals(read, shape)
            ? element with { Content = read }
            : element;

    public bool Formatted(ShapeObject shape) => _links.Formatted(shape);

    public OpenXmlElement Write(OpenXmlElement written, ShapeObject shape, IDrawingRunLinks links) => _links.Write(written, shape, links);
}

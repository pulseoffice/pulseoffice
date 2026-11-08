using DocumentFormat.OpenXml;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Presentations;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml;

internal sealed class PptxElementRotation
{
    private const int FullCircle = 21600000;

    public T Write<T>(T node, SlideElement element) where T : OpenXmlElement
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(element);

        var degrees = element.RotationDegrees + Content(element);
        if (!double.IsFinite(degrees)) return node;
        var angle = (int)((((long)Math.Round(degrees % 360 * 60000)) % FullCircle + FullCircle) % FullCircle);

        switch (node.Descendants<D.Transform2D>().FirstOrDefault())
        {
            case { } transform:
                transform.Rotation = angle == 0 ? null : angle;
                break;
            default:
                if (node.Descendants<DocumentFormat.OpenXml.Presentation.Transform>().FirstOrDefault() is { } frame)
                    frame.Rotation = angle == 0 ? null : angle;
                break;
        }

        return node;
    }

    public T Read<T>(OpenXmlElement node, T element) where T : SlideElement
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(element);

        var angle = node.Descendants<D.Transform2D>().FirstOrDefault()?.Rotation?.Value
            ?? node.Descendants<DocumentFormat.OpenXml.Presentation.Transform>().FirstOrDefault()?.Rotation?.Value
            ?? 0;
        var degrees = ((angle / 60000.0) % 360 + 360) % 360;

        return (element with { RotationDegrees = degrees }) switch
        {
            ObjectElement { Content: ShapeObject shape } placed => (T)(SlideElement)(placed with { Content = shape with { RotationDegrees = 0 } }),
            var other => (T)other,
        };
    }

    private double Content(SlideElement element) => element switch
    {
        ObjectElement { Content: ShapeObject shape } => shape.RotationDegrees,
        ObjectElement { Content: FreeformObject freeform } => freeform.RotationDegrees,
        _ => 0,
    };
}

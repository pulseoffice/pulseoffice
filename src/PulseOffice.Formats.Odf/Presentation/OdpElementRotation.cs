using System.Xml.Linq;
using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal sealed class OdpElementRotation
{
    private readonly OdfDrawRotation _rotation = new();

    public XElement Write(XElement node, SlideElement element)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(element);

        var degrees = Total(element);
        if (!double.IsFinite(degrees) || Math.Abs(degrees % 360) <= 0.001) return node;
        if (node.Attribute(Namespaces.Svg + "x") is null && node.Attribute(Namespaces.Draw + "transform") is null) return node;

        var bounds = element.Bounds;
        _rotation.Place(node, bounds.Left, bounds.Top, bounds.Width, bounds.Height, Mirrored(element) ? -degrees : degrees);
        return node;
    }

    public T Read<T>(XElement node, T element) where T : SlideElement
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(element);
        if (node.Attribute(Namespaces.Draw + "transform") is null) return element;

        var position = _rotation.Read(node, element.Bounds.Width, element.Bounds.Height);
        var degrees = Mirrored(element) ? (360 - position.Degrees) % 360 : position.Degrees;
        return element with
        {
            Bounds = new DocumentRect(position.X, position.Y, element.Bounds.Width, element.Bounds.Height),
            RotationDegrees = degrees,
        };
    }

    private double Total(SlideElement element) => element.RotationDegrees + element switch
    {
        ObjectElement { Content: ShapeObject shape } => shape.RotationDegrees,
        ObjectElement { Content: FreeformObject freeform } => freeform.RotationDegrees,
        ObjectElement { Content: ShapeGroupObject group } => group.RotationDegrees,
        _ => 0,
    };

    private bool Mirrored(SlideElement element) =>
        element is ObjectElement { Content: ShapeObject shape } && shape.FlipHorizontal != shape.FlipVertical;
}

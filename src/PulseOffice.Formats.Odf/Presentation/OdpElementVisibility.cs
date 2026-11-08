using System.Xml.Linq;
using PulseOffice.Formats.Packaging;
using PulseOffice.Presentations;

namespace PulseOffice.Formats.Odf;

internal sealed class OdpElementVisibility
{
    private readonly XName _extended = OdfPackage.Namespaces.DrawExt + "display";
    private readonly XName _standard = OdfPackage.Namespaces.Draw + "display";
    private readonly HashSet<string> _offScreen = new(StringComparer.Ordinal) { "none", "printer" };

    public XElement Write(XElement node, SlideElement element)
    {
        if (element.Hidden) node.SetAttributeValue(_extended, "none");
        return node;
    }

    public bool Hidden(XElement node) =>
        _offScreen.Contains((string?)node.Attribute(_extended) ?? (string?)node.Attribute(_standard) ?? string.Empty);

    public void Inherit(XElement child)
    {
        if (!Hidden(child)) child.SetAttributeValue(_extended, "none");
    }
}

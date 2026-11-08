using System.Xml.Linq;
using PulseOffice.Presentations;

namespace PulseOffice.Formats.Odf;

internal sealed class OdpHiddenShapes(OdpElementVisibility visibility) : OdpPageReader
{
    public override void Read(XElement page, SlideAssembly<string> assembly, OdpReadContext context)
    {
        foreach (var shape in page.Elements().Where(visibility.Hidden))
        {
            if (((string?)shape.Attribute(XmlId) ?? (string?)shape.Attribute(Draw + "id")) is not { } frame
                || !assembly.ElementByFrame.TryGetValue(frame, out var elementId)
                || assembly.Slide.Find(elementId) is not { Hidden: false } element)
                continue;
            assembly.Slide.Replace(element with { Hidden = true });
        }
    }
}

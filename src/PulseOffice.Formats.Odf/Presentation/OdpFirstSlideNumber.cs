using System.Globalization;
using System.Xml.Linq;

namespace PulseOffice.Formats.Odf;

internal sealed class OdpFirstSlideNumber
{
    private static readonly XNamespace Style = OdfPackage.Namespaces.Style;
    private static readonly XName Extension = XNamespace.Get("urn:pulseoffice:presentation-numbering") + "first-slide-number";

    public void Write(XElement properties, PulseOffice.Presentations.Presentation deck)
    {
        if (deck.FirstSlideNumber == 1) return;
        if (deck.FirstSlideNumber > 0)
            properties.Add(new XAttribute(Style + "first-page-number", deck.FirstSlideNumber.ToString(CultureInfo.InvariantCulture)));
        else
            properties.Add(new XAttribute(Extension, deck.FirstSlideNumber.ToString(CultureInfo.InvariantCulture)));
    }

    public void Read(XDocument? styles, PulseOffice.Presentations.Presentation deck)
    {
        if (styles?.Root is not { } root) return;
        foreach (var properties in root.Descendants(Style + "page-layout-properties"))
        {
            var raw = (string?)properties.Attribute(Extension) ?? (string?)properties.Attribute(Style + "first-page-number");
            if (raw is null || !int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)) continue;
            deck.FirstSlideNumber = Math.Clamp(number, 0, 9999);
            return;
        }
    }
}

using System.Globalization;
using System.Xml.Linq;

namespace PulseOffice.Formats.Odf;

internal sealed class OdpFirstSlideNumber
{
    private static readonly XNamespace Style = OdfPackage.Namespaces.Style;
    private static readonly XNamespace Text = OdfPackage.Namespaces.Text;
    private static readonly XName Extension = XNamespace.Get("urn:pulseoffice:presentation-numbering") + "first-slide-number";

    public void Write(XElement properties, PulseOffice.Presentations.Presentation deck)
    {
        if (deck.FirstSlideNumber == 1) return;
        if (deck.FirstSlideNumber > 0)
            properties.Add(new XAttribute(Style + "first-page-number", deck.FirstSlideNumber.ToString(CultureInfo.InvariantCulture)));
        else
            properties.Add(new XAttribute(Extension, deck.FirstSlideNumber.ToString(CultureInfo.InvariantCulture)));
    }

    public void AdjustFields(PulseOffice.Presentations.Presentation deck, params XDocument[] documents)
    {
        if (deck.FirstSlideNumber > 0) return;
        var adjust = (deck.FirstSlideNumber - 1).ToString(CultureInfo.InvariantCulture);
        foreach (var field in documents.SelectMany(document => document.Descendants(Text + "page-number")))
            field.SetAttributeValue(Text + "page-adjust", adjust);
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

    public void ReadAdjust(PulseOffice.Presentations.Presentation deck, params XDocument?[] documents)
    {
        if (deck.FirstSlideNumber != 1) return;
        var adjusts = documents.OfType<XDocument>().SelectMany(document => document.Descendants(Text + "page-number"))
            .Select(field => (string?)field.Attribute(Text + "page-adjust"))
            .ToList();
        if (adjusts.Count == 0 || adjusts.Distinct().Count() != 1) return;
        if (!int.TryParse(adjusts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var adjust) || adjust >= 0) return;
        deck.FirstSlideNumber = Math.Clamp(1 + adjust, 0, 9999);
    }
}

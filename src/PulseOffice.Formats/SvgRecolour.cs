using System.Xml.Linq;

namespace PulseOffice.Formats;

public sealed class SvgRecolour
{
    private readonly System.Xml.XmlReaderSettings _settings = new() { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null };

    public string Apply(string svg, string? hex)
    {
        if (string.IsNullOrEmpty(hex)) return svg;

        XDocument document;
        try
        {
            using var reader = System.Xml.XmlReader.Create(new StringReader(svg), _settings);
            document = XDocument.Load(reader);
        }
        catch (System.Xml.XmlException)
        {
            return svg;
        }

        if (document.Root is not { } root) return svg;

        root.SetAttributeValue("fill", hex);
        foreach (var path in root.Descendants().Where(element => element.Name.LocalName == "path"))
            if (path.Attribute("fill") is { } fill && fill.Value != "none")
                fill.Value = hex;

        return document.ToString(SaveOptions.DisableFormatting);
    }
}

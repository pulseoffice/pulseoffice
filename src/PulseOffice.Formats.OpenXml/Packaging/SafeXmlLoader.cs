using System.Xml;
using System.Xml.Linq;

namespace PulseOffice.Formats.OpenXml;

internal sealed class SafeXmlLoader
{
    private readonly XmlReaderSettings _settings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersFromEntities = 0,
        CloseInput = false,
    };

    public XDocument Document(Stream input)
    {
        using var reader = XmlReader.Create(input, _settings);
        return XDocument.Load(reader);
    }

    public XElement Element(Stream input)
    {
        using var reader = XmlReader.Create(input, _settings);
        return XElement.Load(reader);
    }
}

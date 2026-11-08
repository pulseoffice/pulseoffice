using System.Xml;
using System.Xml.Linq;

namespace PulseOffice.Formats.Packaging;

public sealed class XmlPartLoader
{
    private readonly XmlReaderSettings _settings = new()
    {
        DtdProcessing = DtdProcessing.Ignore,
        XmlResolver = null,
        MaxCharactersFromEntities = 1024,
        CloseInput = false,
    };

    public XDocument Load(Stream stream, LoadOptions options = LoadOptions.PreserveWhitespace)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var reader = XmlReader.Create(stream, _settings);
        return XDocument.Load(reader, options);
    }

    public XDocument Parse(string text, LoadOptions options = LoadOptions.None)
    {
        ArgumentNullException.ThrowIfNull(text);
        using var reader = XmlReader.Create(new StringReader(text), _settings);
        return XDocument.Load(reader, options);
    }
}

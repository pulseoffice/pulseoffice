using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace PulseOffice.Formats.Packaging;

public sealed class XmlCharacterFilter
{
    public void Clean(XContainer document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document is XElement self)
            foreach (var attribute in self.Attributes().Where(attribute => !IsValid(attribute.Value)))
                attribute.Value = Valid(attribute.Value);
        foreach (var node in document.DescendantNodes())
        {
            switch (node)
            {
                case XText text when !IsValid(text.Value):
                    text.Value = Valid(text.Value);
                    break;
                case XElement element:
                    foreach (var attribute in element.Attributes().Where(attribute => !IsValid(attribute.Value)))
                        attribute.Value = Valid(attribute.Value);
                    break;
            }
        }
    }

    public string Valid(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (IsValid(text)) return text;

        var builder = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (char.IsHighSurrogate(character) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                builder.Append(character).Append(text[++index]);
                continue;
            }

            if (XmlConvert.IsXmlChar(character)) builder.Append(character);
        }

        return builder.ToString();
    }

    private bool IsValid(string text)
    {
        try
        {
            XmlConvert.VerifyXmlChars(text);
            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }
}

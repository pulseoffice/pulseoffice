using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace PulseOffice.Formats;

public interface ISvgSanitizer
{
    XDocument? Sanitize(byte[] svg);
}

public sealed partial class SvgSanitizer : ISvgSanitizer
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    private readonly HashSet<string> _forbiddenElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "foreignObject", "iframe", "embed", "object", "audio", "video", "handler", "listener", "animation", "discard",
    };

    public XDocument? Sanitize(byte[] svg)
    {
        ArgumentNullException.ThrowIfNull(svg);

        XDocument document;
        try
        {
            using var reader = XmlReader.Create(new MemoryStream(svg, writable: false), new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 64L << 20,
            });
            document = XDocument.Load(reader);
        }
        catch (XmlException)
        {
            return null;
        }

        if (document.Root is null) return null;

        document.DescendantNodes().OfType<XProcessingInstruction>().ToList().ForEach(instruction => instruction.Remove());
        document.Descendants().Where(element => _forbiddenElements.Contains(element.Name.LocalName) || AnimatesReference(element)).ToList().ForEach(element => element.Remove());

        foreach (var element in document.Descendants().ToList())
        {
            foreach (var attribute in element.Attributes().ToList())
            {
                var name = attribute.Name.LocalName;
                if (name.StartsWith("on", StringComparison.OrdinalIgnoreCase)) attribute.Remove();
                else if (name.Equals("href", StringComparison.OrdinalIgnoreCase) || name.Equals("src", StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsInternal(attribute.Value)) attribute.Remove();
                }
                else if (attribute.Value.Contains('\\') || ExternalUrl().IsMatch(attribute.Value)) attribute.Value = Neutral(attribute.Value);
            }

            if (element.Name.LocalName.Equals("style", StringComparison.OrdinalIgnoreCase))
            {
                element.Value = ImportRule().Replace(Neutral(element.Value), string.Empty);
            }
        }

        return document;
    }

    private string Neutral(string css) => ExternalUrl().Replace(css.Replace("\\", string.Empty, StringComparison.Ordinal), "none");

    private bool AnimatesReference(XElement element) =>
        element.Name.LocalName is "set" or "animate"
        && (element.Attribute("attributeName")?.Value.Trim() is { } target
            && (target.EndsWith("href", StringComparison.OrdinalIgnoreCase) || target.Equals("src", StringComparison.OrdinalIgnoreCase)));

    private bool IsInternal(string reference)
    {
        var trimmed = reference.Trim();
        return trimmed.StartsWith('#')
            || (trimmed.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) && !trimmed.StartsWith("data:image/svg", StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex(@"url\(\s*['""]?(?!\s*#)(?!\s*['""]?\s*#)[^)]*\)", RegexOptions.IgnoreCase)]
    private static partial Regex ExternalUrl();

    [GeneratedRegex(@"@import[^;]*;?", RegexOptions.IgnoreCase)]
    private static partial Regex ImportRule();
}

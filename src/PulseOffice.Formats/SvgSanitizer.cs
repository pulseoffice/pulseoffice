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

        LimitReferences(document);
        return document;
    }

    private const int DeepestReference = 32;
    private const long LargestExpansion = 1_000_000;

    private void LimitReferences(XDocument document)
    {
        var uses = document.Descendants().Where(element => element.Name.LocalName == "use").ToList();
        if (uses.Count == 0) return;

        var ids = document.Descendants()
            .Select(element => (Id: element.Attribute("id")?.Value, Element: element))
            .Where(pair => pair.Id is { Length: > 0 })
            .GroupBy(pair => pair.Id!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Element, StringComparer.Ordinal);

        var costs = new Dictionary<XElement, long>();
        foreach (var use in uses)
        {
            if (Target(use, ids) is not { } target) continue;
            if (use.Ancestors().Contains(target) || Cost(target, ids, costs, [], 0) < 0) use.Remove();
        }

        if (Cost(document.Root!, ids, [], [], 0) is < 0 or > LargestExpansion)
            document.Descendants().Where(element => element.Name.LocalName == "use").ToList().ForEach(element => element.Remove());
    }

    private long Cost(XElement element, Dictionary<string, XElement> ids, Dictionary<XElement, long> costs, HashSet<XElement> visiting, int depth)
    {
        if (costs.TryGetValue(element, out var known)) return known;
        if (depth > DeepestReference || !visiting.Add(element)) return -1;

        long total = 0;
        foreach (var node in element.DescendantsAndSelf())
        {
            total++;
            if (node.Name.LocalName == "use" && Target(node, ids) is { } target)
            {
                var nested = Cost(target, ids, costs, visiting, depth + 1);
                if (nested < 0)
                {
                    visiting.Remove(element);
                    return -1;
                }
                total += nested;
            }
            if (total > LargestExpansion) break;
        }

        visiting.Remove(element);
        costs[element] = total;
        return total;
    }

    private XElement? Target(XElement use, Dictionary<string, XElement> ids)
    {
        var reference = use.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "href")?.Value.Trim();
        return reference is { Length: > 1 } && reference[0] == '#' && ids.TryGetValue(reference[1..], out var target) ? target : null;
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

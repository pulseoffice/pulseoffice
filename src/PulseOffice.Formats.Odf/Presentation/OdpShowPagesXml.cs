using System.Text;
using System.Xml.Linq;

namespace PulseOffice.Formats.Odf;

internal sealed class OdpShowPagesXml
{
    private const char Separator = ',';
    private const char Escape = '\\';
    private static readonly XNamespace Presentation = OdfPackage.Namespaces.Presentation;
    private readonly XName _pages = XNamespace.Get("urn:pulseoffice:presentation-show") + "pages";

    public bool Representable(string pageName) => !pageName.Contains(Separator, StringComparison.Ordinal);

    public void Write(XElement show, IReadOnlyList<string> pageNames)
    {
        var standard = pageNames.Where(Representable).ToList();
        show.Add(new XAttribute(Presentation + "pages", string.Join(Separator, standard)));
        if (standard.Count != pageNames.Count)
            show.Add(new XAttribute(_pages, string.Join(Separator, pageNames.Select(Escaped))));
    }

    public IReadOnlyList<string>? Own(XElement show) =>
        (string?)show.Attribute(_pages) is { } encoded ? Unescaped(encoded) : null;

    private static string Escaped(string name) =>
        name.Replace(Escape.ToString(), $"{Escape}{Escape}", StringComparison.Ordinal)
            .Replace(Separator.ToString(), $"{Escape}{Separator}", StringComparison.Ordinal);

    private static List<string> Unescaped(string encoded)
    {
        var names = new List<string>();
        var current = new StringBuilder();
        for (var index = 0; index < encoded.Length; index++)
        {
            var character = encoded[index];
            if (character == Escape && index + 1 < encoded.Length) current.Append(encoded[++index]);
            else if (character == Separator) { names.Add(current.ToString()); current.Clear(); }
            else current.Append(character);
        }
        names.Add(current.ToString());
        return [.. names.Where(name => name.Length > 0)];
    }
}

using System.Xml.Linq;

namespace PulseOffice.Formats.Odf.TextCodecs;

internal sealed class OdfFontFaceNames : OdfXml
{
    public string? Family(XElement? context, string? name)
    {
        if (name is not { Length: > 0 }) return null;
        var face = context?.Document?.Root?.Element(Office + "font-face-decls")?.Elements(Style + "font-face")
            .FirstOrDefault(candidate => (string?)candidate.Attribute(Style + "name") == name);
        return (string?)face?.Attribute(Svg + "font-family") is { Length: > 0 } family ? First(family) : name;
    }

    public bool Symbolic(XElement? context, XElement? properties)
    {
        if ((string?)properties?.Attribute(Style + "font-charset") == "x-symbol") return true;
        if ((string?)properties?.Attribute(Style + "font-name") is not { Length: > 0 } name) return false;
        var face = context?.Document?.Root?.Element(Office + "font-face-decls")?.Elements(Style + "font-face")
            .FirstOrDefault(candidate => (string?)candidate.Attribute(Style + "name") == name);
        return (string?)face?.Attribute(Style + "font-charset") == "x-symbol";
    }

    public string? Unquoted(string? family) => family is { Length: > 0 } ? First(family) : null;

    private string First(string family)
    {
        var first = family.Split(',')[0].Trim();
        return first.Length > 1 && first[0] is '\'' or '"' && first[^1] == first[0] ? first[1..^1] : first;
    }
}

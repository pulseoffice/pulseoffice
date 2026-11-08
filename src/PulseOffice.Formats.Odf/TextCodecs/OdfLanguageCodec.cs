using System.Xml.Linq;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf.TextCodecs;

internal enum OdfScript
{
    Western,
    Asian,
    Complex,
}

internal sealed class OdfLanguageCodec : OdfXml
{
    public string? Read(XElement? properties, OdfScript script)
    {
        var (languageName, countryName) = Names(script);
        if ((string?)properties?.Attribute(languageName) is not { Length: > 0 } language || language == "none") return null;
        var country = (string?)properties?.Attribute(countryName);
        return country is { Length: > 0 } and not "none" ? $"{language}-{country}" : language;
    }

    public TextLanguages Read(XElement? properties) =>
        new(Read(properties, OdfScript.Western), Read(properties, OdfScript.Asian), Read(properties, OdfScript.Complex));

    public IEnumerable<XAttribute> Write(string? tag, OdfScript script)
    {
        if (tag is not { Length: > 0 }) yield break;
        var parts = tag.Replace('_', '-').Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) yield break;
        var (languageName, countryName) = Names(script);
        yield return new XAttribute(languageName, parts[0].ToLowerInvariant());
        yield return new XAttribute(countryName, parts.Length > 1 ? parts[^1].ToUpperInvariant() : "none");
    }

    public IEnumerable<XAttribute> Write(TextLanguages languages) =>
        Write(languages.Western, OdfScript.Western)
            .Concat(Write(languages.Asian, OdfScript.Asian))
            .Concat(Write(languages.Complex, OdfScript.Complex));

    private (XName Language, XName Country) Names(OdfScript script) => script switch
    {
        OdfScript.Asian => (Style + "language-asian", Style + "country-asian"),
        OdfScript.Complex => (Style + "language-complex", Style + "country-complex"),
        _ => (Fo + "language", Fo + "country"),
    };
}

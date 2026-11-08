using System.Xml.Linq;
using PulseOffice.Formats.Odf.TextCodecs;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf;

internal sealed class OdpTextDefaults(OdfLanguageCodec codec, OdfFontFaceNames faces) : OdfXml
{
    public OdpTextDefaults() : this(new OdfLanguageCodec(), new OdfFontFaceNames())
    {
    }

    public (TextLanguages Languages, ScriptFonts Fonts) Read(XDocument? styles)
    {
        var defaults = styles?.Root?.Element(Office + "styles")?.Elements(Style + "default-style").ToList() ?? [];
        var graphic = defaults.FirstOrDefault(style => (string?)style.Attribute(Style + "family") == "graphic")
            ?? defaults.FirstOrDefault(style => (string?)style.Attribute(Style + "family") == "paragraph");
        var properties = graphic?.Element(Style + "text-properties");
        return (codec.Read(properties), new ScriptFonts(
            faces.Unquoted((string?)properties?.Attribute(Style + "font-family-asian")) ?? faces.Family(properties, (string?)properties?.Attribute(Style + "font-name-asian")),
            faces.Unquoted((string?)properties?.Attribute(Style + "font-family-complex")) ?? faces.Family(properties, (string?)properties?.Attribute(Style + "font-name-complex"))));
    }

    public XElement? Write(TextLanguages languages, ScriptFonts fonts) =>
        languages.IsEmpty && fonts.IsEmpty
            ? null
            : new XElement(Style + "default-style",
                new XAttribute(Style + "family", "graphic"),
                new XElement(Style + "text-properties",
                    codec.Write(languages),
                    fonts.Asian is { } asian ? new XAttribute(Style + "font-family-asian", asian) : null,
                    fonts.Complex is { } complex ? new XAttribute(Style + "font-family-complex", complex) : null));
}

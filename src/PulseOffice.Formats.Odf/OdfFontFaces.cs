using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Document.Fonts;
using PulseOffice.Formats.Packaging;
using static PulseOffice.Formats.Packaging.OdfPackage;

namespace PulseOffice.Formats.Odf;

public sealed record OdfFontFile(string Path, byte[] Data)
{
    public const string MediaType = "application/x-font-ttf";
}

public sealed class OdfFontFaceWriter
{
    private static readonly XNamespace Config = "urn:oasis:names:tc:opendocument:xmlns:config:1.0";

    public (XElement Declarations, IReadOnlyList<OdfFontFile> Files) Build(IEnumerable<string> families, DocumentFonts fonts)
    {
        ArgumentNullException.ThrowIfNull(families);
        ArgumentNullException.ThrowIfNull(fonts);

        var names = new List<string>();
        foreach (var family in families.Concat(fonts.Families))
            if (!string.IsNullOrWhiteSpace(family) && !names.Contains(family, StringComparer.OrdinalIgnoreCase)) names.Add(family);

        var files = new List<OdfFontFile>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var declarations = new XElement(Namespaces.Office + "font-face-decls");

        foreach (var family in names)
        {
            var face = new XElement(Namespaces.Style + "font-face",
                new XAttribute(Namespaces.Style + "name", family),
                new XAttribute(Namespaces.Svg + "font-family", Quoted(family)));
            if (fonts.TraitsOf(family) is { } traits) Classify(face, traits);

            var embedded = fonts.FilesOf(family).ToList();
            if (embedded.Count > 0)
            {
                var source = new XElement(Namespaces.Svg + "font-face-src");
                foreach (var file in embedded)
                {
                    var path = UniquePath(file, used);
                    files.Add(new OdfFontFile(path, file.Data));
                    source.Add(new XElement(Namespaces.Svg + "font-face-uri",
                        new XAttribute(Namespaces.Xlink + "href", path),
                        new XAttribute(Namespaces.Xlink + "type", "simple"),
                        new XAttribute(Namespaces.Loext + "font-style", file.Italic ? "italic" : "normal"),
                        new XAttribute(Namespaces.Loext + "font-weight", file.IsBold ? "bold" : "normal"),
                        new XElement(Namespaces.Svg + "font-face-format", new XAttribute(Namespaces.Svg + "string", "truetype"))));
                }

                face.Add(source);
            }

            declarations.Add(face);
        }

        return (declarations, files);
    }

    public XDocument Settings(DocumentFonts fonts) => Settings(fonts, []);

    public XDocument Settings(DocumentFonts fonts, IReadOnlyList<XElement> additionalSets)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        XElement Item(string name, bool value) => new(Config + "config-item",
            new XAttribute(Config + "name", name),
            new XAttribute(Config + "type", "boolean"),
            value ? "true" : "false");

        return new XDocument(new XElement(Namespaces.Office + "document-settings",
            new XAttribute(XNamespace.Xmlns + "office", Namespaces.Office.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "config", Config.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "ooo", Namespaces.Ooo.NamespaceName),
            new XAttribute(Namespaces.Office + "version", "1.3"),
            new XElement(Namespaces.Office + "settings",
                new XElement(Config + "config-item-set",
                    new XAttribute(Config + "name", "ooo:configuration-settings"),
                    Item("EmbedFonts", fonts.EmbedSystemFonts || fonts.HasFiles),
                    Item("EmbedOnlyUsedFonts", true),
                    Item("EmbedPulseOfficeSystemFonts", fonts.EmbedSystemFonts)),
                additionalSets)));
    }

    public IEnumerable<(string Path, string MediaType)> WriteAll(OdfPackage package, IReadOnlyList<OdfFontFile> files, DocumentFonts fonts) =>
        WriteAll(package, files, fonts, []);

    public IEnumerable<(string Path, string MediaType)> WriteAll(
        OdfPackage package, IReadOnlyList<OdfFontFile> files, DocumentFonts fonts, IReadOnlyList<XElement> additionalSettings)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(files);

        foreach (var file in files)
        {
            package.WriteBytes(file.Path, file.Data);
            yield return (file.Path, OdfFontFile.MediaType);
        }

        if (files.Count > 0 || fonts.EmbedSystemFonts || additionalSettings.Count > 0)
        {
            package.WriteXml("settings.xml", Settings(fonts, additionalSettings));
            yield return ("settings.xml", "text/xml");
        }
    }

    private void Classify(XElement face, FontFaceTraits traits)
    {
        face.Add(new XAttribute(Namespaces.Style + "font-family-generic", traits.Generic switch
        {
            FontGenericFamily.Swiss => "swiss",
            FontGenericFamily.Modern => "modern",
            FontGenericFamily.Script => "script",
            FontGenericFamily.Decorative => "decorative",
            FontGenericFamily.System => "system",
            _ => "roman",
        }));
        if (traits.Pitch != FontPitch.Unknown)
            face.Add(new XAttribute(Namespaces.Style + "font-pitch", traits.Pitch == FontPitch.Fixed ? "fixed" : "variable"));
    }

    private string Quoted(string family) => family.Contains(' ', StringComparison.Ordinal) ? $"'{family}'" : family;

    private string UniquePath(EmbeddedFontFile file, HashSet<string> used)
    {
        var stem = new string([.. file.Family.Where(char.IsLetterOrDigit)]);
        if (stem.Length == 0) stem = "Font";
        var name = $"{stem}-{file.StyleName.Replace(" ", string.Empty, StringComparison.Ordinal)}";
        var path = $"Fonts/{name}.ttf";
        for (var index = 2; !used.Add(path); index++) path = $"Fonts/{name}{index.ToString(CultureInfo.InvariantCulture)}.ttf";
        return path;
    }
}

public sealed class OdfFontFaceReader
{
    private static readonly XNamespace Config = "urn:oasis:names:tc:opendocument:xmlns:config:1.0";

    public DocumentFonts Read(OdfPackage package, params XDocument?[] documents)
    {
        ArgumentNullException.ThrowIfNull(package);
        var files = new List<EmbeddedFontFile>();
        var traits = new List<FontFaceTraits>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var face in documents.OfType<XDocument>().SelectMany(document => document.Descendants(Namespaces.Style + "font-face")))
        {
            var family = Unquote((string?)face.Attribute(Namespaces.Svg + "font-family") ?? (string?)face.Attribute(Namespaces.Style + "name") ?? string.Empty);
            if (family.Length == 0) continue;
            if (Traits(family, face) is { } found && !traits.Any(known => known.Family.Equals(family, StringComparison.OrdinalIgnoreCase))) traits.Add(found);

            foreach (var uri in face.Descendants(Namespaces.Svg + "font-face-uri"))
            {
                var href = ((string?)uri.Attribute(Namespaces.Xlink + "href") ?? string.Empty).TrimStart('.', '/');
                if (href.Length == 0 || href.Contains("://", StringComparison.Ordinal) || !seen.Add(href)) continue;

                var format = (string?)uri.Element(Namespaces.Svg + "font-face-format")?.Attribute(Namespaces.Svg + "string");
                if (format is "embedded-opentype") continue;
                if (package.ReadBytes(href) is not { Length: > 12 } data) continue;

                files.Add(new EmbeddedFontFile(family, Weight((string?)uri.Attribute(Namespaces.Loext + "font-weight")),
                    (string?)uri.Attribute(Namespaces.Loext + "font-style") is "italic" or "oblique", Path.GetFileName(href), data));
            }
        }

        var settings = package.ReadXml("settings.xml");
        string? Flag(string name) => settings?.Descendants(Config + "config-item")
            .FirstOrDefault(item => (string?)item.Attribute(Config + "name") == name)?.Value;

        var embedSystem = (Flag("EmbedPulseOfficeSystemFonts") ?? Flag("EmbedFonts")) == "true";
        return new DocumentFonts(embedSystem, files) { Traits = traits };
    }

    private FontFaceTraits? Traits(string family, XElement face)
    {
        var generic = (string?)face.Attribute(Namespaces.Style + "font-family-generic");
        var pitch = (string?)face.Attribute(Namespaces.Style + "font-pitch");
        if (generic is null && pitch is null) return null;
        return new FontFaceTraits(family,
            generic switch
            {
                "roman" => FontGenericFamily.Roman,
                "swiss" => FontGenericFamily.Swiss,
                "modern" => FontGenericFamily.Modern,
                "script" => FontGenericFamily.Script,
                "decorative" => FontGenericFamily.Decorative,
                "system" => FontGenericFamily.System,
                _ => FontGenericFamily.Unknown,
            },
            pitch switch
            {
                "fixed" => FontPitch.Fixed,
                "variable" => FontPitch.Variable,
                _ => FontPitch.Unknown,
            });
    }

    private int Weight(string? value) => value switch
    {
        "bold" => 700,
        "normal" or null or "" => 400,
        _ => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var weight) ? Math.Clamp(weight, 1, 1000) : 400,
    };

    private string Unquote(string family) => family.Trim().Trim('\'', '"');
}

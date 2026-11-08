using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Formats.OpenXml.DrawingML;
using PulseOffice.Presentations;
using A = DocumentFormat.OpenXml.Drawing;
using W = DocumentFormat.OpenXml.Wordprocessing;
using WPS = DocumentFormat.OpenXml.Office2010.Word.DrawingShape;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

internal sealed class DocxThemeColours
{
    private readonly OoxmlValues _values = new();
    private readonly SchemeRoles _schemes = new();
    private readonly OoxmlThemeScheme _scheme;
    private readonly DrawingShapeColourNormalizer _normalizer;

    public DocxThemeColours()
    {
        _scheme = new OoxmlThemeScheme(_values);
        _normalizer = new DrawingShapeColourNormalizer(new RunColorReader(_values, _schemes, new PresetColors()), new DrawingColourModifiers());
    }

    public void Resolve(MainDocumentPart main)
    {
        if (main.ThemePart?.Theme?.ThemeElements?.ColorScheme is not { } scheme) return;
        var roots = new List<OpenXmlPartRootElement?> { main.Document, main.FootnotesPart?.Footnotes, main.EndnotesPart?.Endnotes };
        roots.AddRange(main.HeaderParts.Select(part => (OpenXmlPartRootElement?)part.Header));
        roots.AddRange(main.FooterParts.Select(part => (OpenXmlPartRootElement?)part.Footer));
        using var scope = _schemes.Enter(OoxmlColorMap.Standard, OoxmlColorMap.Standard, _scheme.Colours(scheme));
        foreach (var root in roots.OfType<OpenXmlPartRootElement>())
        {
            _normalizer.Normalize(root, main.ThemePart, () => null);
            foreach (var shape in root.Descendants<WPS.WordprocessingShape>())
                if (shape.GetFirstChild<WPS.ShapeStyle>()?.FontReference?.GetFirstChild<A.RgbColorModelHex>()?.Val?.Value is { Length: 6 } hex)
                    Colour(shape, hex);
        }
    }

    private void Colour(WPS.WordprocessingShape shape, string hex)
    {
        foreach (var run in shape.GetFirstChild<WPS.TextBoxInfo2>()?.Descendants<W.Run>() ?? [])
        {
            var properties = run.RunProperties;
            if (properties?.Color is not null || properties?.RunStyle is not null) continue;
            properties ??= run.PrependChild(new W.RunProperties());
            properties.Append(new W.Color { Val = hex });
        }
    }
}

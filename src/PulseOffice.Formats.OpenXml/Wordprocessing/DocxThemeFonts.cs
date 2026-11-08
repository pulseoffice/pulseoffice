using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxThemeFonts
{
    public void Resolve(MainDocumentPart main)
    {
        if (main.ThemePart?.Theme?.ThemeElements?.FontScheme is not { } scheme) return;
        var roots = new List<OpenXmlPartRootElement?>
        {
            main.Document, main.StyleDefinitionsPart?.Styles, main.NumberingDefinitionsPart?.Numbering,
            main.FootnotesPart?.Footnotes, main.EndnotesPart?.Endnotes, main.WordprocessingCommentsPart?.Comments,
        };
        roots.AddRange(main.HeaderParts.Select(part => (OpenXmlPartRootElement?)part.Header));
        roots.AddRange(main.FooterParts.Select(part => (OpenXmlPartRootElement?)part.Footer));
        foreach (var fonts in roots.OfType<OpenXmlPartRootElement>().SelectMany(root => root.Descendants<W.RunFonts>()))
        {
            fonts.Ascii ??= Typeface(scheme, fonts.AsciiTheme?.Value);
            fonts.HighAnsi ??= Typeface(scheme, fonts.HighAnsiTheme?.Value);
            fonts.EastAsia ??= Typeface(scheme, fonts.EastAsiaTheme?.Value);
            fonts.ComplexScript ??= Typeface(scheme, fonts.ComplexScriptTheme?.Value);
        }
    }

    private StringValue? Typeface(A.FontScheme scheme, W.ThemeFontValues? theme)
    {
        if (theme is not { } value) return null;
        var major = value == W.ThemeFontValues.MajorAscii || value == W.ThemeFontValues.MajorHighAnsi
                    || value == W.ThemeFontValues.MajorEastAsia || value == W.ThemeFontValues.MajorBidi;
        var collection = major ? (A.FontCollectionType?)scheme.MajorFont : scheme.MinorFont;
        var face = value == W.ThemeFontValues.MinorEastAsia || value == W.ThemeFontValues.MajorEastAsia
            ? collection?.EastAsianFont?.Typeface?.Value
            : value == W.ThemeFontValues.MinorBidi || value == W.ThemeFontValues.MajorBidi
                ? collection?.ComplexScriptFont?.Typeface?.Value
                : collection?.LatinFont?.Typeface?.Value;
        return face is { Length: > 0 } ? new StringValue(face) : null;
    }
}

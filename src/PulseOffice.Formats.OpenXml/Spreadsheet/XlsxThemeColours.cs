using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Formats.OpenXml.DrawingML;

namespace PulseOffice.Formats.OpenXml;

internal sealed class XlsxThemeColours
{
    private readonly OoxmlValues _values = new();
    private readonly SchemeRoles _schemes = new();
    private readonly OoxmlThemeScheme _scheme;
    private readonly DrawingShapeColourNormalizer _normalizer;

    public XlsxThemeColours()
    {
        _scheme = new OoxmlThemeScheme(_values);
        _normalizer = new DrawingShapeColourNormalizer(new RunColorReader(_values, _schemes, new PresetColors()), new DrawingColourModifiers());
    }

    public void Resolve(WorkbookPart workbook)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        var scheme = workbook.ThemePart?.Theme?.ThemeElements?.ColorScheme;
        using var scope = _schemes.Enter(OoxmlColorMap.Standard, OoxmlColorMap.Standard, _scheme.Colours(scheme ?? new DocumentFormat.OpenXml.Drawing.ColorScheme()));
        foreach (var drawing in workbook.WorksheetParts.Select(part => part.DrawingsPart?.WorksheetDrawing).OfType<DocumentFormat.OpenXml.Drawing.Spreadsheet.WorksheetDrawing>())
            _normalizer.Normalize(drawing, workbook.ThemePart, () => null);
    }
}

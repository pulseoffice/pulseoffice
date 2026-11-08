using PulseOffice.Document.Themes;
namespace PulseOffice.Sheets.Styling;

public interface ISheetThemeApplier
{
    int Apply(Workbook workbook, ThemePalette theme);
}

public sealed class SheetThemeApplier(ICellStyleDelta delta) : ISheetThemeApplier
{
    public int Apply(Workbook workbook, ThemePalette theme)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(theme);

        var catalog = workbook.NamedStyles;
        var previous = catalog.All.DistinctBy(named => named.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(named => named.Name, catalog.StyleOf, StringComparer.OrdinalIgnoreCase);

        foreach (var named in catalog.All.ToList())
        {
            var own = workbook.Styles.Get(named.StyleId);
            catalog.Define(named.Name, Themed(named.Name, own, theme), named.Parent, named.IsBuiltIn);
        }

        var restyled = 0;

        foreach (var sheet in workbook.Sheets.Where(sheet => !sheet.Protection.IsEnabled))
            foreach (var (address, cell) in sheet.NonEmptyCells().ToList())
            {
                if (cell.StyleName is not { Length: > 0 } name || catalog.Find(name) is not { } named
                    || !previous.TryGetValue(name, out var before)) continue;

                var current = workbook.Styles.Get(cell.StyleId);
                var style = delta.Apply(before, catalog.StyleOf(named), current);
                if (style == current) continue;
                sheet.SetStyle(address, workbook.Styles.Intern(style));
                restyled++;
            }

        return restyled;
    }

    private CellStyle Themed(string name, CellStyle style, ThemePalette theme)
    {
        var font = style.Font with { Family = theme.FontFamily };

        return name switch
        {
            "Heading" or "Heading 1" or "Heading 2" or "Result" => style with { Font = font with { ColorHex = theme.HeadingColor } },
            "Accent 1" => style with { Font = font with { ColorHex = "#FFFFFF" }, BackgroundHex = theme.Accent1 },
            "Accent 2" => style with { Font = font with { ColorHex = "#FFFFFF" }, BackgroundHex = theme.Accent2 },
            "Accent 3" => style with { Font = font with { ColorHex = "#FFFFFF" }, BackgroundHex = theme.Accent3 },
            "Note" => style with { Font = font, BackgroundHex = theme.NoteBackground },
            _ => style with { Font = font },
        };
    }
}

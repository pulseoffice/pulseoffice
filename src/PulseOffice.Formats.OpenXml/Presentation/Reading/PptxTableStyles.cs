using PulseOffice.Foundation.Colors;
using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Document.Embedding;
using PulseOffice.Formats.OpenXml.DrawingML;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml;

internal sealed class PptxBuiltInTableStyles
{
    private const string Medium2 =
        "<a:tblStyle xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" styleId=\"{0}\" styleName=\"Medium Style 2\">" +
        "<a:wholeTbl><a:tcTxStyle><a:fontRef idx=\"minor\"/><a:schemeClr val=\"dk1\"/></a:tcTxStyle><a:tcStyle><a:tcBdr>" +
        "<a:left><a:ln w=\"12700\"><a:solidFill><a:schemeClr val=\"lt1\"/></a:solidFill></a:ln></a:left>" +
        "<a:right><a:ln w=\"12700\"><a:solidFill><a:schemeClr val=\"lt1\"/></a:solidFill></a:ln></a:right>" +
        "<a:top><a:ln w=\"12700\"><a:solidFill><a:schemeClr val=\"lt1\"/></a:solidFill></a:ln></a:top>" +
        "<a:bottom><a:ln w=\"12700\"><a:solidFill><a:schemeClr val=\"lt1\"/></a:solidFill></a:ln></a:bottom>" +
        "<a:insideH><a:ln w=\"12700\"><a:solidFill><a:schemeClr val=\"lt1\"/></a:solidFill></a:ln></a:insideH>" +
        "<a:insideV><a:ln w=\"12700\"><a:solidFill><a:schemeClr val=\"lt1\"/></a:solidFill></a:ln></a:insideV>" +
        "</a:tcBdr><a:fill><a:solidFill><a:schemeClr val=\"{1}\"><a:tint val=\"20000\"/></a:schemeClr></a:solidFill></a:fill></a:tcStyle></a:wholeTbl>" +
        "<a:band1H><a:tcStyle><a:tcBdr/><a:fill><a:solidFill><a:schemeClr val=\"{1}\"><a:tint val=\"40000\"/></a:schemeClr></a:solidFill></a:fill></a:tcStyle></a:band1H>" +
        "<a:band1V><a:tcStyle><a:tcBdr/><a:fill><a:solidFill><a:schemeClr val=\"{1}\"><a:tint val=\"40000\"/></a:schemeClr></a:solidFill></a:fill></a:tcStyle></a:band1V>" +
        "<a:lastCol><a:tcTxStyle b=\"on\"><a:fontRef idx=\"minor\"/><a:schemeClr val=\"lt1\"/></a:tcTxStyle><a:tcStyle><a:tcBdr/><a:fill><a:solidFill><a:schemeClr val=\"{1}\"/></a:solidFill></a:fill></a:tcStyle></a:lastCol>" +
        "<a:firstCol><a:tcTxStyle b=\"on\"><a:fontRef idx=\"minor\"/><a:schemeClr val=\"lt1\"/></a:tcTxStyle><a:tcStyle><a:tcBdr/><a:fill><a:solidFill><a:schemeClr val=\"{1}\"/></a:solidFill></a:fill></a:tcStyle></a:firstCol>" +
        "<a:lastRow><a:tcTxStyle b=\"on\"><a:fontRef idx=\"minor\"/><a:schemeClr val=\"lt1\"/></a:tcTxStyle><a:tcStyle><a:tcBdr><a:top><a:ln w=\"38100\"><a:solidFill><a:schemeClr val=\"lt1\"/></a:solidFill></a:ln></a:top></a:tcBdr><a:fill><a:solidFill><a:schemeClr val=\"{1}\"/></a:solidFill></a:fill></a:tcStyle></a:lastRow>" +
        "<a:firstRow><a:tcTxStyle b=\"on\"><a:fontRef idx=\"minor\"/><a:schemeClr val=\"lt1\"/></a:tcTxStyle><a:tcStyle><a:tcBdr><a:bottom><a:ln w=\"38100\"><a:solidFill><a:schemeClr val=\"lt1\"/></a:solidFill></a:ln></a:bottom></a:tcBdr><a:fill><a:solidFill><a:schemeClr val=\"{1}\"/></a:solidFill></a:fill></a:tcStyle></a:firstRow>" +
        "</a:tblStyle>";

    private readonly IReadOnlyDictionary<string, string?> _medium2 = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
    {
        ["{073A0DAA-6AF3-43AB-8588-CEC1D06C72B9}"] = "dk1",
        ["{5C22544A-7EE6-4342-B048-85BDC9FD1C3A}"] = "accent1",
        ["{21E4AEA4-8DFA-4A89-87EB-49C32662AFE0}"] = "accent2",
        ["{F5AB1C69-6EDB-4FF4-983F-18BD219EF322}"] = "accent3",
        ["{00A15C55-8517-42AA-B614-E9B94910E393}"] = "accent4",
        ["{7DF18680-E054-41AD-8BC1-D1AEF772440D}"] = "accent5",
        ["{93296810-A885-4BE3-A3E7-6D5BEEA58F35}"] = "accent6",
    };

    private readonly HashSet<string> _plain = new(StringComparer.OrdinalIgnoreCase) { "{2D5ABB26-0587-4C30-8999-92F81FD0307C}" };

    public bool IsPlain(string id) => _plain.Contains(id);

    public OpenXmlCompositeElement? Find(string id) =>
        _medium2.TryGetValue(id, out var accent)
            ? new D.TableStyleEntry(string.Format(CultureInfo.InvariantCulture, Medium2, id, accent))
            : null;
}

internal sealed record TableFlags(bool FirstRow, bool LastRow, bool FirstColumn, bool LastColumn, bool BandRows, bool BandColumns);

internal sealed class PptxTableStyleResolver(IDrawingColorReader colours, DrawingColourModifiers modifiers, PptxBuiltInTableStyles builtIn)
{

    public OpenXmlCompositeElement? Style(D.Table table, PresentationPart? presentation)
    {
        var properties = table.TableProperties;
        if (properties?.GetFirstChild<D.TableStyle>() is { } inline) return inline;
        if (properties?.GetFirstChild<D.TableStyleId>()?.Text is not { Length: > 0 } id) return null;
        if (presentation?.TableStylesPart?.TableStyleList?.Elements<D.TableStyleEntry>()
                .FirstOrDefault(entry => string.Equals(entry.StyleId?.Value, id, StringComparison.OrdinalIgnoreCase)) is { } entry)
            return entry;
        return builtIn.Find(id);
    }

    public bool HasLook(D.Table table, PresentationPart? presentation) =>
        Style(table, presentation) is not null
        || table.Descendants<D.TableCellProperties>().Any(cell => cell.HasChildren)
        || table.Descendants<D.RunProperties>().Any(Formatted)
        || table.TableProperties?.GetFirstChild<D.TableStyleId>()?.Text is { } id && builtIn.IsPlain(id);

    private bool Formatted(D.RunProperties run) =>
        run.Bold is not null || run.Italic is not null || run.Underline is not null || run.Strike is not null || run.FontSize is not null
        || run.GetFirstChild<D.SolidFill>() is not null || run.GetFirstChild<D.LatinFont>() is not null;

    public TableCellLook Look(OpenXmlCompositeElement? style, TableFlags flags, D.TableCell cell, int row, int column, int rows, int columns, ThemePart? theme = null)
    {
        var look = TableCellLook.Plain;
        var backdrop = Backdrop(style, theme);
        foreach (var part in Parts(style, flags, row, column, rows, columns))
            look = Apply(look, part, row, column, rows, columns, theme, backdrop);
        return Direct(look, cell);
    }

    private uint Backdrop(OpenXmlCompositeElement? style, ThemePart? theme)
    {
        if (style?.GetFirstChild<D.TableBackground>() is not { } background) return White;
        return Painted(background, theme) is { } argb ? Over(argb, White) : White;
    }

    private uint? Painted(OpenXmlCompositeElement owner, ThemePart? theme)
    {
        if (owner.GetFirstChild<D.FillProperties>() is { } fill)
            return fill.GetFirstChild<D.SolidFill>() is { } solid ? Argb(solid) : null;
        if (owner.GetFirstChild<D.FillReference>() is not { } reference
            || reference.Index?.Value is not (> 0 and var index)
            || Argb(reference) is not { } placeholder) return null;
        var themed = theme?.Theme?.ThemeElements?.FormatScheme?.FillStyleList?.ChildElements.ElementAtOrDefault((int)index - 1);
        var colours = themed switch
        {
            D.SolidFill solid => solid.ChildElements.Take(1).ToList(),
            D.GradientFill gradient => [.. gradient.Descendants<D.GradientStop>().Select(stop => stop.FirstChild).OfType<OpenXmlElement>()],
            _ => [],
        };
        if (colours.Count == 0) return placeholder;
        var resolved = colours.Select(colour => colour is D.SchemeColor { Val.Value: var value } && value == D.SchemeColorValues.PhColor
            ? modifiers.Apply(placeholder, colour)
            : Argb(colour.Parent!) ?? placeholder).ToList();
        uint Average(int shift) => (uint)Math.Round(resolved.Average(argb => (double)((argb >> shift) & 0xFF)));
        return (Average(24) << 24) | (Average(16) << 16) | (Average(8) << 8) | Average(0);
    }

    private OpenXmlCompositeElement? Outline(OpenXmlCompositeElement? border, ThemePart? theme)
    {
        if (border is null) return null;
        if (border.GetFirstChild<D.Outline>() is { } outline) return outline;
        if (border.GetFirstChild<D.LineReference>() is not { Index.Value: > 0 and var index } reference) return null;
        if (theme?.Theme?.ThemeElements?.FormatScheme?.LineStyleList?.Elements<D.Outline>().ElementAtOrDefault((int)index - 1) is not { } themed) return null;
        var copy = (D.Outline)themed.CloneNode(true);
        if (Argb(reference) is { } placeholder)
            foreach (var colour in copy.Descendants<D.SchemeColor>().Where(colour => colour.Val?.Value == D.SchemeColorValues.PhColor).ToList())
                colour.Parent!.ReplaceChild(new D.RgbColorModelHex { Val = Hex(modifiers.Apply(placeholder, colour)) }, colour);
        return copy;
    }

    private const uint White = 0xFFFFFFFF;
    private const long MaximumLineEmu = 20116800;

    private uint Over(uint top, uint bottom)
    {
        var alpha = (top >> 24) / 255.0;
        uint Channel(int shift) => (uint)Math.Round(((top >> shift) & 0xFF) * alpha + ((bottom >> shift) & 0xFF) * (1 - alpha));
        return 0xFF000000u | (Channel(16) << 16) | (Channel(8) << 8) | Channel(0);
    }

    private IEnumerable<OpenXmlCompositeElement> Parts(OpenXmlCompositeElement? style, TableFlags flags, int row, int column, int rows, int columns)
    {
        if (style is null) yield break;
        if (style.GetFirstChild<D.WholeTable>() is { } whole) yield return whole;
        var bodyRow = row - (flags.FirstRow ? 1 : 0);
        var bodyColumn = column - (flags.FirstColumn ? 1 : 0);
        if (flags.BandRows && bodyRow >= 0)
            if ((bodyRow % 2 == 0 ? (OpenXmlCompositeElement?)style.GetFirstChild<D.Band1Horizontal>() : style.GetFirstChild<D.Band2Horizontal>()) is { } band) yield return band;
        if (flags.BandColumns && bodyColumn >= 0)
            if ((bodyColumn % 2 == 0 ? (OpenXmlCompositeElement?)style.GetFirstChild<D.Band1Vertical>() : style.GetFirstChild<D.Band2Vertical>()) is { } band) yield return band;
        if (flags.LastColumn && column == columns - 1 && style.GetFirstChild<D.LastColumn>() is { } last) yield return last;
        if (flags.FirstColumn && column == 0 && style.GetFirstChild<D.FirstColumn>() is { } first) yield return first;
        if (flags.LastRow && row == rows - 1 && style.GetFirstChild<D.LastRow>() is { } lastRow) yield return lastRow;
        if (flags.FirstRow && row == 0 && style.GetFirstChild<D.FirstRow>() is { } firstRow) yield return firstRow;
    }

    private TableCellLook Apply(TableCellLook look, OpenXmlCompositeElement part, int row, int column, int rows, int columns, ThemePart? theme, uint backdrop)
    {
        if (part.GetFirstChild<D.TableCellTextStyle>() is { } text)
        {
            if (text.Bold?.Value == D.BooleanStyleValues.On) look = look with { Bold = true };
            else if (text.Bold?.Value == D.BooleanStyleValues.Off) look = look with { Bold = false };
            if (Colour(text) is { } ink) look = look with { TextHex = ink };
        }
        if (part.GetFirstChild<D.TableCellStyle>() is not { } cellStyle) return look;
        if (cellStyle.GetFirstChild<D.FillProperties>() is { } explicitFill && explicitFill.GetFirstChild<D.NoFill>() is not null)
            look = look with { FillHex = backdrop == White ? null : "#" + Hex(backdrop) };
        else if (Painted(cellStyle, theme) is { } fill)
            look = look with { FillHex = "#" + Hex(Over(fill, backdrop)) };
        else if (part is D.WholeTable && backdrop != White)
            look = look with { FillHex = "#" + Hex(backdrop) };
        if (cellStyle.TableCellBorders is { } borders)
        {
            var isWhole = part is D.WholeTable;
            look = look with
            {
                Left = Line(Outline(column == 0 || !isWhole ? borders.LeftBorder : borders.InsideVerticalBorder, theme), look.Left),
                Right = Line(Outline(column == columns - 1 || !isWhole ? borders.RightBorder : borders.InsideVerticalBorder, theme), look.Right),
                Top = Line(Outline(row == 0 || !isWhole ? borders.TopBorder : borders.InsideHorizontalBorder, theme), look.Top),
                Bottom = Line(Outline(row == rows - 1 || !isWhole ? borders.BottomBorder : borders.InsideHorizontalBorder, theme), look.Bottom),
            };
        }
        return look;
    }

    private TableCellLook Direct(TableCellLook look, D.TableCell cell)
    {
        if (cell.TableCellProperties is { } properties)
        {
            if (properties.GetFirstChild<D.NoFill>() is not null) look = look with { FillHex = null };
            else if (properties.GetFirstChild<D.SolidFill>() is { } solid && Colour(solid) is { } fill) look = look with { FillHex = fill };
            look = look with
            {
                Left = Line(properties.LeftBorderLineProperties, look.Left),
                Right = Line(properties.RightBorderLineProperties, look.Right),
                Top = Line(properties.TopBorderLineProperties, look.Top),
                Bottom = Line(properties.BottomBorderLineProperties, look.Bottom),
            };
        }
        var paragraph = cell.TextBody?.Elements<D.Paragraph>().FirstOrDefault();
        var run = paragraph?.Elements<D.Run>().FirstOrDefault()?.RunProperties;
        if (run?.FontSize?.Value is >= 100 and <= 400000 and var size) look = look with { FontSize = size / 100.0 };
        if (run?.Bold?.Value is { } bold) look = look with { Bold = bold };
        if (run?.Italic?.Value is { } italic) look = look with { Italic = italic };
        if (run?.Underline?.Value is { } underline) look = look with { Underline = underline != D.TextUnderlineValues.None };
        if (run?.Strike?.Value is { } strike) look = look with { Strikethrough = strike != D.TextStrikeValues.NoStrike };
        if (run?.GetFirstChild<D.LatinFont>()?.Typeface?.Value is { Length: > 0 } family && !family.StartsWith('+')) look = look with { FontFamily = family };
        if (run?.GetFirstChild<D.SolidFill>() is { } ink && Colour(ink) is { } text) look = look with { TextHex = text };
        var align = paragraph?.ParagraphProperties?.Alignment?.Value;
        if (align == D.TextAlignmentTypeValues.Center) look = look with { Horizontal = CellHorizontalAlign.Center };
        else if (align == D.TextAlignmentTypeValues.Right) look = look with { Horizontal = CellHorizontalAlign.Right };
        return look;
    }

    private TableBorderLine? Line(OpenXmlCompositeElement? outline, TableBorderLine? current)
    {
        if (outline is null) return current;
        if (outline.GetFirstChild<D.NoFill>() is not null) return null;
        if (outline.GetFirstChild<D.SolidFill>() is not { } solid || Colour(solid) is not { } colour) return current;
        var width = outline.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == "w").Value is { } text
            && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var emu) && emu >= 0 ? Math.Min(emu, MaximumLineEmu) / ((double)PulseOffice.Foundation.Units.Emu.PerPoint) : 0.75;
        return new TableBorderLine(colour, width);
    }

    private string? Colour(OpenXmlElement container) => Argb(container) is { } argb ? "#" + Hex(argb) : null;

    private uint? Argb(OpenXmlElement container)
    {
        var element = container.ChildElements.FirstOrDefault(child => child is D.RgbColorModelHex or D.SchemeColor or D.PresetColor or D.SystemColor);
        if (element is null || colours.Read(container) is not { } argb) return null;
        return modifiers.Apply(argb | 0xFF000000u, element);
    }

    private string Hex(uint argb) => new ArgbColor(argb).ToRgbDigits();
}

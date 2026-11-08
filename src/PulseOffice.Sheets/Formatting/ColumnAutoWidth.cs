using PulseOffice.Foundation.Units;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Formatting;

public interface IColumnAutoWidth
{
    Twip? Measure(Workbook workbook, Worksheet sheet, int column, CellRange area, double extraPoints);
}

public sealed class ColumnAutoWidth(ICellTextMeasure measure, ICellPresenter presenter) : IColumnAutoWidth
{
    private const double UnlimitedCharacters = 1024;

    public Twip? Measure(Workbook workbook, Worksheet sheet, int column, CellRange area, double extraPoints)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(sheet);

        var columnArea = new CellRange(new CellAddress(area.Start.Row, column), new CellAddress(area.End.Row, column));
        var widest = sheet.NonEmptyCellsIn(columnArea)
            .Where(pair => !sheet.Layout.IsRowHidden(pair.Key.Row) && SingleColumn(sheet, pair.Key))
            .Select(pair => ContentWidth(workbook, sheet, pair.Key, pair.Value))
            .OfType<double>()
            .DefaultIfEmpty(0)
            .Max();

        if (widest <= 0) return null;
        return new Twip((int)Math.Ceiling(widest / CellBoxMetrics.PixelsPerTwip) + Twip.FromPoints(extraPoints).Value);
    }

    private bool SingleColumn(Worksheet sheet, CellAddress address) =>
        sheet.Merges.At(address) is not { } region || (region.Start == address && region.End.Column == region.Start.Column);

    private double? ContentWidth(Workbook workbook, Worksheet sheet, CellAddress address, Cell cell)
    {
        if (cell.Value.IsEmpty && !cell.HasFormula) return null;

        var styled = cell with { StyleId = sheet.StyleIdAt(address) };
        var style = workbook.Styles.Get(styled.StyleId);
        var text = presenter.Present(styled, workbook.Styles, workbook.NumberFormats, UnlimitedCharacters).Text;
        if (string.IsNullOrEmpty(text)) return null;

        var longest = text.Split('\n').MaxBy(line => line.Length) ?? text;
        return measure.Measure(longest, style.Font, null, style.RotationDegrees).Width
            + (CellBoxMetrics.CellPadding + style.Indent * CellBoxMetrics.IndentStep) * 2;
    }
}

public static class CellBoxMetrics
{
    public const double PixelsPerTwip = 96.0 / Twip.PerInch;
    public const double VerticalPadding = 4;
    public const double CellPadding = 3;
    public const double IndentStep = 8;
}

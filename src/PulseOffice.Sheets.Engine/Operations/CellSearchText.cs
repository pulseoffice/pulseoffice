using System.Globalization;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

public interface ICellSearchText
{
    string Text(Worksheet sheet, Cell cell);

    CellValue Parse(string text);
}

public sealed class StoredCellSearchText : ICellSearchText
{
    public string Text(Worksheet sheet, Cell cell) => cell.Value.ToString();

    public CellValue Parse(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? CellValue.Number(number)
            : CellValue.Text(text);
}

public sealed class PresentedCellSearchText(ICellPresenter presenter, ISpreadsheetLocale locale) : ICellSearchText
{
    private const double UnboundedWidth = 1000;

    public string Text(Worksheet sheet, Cell cell)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(cell);
        if (cell.Value.Kind == ValueKind.Text || sheet.Owner is not { } workbook) return cell.Value.ToString();
        return presenter.Present(cell, workbook.Styles, workbook.NumberFormats, UnboundedWidth).Text;
    }

    public CellValue Parse(string text) =>
        double.TryParse(text, NumberStyles.Float, locale.Culture, out var number)
            ? CellValue.Number(number)
            : CellValue.Text(text);
}

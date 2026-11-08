using Microsoft.Extensions.Logging;
using PulseOffice.Exchange;
using PulseOffice.Formats;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.UI.Exchange;

public interface ISheetRangeCapture
{
    CellRangeContent Grid(Workbook workbook, Worksheet sheet, CellRange range);

    IReadOnlyList<ExchangeRepresentation> Packages(Workbook workbook, Worksheet sheet, CellRange range);

    CellRange Occupied(Worksheet sheet, CellRange range);
}

public sealed class SheetRangeCapture(ICellPresenter presenter, ISpreadsheetFormatRegistry formats, IExchangeCellStyles styles, ILogger<SheetRangeCapture> logger) : ISheetRangeCapture
{
    private const double ColumnWidthInCharacters = 15;

    private readonly IReadOnlyList<(string FormatId, ExchangeFlavor Flavor, string MediaType)> _packages =
    [
        ("ods", ExchangeFlavor.OpenDocument, ExchangeMediaTypes.Spreadsheet),
        ("xlsx", ExchangeFlavor.OpenXml, ExchangeMediaTypes.Workbook),
    ];

    public CellRangeContent Grid(Workbook workbook, Worksheet sheet, CellRange range)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(sheet);
        range = Occupied(sheet, range);
        var rows = new List<IReadOnlyList<ExchangeCell>>(range.RowCount);
        for (var row = range.Start.Row; row <= range.End.Row; row++)
        {
            var cells = new List<ExchangeCell>(range.ColumnCount);
            for (var column = range.Start.Column; column <= range.End.Column; column++)
                cells.Add(Cell(workbook, sheet, new CellAddress(row, column)));
            rows.Add(cells);
        }
        return new CellRangeContent
        {
            SheetName = sheet.Name,
            FirstRow = range.Start.Row,
            FirstColumn = range.Start.Column,
            RowCount = range.RowCount,
            ColumnCount = range.ColumnCount,
            Rows = rows,
            Merges = [.. sheet.Merges.All
                .Where(region => range.Contains(region.Start) && range.Contains(region.End))
                .Select(region => new ExchangeMerge(region.Start.Row - range.Start.Row, region.Start.Column - range.Start.Column, region.RowCount, region.ColumnCount))],
        };
    }

    public IReadOnlyList<ExchangeRepresentation> Packages(Workbook workbook, Worksheet sheet, CellRange range)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(sheet);
        var portion = Portion(workbook, sheet, Occupied(sheet, range));
        var result = new List<ExchangeRepresentation>();
        foreach (var (formatId, flavor, mediaType) in _packages)
        {
            if (formats.WritableFormats.FirstOrDefault(format => format.Id == formatId) is not { } format || formats.WriterFor(format) is not { } writer) continue;
            using var stream = new MemoryStream();
            var written = writer.Write(portion, stream);
            if (written.Succeeded) result.Add(new ExchangeRepresentation(flavor, stream.ToArray()) { MediaType = mediaType });
            else logger.LogWarning("Writing the copied range as {Format} failed", formatId);
        }
        return result;
    }

    public CellRange Occupied(Worksheet sheet, CellRange range)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var bottom = Math.Min(range.End.Row, Math.Max(sheet.LastFormattedRow, range.Start.Row));
        var right = Math.Min(range.End.Column, Math.Max(sheet.LastFormattedColumn, range.Start.Column));
        return new CellRange(range.Start, new CellAddress(bottom, right));
    }

    private Workbook Portion(Workbook workbook, Worksheet sheet, CellRange range)
    {
        var portion = new Workbook();
        var target = portion.AddSheet(sheet.Name);
        foreach (var address in range.Cells())
        {
            var cell = sheet[address];
            var styleId = sheet.StyleIdAt(address);
            if (cell.Value.IsEmpty && !cell.HasFormula && styleId == StyleCatalog.DefaultStyleId) continue;
            target[new CellAddress(address.Row - range.Start.Row, address.Column - range.Start.Column)] = cell with
            {
                StyleId = portion.Styles.Intern(workbook.Styles.Get(styleId)),
                ExplicitStyle = styleId != StyleCatalog.DefaultStyleId,
            };
        }
        return portion;
    }

    private ExchangeCell Cell(Workbook workbook, Worksheet sheet, CellAddress address)
    {
        var cell = sheet[address] with { StyleId = sheet.StyleIdAt(address) };
        var style = workbook.Styles.Get(cell.StyleId);
        var shown = cell.Value.IsEmpty ? FormattedCell.Empty : presenter.Present(cell, workbook.Styles, workbook.NumberFormats, ColumnWidthInCharacters);
        return styles.Describe(new ExchangeCell
        {
            Kind = cell.Value.Kind switch
            {
                ValueKind.Number => ExchangeCellKind.Number,
                ValueKind.Text => ExchangeCellKind.Text,
                ValueKind.Boolean => ExchangeCellKind.Boolean,
                ValueKind.Error => ExchangeCellKind.Error,
                _ => ExchangeCellKind.Empty,
            },
            Text = shown.Text,
            Number = cell.Value.IsNumeric ? cell.Value.AsNumber() : 0,
            Formula = cell.Formula,
            NumberFormat = style.NumberFormatId == 0 ? null : workbook.NumberFormats.CodeOf(style.NumberFormatId),
        }, style);
    }
}

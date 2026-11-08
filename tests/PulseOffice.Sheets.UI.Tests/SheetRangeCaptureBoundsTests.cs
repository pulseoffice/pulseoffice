using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Formats;
using PulseOffice.Foundation.Diagnostics;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.UI.Exchange;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class SheetRangeCaptureBoundsTests
{
    private sealed class NoFormats : ISpreadsheetFormatRegistry
    {
        public IReadOnlyList<SpreadsheetFormat> ReadableFormats => [];

        public IReadOnlyList<SpreadsheetFormat> WritableFormats => [];

        public SpreadsheetFormat Native => throw new NotSupportedException();

        public ISpreadsheetReader? ReaderFor(string path) => null;

        public ISpreadsheetWriter? WriterFor(string path) => null;

        public ISpreadsheetWriter? WriterFor(SpreadsheetFormat format) => null;

        public ISpreadsheetReader? ReaderForContent(Stream stream) => null;

        public OperationResult<Workbook> Read(string path, Stream stream) => throw new NotSupportedException();
    }

    private static SheetRangeCapture Capture() => new(
        new CellPresenter(new NumberFormatter(CultureInfo.InvariantCulture, serial => DateTime.FromOADate(serial))),
        new NoFormats(), new ExchangeCellStyles(), NullLogger<SheetRangeCapture>.Instance);

    [Fact]
    public void CopyingWholeColumnsCapturesOnlyTheUsedRows()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        for (var row = 0; row < 3; row++) sheet.SetValue(new CellAddress(row, 1), CellValue.Number(row));
        var columns = new CellRange(new CellAddress(0, 0), new CellAddress(CellAddress.MaxRows - 1, 1));

        var grid = Capture().Grid(workbook, sheet, columns);

        Assert.Equal(3, grid.RowCount);
        Assert.Equal(3, grid.Rows.Count);
        Assert.Equal(2, grid.ColumnCount);
    }

    [Fact]
    public void CopyingTheWholeSheetDoesNotWalkEveryCell()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(4, 2), CellValue.Number(7));
        var everything = new CellRange(new CellAddress(0, 0), new CellAddress(CellAddress.MaxRows - 1, CellAddress.MaxColumns - 1));

        var grid = Capture().Grid(workbook, sheet, everything);

        Assert.Equal(5, grid.RowCount);
        Assert.Equal(3, grid.ColumnCount);
    }
}

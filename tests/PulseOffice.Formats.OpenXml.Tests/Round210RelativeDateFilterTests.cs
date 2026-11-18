using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using Xunit;
using CellValue = PulseOffice.Sheets.Values.CellValue;
using Workbook = PulseOffice.Sheets.Workbook;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round210RelativeDateFilterTests
{
    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static Workbook Dates()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Text("Date"));
        DateTime[] dates = [new(2026, 11, 18), new(2026, 11, 17), new(2026, 10, 2)];
        for (var index = 0; index < dates.Length; index++)
            sheet.SetValue(new CellAddress(index + 1, 0), CellValue.Number(dates[index].ToOADate()));
        sheet.AutoFilter.Activate(new CellRange(CellAddress.First, new CellAddress(3, 0)));
        return workbook;
    }

    private static IEnumerable<int> Hidden(PulseOffice.Sheets.Worksheet sheet) =>
        new SheetFilterService { Clock = new FixedClock(new DateTime(2026, 11, 18, 12, 0, 0)) }
            .RowsToHide(sheet, sheet.AutoFilter.Range!.Value, sheet.AutoFilter.Filters).Order();

    [Fact]
    public void A_yesterday_filter_without_stored_bounds_is_kept_and_evaluated_against_today()
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(Dates(), stream);
        using (var document = SpreadsheetDocument.Open(stream, true))
            document.WorkbookPart!.WorksheetParts.First().Worksheet!.Descendants<AutoFilter>().Single()
                .Append(new FilterColumn(new DynamicFilter { Type = DynamicFilterValues.Yesterday }) { ColumnId = 0 });
        stream.Position = 0;

        var read = new XlsxReader().Read(stream).Value!.Sheets[0];

        Assert.Equal(RelativeDatePeriod.Yesterday, Assert.Single(read.AutoFilter.Filters).Period);
        Assert.Equal([1, 3], Hidden(read));
    }

    [Fact]
    public void A_relative_period_crosses_xlsx_and_ods()
    {
        var workbook = Dates();
        workbook.Sheets[0].AutoFilter.Set(new ColumnFilter(0) { Period = RelativeDatePeriod.ThisMonth });
        using var xlsx = new MemoryStream();
        new XlsxWriter().Write(workbook, xlsx);
        xlsx.Position = 0;
        var fromXlsx = new XlsxReader().Read(xlsx).Value!;
        using var ods = new MemoryStream();
        new OdsWriter().Write(fromXlsx, ods);
        ods.Position = 0;

        var crossed = new OdsReader().Read(ods).Value!.Sheets[0];

        Assert.Equal(RelativeDatePeriod.ThisMonth, Assert.Single(crossed.AutoFilter.Filters).Period);
        Assert.Equal([3], Hidden(crossed));
    }
}

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Pivot;
using CalculationSettings = PulseOffice.Sheets.CalculationSettings;
using CellValue = PulseOffice.Sheets.Values.CellValue;
using ColumnFilter = PulseOffice.Sheets.ColumnFilter;
using ModelCell = PulseOffice.Sheets.Cell;
using PivotArea = PulseOffice.Sheets.Pivot.PivotArea;
using PivotField = PulseOffice.Sheets.Pivot.PivotField;
using RelativeDatePeriod = PulseOffice.Sheets.RelativeDatePeriod;
using Workbook = PulseOffice.Sheets.Workbook;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round270DateEpochTests
{
    private const double MidMarch2024In1904 = 43904;

    private static Workbook Dated1904(params double[] serials)
    {
        var workbook = Workbook.CreateDefault();
        workbook.Calculation = workbook.Calculation with { DateEpoch = CalculationSettings.Mac1904DateEpoch };
        var sheet = workbook.Sheets[0];
        var date = workbook.Styles.Intern(workbook.Styles.Get(0) with { NumberFormatId = workbook.NumberFormats.Register("yyyy-mm-dd") });
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Day"));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Text("Amount"));
        for (var row = 0; row < serials.Length; row++)
        {
            sheet[new CellAddress(row + 1, 0)] = new ModelCell { Value = CellValue.Number(serials[row]), StyleId = date, ExplicitStyle = true };
            sheet.SetValue(new CellAddress(row + 1, 1), CellValue.Number(row + 1));
        }
        return workbook;
    }

    private static MemoryStream Written(Workbook workbook)
    {
        var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void A_1904_date_filter_is_written_with_its_calendar_date_and_read_back()
    {
        var workbook = Dated1904(MidMarch2024In1904, MidMarch2024In1904 + 1);
        var sheet = workbook.Sheets[0];
        sheet.AutoFilter.Activate(new CellRange(CellAddress.First, new CellAddress(2, 1)));
        var kept = sheet.GetValue(new CellAddress(1, 0)).ToString();
        sheet.AutoFilter.Set(new ColumnFilter(0) { AllowedValues = new HashSet<string> { kept }, IncludeBlanks = false });

        using var stream = Written(workbook);
        using (var document = SpreadsheetDocument.Open(stream, false))
        {
            var group = document.WorkbookPart!.WorksheetParts.Single().Worksheet!.GetFirstChild<AutoFilter>()!.Descendants<DateGroupItem>().Single();
            Assert.Equal((ushort)2024, group.Year!.Value);
            Assert.Equal((ushort)3, group.Month!.Value);
            Assert.Equal((ushort)15, group.Day!.Value);
        }
        stream.Position = 0;
        var read = new XlsxReader().Read(stream).Require();

        Assert.Equal([kept], read.Sheets[0].AutoFilter.Filters.Single().AllowedValues!);
    }

    [Fact]
    public void A_1904_dynamic_filter_writes_its_bounds_in_the_1904_system()
    {
        var workbook = Dated1904(MidMarch2024In1904);
        var sheet = workbook.Sheets[0];
        sheet.AutoFilter.Activate(new CellRange(CellAddress.First, new CellAddress(1, 1)));
        sheet.AutoFilter.Set(new ColumnFilter(0) { Period = RelativeDatePeriod.ThisYear });

        using var stream = Written(workbook);
        using var document = SpreadsheetDocument.Open(stream, false);
        var dynamic = document.WorkbookPart!.WorksheetParts.Single().Worksheet!.Descendants<DynamicFilter>().Single();

        Assert.Equal(workbook.Calculation.SerialOf(new DateTime(DateTime.Today.Year, 1, 1)), dynamic.Val!.Value);
    }

    [Fact]
    public void A_1904_date_grouped_pivot_writes_its_calendar_dates()
    {
        var workbook = Dated1904(MidMarch2024In1904, MidMarch2024In1904 + 40);
        var sheet = workbook.Sheets[0];
        workbook.Pivots.Add(new PivotDefinition
        {
            Id = "p", Name = "P", SourceSheet = sheet.Name,
            SourceRange = new CellRange(CellAddress.First, new CellAddress(2, 1)),
            DestinationSheet = sheet.Name, Destination = new CellAddress(0, 4),
            OutputRange = new CellRange(new CellAddress(0, 4), new CellAddress(4, 5)),
            Fields =
            [
                new PivotField { SourceName = "Day", SourceColumnIndex = 0, Area = PivotArea.Row, DateGrouping = DateGrouping.Months },
                new PivotField { SourceName = "Amount", SourceColumnIndex = 1, Area = PivotArea.Value },
            ],
        });

        using var stream = Written(workbook);
        using (var document = SpreadsheetDocument.Open(stream, false))
        {
            var range = document.WorkbookPart!.PivotTableCacheDefinitionParts.Single().PivotCacheDefinition!.CacheFields!
                .Elements<CacheField>().First().FieldGroup!.GetFirstChild<RangeProperties>()!;
            Assert.Equal(new DateTime(2024, 3, 15), range.StartDate!.Value);
        }
        stream.Position = 0;
        var read = new XlsxReader().Read(stream).Require();

        Assert.Equal(DateGrouping.Months, Assert.Single(read.Pivots).In(PivotArea.Row).Single().DateGrouping);
    }
}

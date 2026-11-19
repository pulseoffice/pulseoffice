using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.Values;
using CellValue = PulseOffice.Sheets.Values.CellValue;
using PivotArea = PulseOffice.Sheets.Pivot.PivotArea;
using PivotField = PulseOffice.Sheets.Pivot.PivotField;
using Workbook = PulseOffice.Sheets.Workbook;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round264XlsxPivotTests
{
    internal static Workbook Pivoted(PivotField value)
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Region"));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Text("Year"));
        sheet.SetValue(new CellAddress(0, 2), CellValue.Text("Amount"));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Text("North"));
        sheet.SetValue(new CellAddress(1, 1), CellValue.Number(2024));
        sheet.SetValue(new CellAddress(1, 2), CellValue.Number(3));
        workbook.Pivots.Add(new PivotDefinition
        {
            Id = "p",
            Name = "P",
            SourceSheet = sheet.Name,
            SourceRange = new CellRange(CellAddress.First, new CellAddress(1, 2)),
            DestinationSheet = sheet.Name,
            Destination = new CellAddress(0, 5),
            OutputRange = new CellRange(new CellAddress(0, 5), new CellAddress(3, 7)),
            Fields =
            [
                new PivotField { SourceName = "Region", SourceColumnIndex = 0, Area = PivotArea.Row },
                new PivotField { SourceName = "Year", SourceColumnIndex = 1, Area = PivotArea.Row, AreaOrder = 1 },
                value,
            ],
        });
        return workbook;
    }

    internal static PivotField Through(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        return Assert.Single(Assert.Single(new XlsxReader().Read(stream).Value!.Pivots).In(PivotArea.Value));
    }

    private static PivotField Amount(ValueDisplayMode mode) =>
        new() { SourceName = "Amount", SourceColumnIndex = 2, Area = PivotArea.Value, DisplayMode = mode };

    [Theory]
    [InlineData(ValueDisplayMode.PercentOfParent)]
    [InlineData(ValueDisplayMode.PercentRunningTotal)]
    [InlineData(ValueDisplayMode.RankDescending)]
    public void Extended_display_modes_cross_xlsx_without_a_declared_loss(ValueDisplayMode mode)
    {
        var workbook = Pivoted(Amount(mode));

        Assert.DoesNotContain(new XlsxSpreadsheetWriter().CheckFidelity(workbook).Losses, loss => loss.Code == "XLSX030");
        Assert.Equal(mode, Through(workbook).DisplayMode);
    }

    [Fact]
    public void The_base_field_crosses_xlsx()
    {
        Assert.Equal("Year", Through(Pivoted(Amount(ValueDisplayMode.DifferenceFrom) with { BaseField = "Year" })).BaseField);
    }

    [Theory]
    [InlineData("#,##0.00")]
    [InlineData("0.0 \"kg\"")]
    public void A_value_field_number_format_crosses_xlsx(string code)
    {
        Assert.Equal(code, Through(Pivoted(Amount(ValueDisplayMode.Normal) with { NumberFormat = code })).NumberFormat);
    }

    private static PivotDefinition ReadGrouped(Action<List<CacheField>, PivotTableDefinition> alter)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(Pivoted(Amount(ValueDisplayMode.Normal)), stream);
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var cache = Assert.Single(document.WorkbookPart!.PivotTableCacheDefinitionParts).PivotCacheDefinition!;
            var table = Assert.Single(document.WorkbookPart.WorksheetParts.SelectMany(part => part.PivotTableParts)).PivotTableDefinition!;
            alter([.. cache.CacheFields!.Elements<CacheField>()], table);
            cache.Save();
            table.Save();
        }
        stream.Position = 0;
        return Assert.Single(new XlsxReader().Read(stream).Value!.Pivots);
    }

    [Fact]
    public void A_numeric_range_group_is_read_from_xlsx()
    {
        var read = ReadGrouped((fields, _) => fields[1].AppendChild(new FieldGroup(
            new RangeProperties { GroupBy = GroupByValues.Range, StartNumber = 2000, EndNum = 2030, GroupInterval = 5 }) { Base = 1u }));

        Assert.Equal(5, read.In(PivotArea.Row).Single(field => field.SourceName == "Year").NumericGroupInterval);
    }

    [Fact]
    public void Excel_years_and_months_levels_of_one_date_field_read_as_one_month_grouping()
    {
        var read = ReadGrouped((fields, table) =>
        {
            fields[1].AppendChild(new FieldGroup(new RangeProperties { GroupBy = GroupByValues.Months }) { Base = 1u });
            var cacheFields = fields[0].Parent!;
            cacheFields.AppendChild(new CacheField(new FieldGroup(new RangeProperties { GroupBy = GroupByValues.Years }) { Base = 1u })
            { Name = "Years", NumberFormatId = 0u, DatabaseField = false });
            table.RowFields!.InsertAt(new Field { Index = 3 }, 1);
        });

        var year = Assert.Single(read.In(PivotArea.Row), field => field.SourceName == "Year");
        Assert.Equal(DateGrouping.Months, year.DateGrouping);
        Assert.Equal(2, read.In(PivotArea.Row).Count());
    }
}

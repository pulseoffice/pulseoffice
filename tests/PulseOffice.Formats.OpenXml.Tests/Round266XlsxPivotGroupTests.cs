using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Pivot;
using CellValue = PulseOffice.Sheets.Values.CellValue;
using PivotArea = PulseOffice.Sheets.Pivot.PivotArea;
using PivotField = PulseOffice.Sheets.Pivot.PivotField;
using Workbook = PulseOffice.Sheets.Workbook;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round266XlsxPivotGroupTests
{
    private static Workbook Pivoted(PivotField grouped, params double[] keys)
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Key"));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Text("Amount"));
        for (var row = 0; row < keys.Length; row++)
        {
            sheet.SetValue(new CellAddress(row + 1, 0), CellValue.Number(keys[row]));
            sheet.SetValue(new CellAddress(row + 1, 1), CellValue.Number(row + 1));
        }

        workbook.Pivots.Add(new PivotDefinition
        {
            Id = "p", Name = "P", SourceSheet = sheet.Name,
            SourceRange = new CellRange(CellAddress.First, new CellAddress(keys.Length, 1)),
            DestinationSheet = sheet.Name, Destination = new CellAddress(0, 4),
            OutputRange = new CellRange(new CellAddress(0, 4), new CellAddress(4, 5)),
            Fields = [grouped, new PivotField { SourceName = "Amount", SourceColumnIndex = 1, Area = PivotArea.Value }],
        });
        return workbook;
    }

    private static PivotField Key(DateGrouping grouping = DateGrouping.None, double? interval = null) =>
        new() { SourceName = "Key", SourceColumnIndex = 0, Area = PivotArea.Row, DateGrouping = grouping, NumericGroupInterval = interval };

    private static MemoryStream Written(Workbook workbook)
    {
        var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        return stream;
    }

    private static readonly double[] AcrossYears = [45306, 45337, 45672];

    [Fact]
    public void Months_across_years_are_written_with_a_derived_years_field()
    {
        using var stream = Written(Pivoted(Key(DateGrouping.Months), AcrossYears));
        using var document = SpreadsheetDocument.Open(stream, false);
        var cacheFields = document.WorkbookPart!.PivotTableCacheDefinitionParts.Single().PivotCacheDefinition!.CacheFields!.Elements<CacheField>().ToList();
        var table = document.WorkbookPart.WorksheetParts.SelectMany(part => part.PivotTableParts).Single().PivotTableDefinition!;

        Assert.Equal(3, cacheFields.Count);
        Assert.Equal(GroupByValues.Months, cacheFields[0].FieldGroup!.GetFirstChild<RangeProperties>()!.GroupBy!.Value);
        Assert.Equal(2u, cacheFields[0].FieldGroup!.ParentId!.Value);
        Assert.False(cacheFields[2].DatabaseField!.Value);
        Assert.Equal(0u, cacheFields[2].FieldGroup!.Base!.Value);
        Assert.Equal(["<1/15/2024", "2024", "2025", ">1/16/2025"],
            cacheFields[2].FieldGroup!.GetFirstChild<GroupItems>()!.Elements<StringItem>().Select(item => item.Val!.Value));
        Assert.Equal([2, 0], table.RowFields!.Elements<Field>().Select(field => field.Index!.Value));
        Assert.Equal(3u, table.PivotFields!.Count!.Value);
    }

    [Fact]
    public void Grouped_months_and_number_ranges_come_back_and_declare_no_loss()
    {
        var months = Pivoted(Key(DateGrouping.Months), AcrossYears);
        var ranges = Pivoted(Key(interval: 10), 3, 14, 27);

        Assert.DoesNotContain(new XlsxSpreadsheetWriter().CheckFidelity(months).Losses, loss => loss.Code == "XLSX030");
        Assert.DoesNotContain(new XlsxSpreadsheetWriter().CheckFidelity(ranges).Losses, loss => loss.Code == "XLSX030");
        using var monthStream = Written(months);
        using var rangeStream = Written(ranges);
        Assert.Equal(DateGrouping.Months, Assert.Single(Assert.Single(new XlsxReader().Read(monthStream).Value!.Pivots).In(PivotArea.Row)).DateGrouping);
        Assert.Equal(10, Assert.Single(Assert.Single(new XlsxReader().Read(rangeStream).Value!.Pivots).In(PivotArea.Row)).NumericGroupInterval);
    }

    [Fact]
    public void A_grouping_with_hidden_items_is_still_declared()
    {
        var workbook = Pivoted(Key(DateGrouping.Months) with { ExcludedValues = ["2024-01"] }, AcrossYears);

        Assert.Contains(new XlsxSpreadsheetWriter().CheckFidelity(workbook).Losses, loss => loss.Code == "XLSX030");
    }
}

using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round90FilterParityTests
{
    private static readonly CellRange Data = new(CellAddress.First, new CellAddress(5, 1));

    private static readonly CellRange CriteriaRange = new(new CellAddress(0, 3), new CellAddress(1, 3));

    private static Workbook Table()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Text("Region"));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Text("Amount"));
        string[] regions = ["North", "South", "North", "East", "South"];
        for (var index = 0; index < regions.Length; index++)
        {
            sheet.SetValue(new CellAddress(index + 1, 0), CellValue.Text(regions[index]));
            sheet.SetValue(new CellAddress(index + 1, 1), CellValue.Number((index + 1) * 10));
        }
        sheet.SetValue(new CellAddress(0, 3), CellValue.Text("Region"));
        sheet.SetValue(new CellAddress(1, 3), CellValue.Text("North"));
        return workbook;
    }

    private static readonly FilterNode Mixed = new FilterGroup(FilterGroupJoin.Or,
    [
        new FilterGroup(FilterGroupJoin.And, [new FilterTerm(0, "North"), new FilterTerm(1, ">15")]),
        new FilterTerm(0, "East"),
    ]);

    private static Workbook ThroughXlsx(Workbook source)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(source, stream);
        stream.Position = 0;
        var result = new XlsxReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    private static Workbook ThroughOds(Workbook source)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(source, stream);
        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    private static IEnumerable<string> XlsxLosses(Workbook workbook) =>
        new XlsxSpreadsheetWriter().CheckFidelity(workbook).Losses.Select(loss => loss.Code);

    private static IEnumerable<string> OdsLosses(Workbook workbook) =>
        new OdsSpreadsheetWriter().CheckFidelity(workbook).Losses.Select(loss => loss.Code);

    [Fact]
    public void A_condition_tree_on_the_sheet_range_crosses_ods_and_is_declared_in_xlsx()
    {
        var workbook = Table();
        workbook.Sheets[0].RangeFilter = new SheetRangeFilter(Data, true, new RangeFilter { Condition = Mixed });

        var read = ThroughOds(workbook).Sheets[0];

        Assert.False(read.AutoFilter.IsActive);
        Assert.Equal(new SheetRangeFilter(Data, true, new RangeFilter { Condition = Mixed }), read.RangeFilter);
        Assert.Contains("XLSX233", XlsxLosses(workbook));
        Assert.DoesNotContain("XLSX232", XlsxLosses(workbook));
        Assert.Null(ThroughXlsx(workbook).Sheets[0].RangeFilter);
    }

    [Fact]
    public void A_column_shaped_standard_filter_becomes_an_autofilter_in_xlsx()
    {
        var workbook = Table();
        var tree = new FilterGroup(FilterGroupJoin.And,
            [new FilterTerm(0, "North"), new FilterGroup(FilterGroupJoin.Or, [new FilterTerm(1, "<15"), new FilterTerm(1, ">25")])]);
        workbook.Sheets[0].RangeFilter = new SheetRangeFilter(Data, true, new RangeFilter { Condition = tree });

        Assert.Equal(tree, ThroughOds(workbook).Sheets[0].RangeFilter!.Filter.Condition);
        Assert.Contains("XLSX232", XlsxLosses(workbook));
        Assert.DoesNotContain("XLSX233", XlsxLosses(workbook));

        var read = ThroughXlsx(workbook).Sheets[0];
        Assert.Equal(Data, read.AutoFilter.Range);
        Assert.Equal("North", Assert.Single(read.AutoFilter.Filters, column => column.ColumnOffset == 0).Criteria);
        var amount = Assert.Single(read.AutoFilter.Filters, column => column.ColumnOffset == 1);
        Assert.Equal(("<15", ">25", CriteriaJoin.Or), (amount.Criteria, amount.SecondCriteria, amount.Join));
    }

    [Fact]
    public void An_advanced_filter_in_place_keeps_its_criteria_range_in_both_formats()
    {
        var workbook = Table();
        var filter = new RangeFilter { Criteria = new AdvancedCriteria(0, CriteriaRange) };
        workbook.Sheets[0].RangeFilter = new SheetRangeFilter(Data, true, filter);

        Assert.Equal(filter, ThroughOds(workbook).Sheets[0].RangeFilter!.Filter);
        Assert.Equal(filter, ThroughXlsx(workbook).Sheets[0].RangeFilter!.Filter);
        Assert.Equal(filter, ThroughXlsx(ThroughOds(workbook)).Sheets[0].RangeFilter!.Filter);
        Assert.DoesNotContain("XLSX233", XlsxLosses(workbook));
    }

    [Fact]
    public void Unique_records_cross_ods_and_are_declared_in_xlsx()
    {
        var workbook = Table();
        var filter = new RangeFilter { Criteria = new AdvancedCriteria(0, CriteriaRange), UniqueOnly = true };
        workbook.Sheets[0].RangeFilter = new SheetRangeFilter(Data, true, filter);

        Assert.Equal(filter, ThroughOds(workbook).Sheets[0].RangeFilter!.Filter);
        Assert.Contains("XLSX233", XlsxLosses(workbook));
    }

    [Fact]
    public void A_named_range_keeps_its_condition_tree_in_ods()
    {
        var workbook = Table();
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "Sales", SheetIndex = 0, Range = Data, RangeFilter = new RangeFilter { Condition = Mixed } });

        Assert.Equal(Mixed, ThroughOds(workbook).DatabaseRanges.Find("Sales")!.RangeFilter!.Condition);
        Assert.Contains("XLSX233", XlsxLosses(workbook));
    }

    [Fact]
    public void An_advanced_filter_on_a_named_range_crosses_xlsx_through_the_sheet_criteria_names()
    {
        var workbook = Table();
        var filter = new RangeFilter { Criteria = new AdvancedCriteria(0, CriteriaRange) };
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "Sales", SheetIndex = 0, Range = Data, RangeFilter = filter });

        Assert.DoesNotContain("XLSX233", XlsxLosses(workbook));
        var read = ThroughXlsx(workbook);
        Assert.Equal(filter, read.DatabaseRanges.Find("Sales")!.RangeFilter);
        Assert.Null(read.Sheets[0].RangeFilter);
        Assert.Equal(filter, ThroughXlsx(ThroughOds(workbook)).DatabaseRanges.Find("Sales")!.RangeFilter);
    }

    [Fact]
    public void A_second_named_range_advanced_filter_on_the_same_sheet_is_declared_in_xlsx()
    {
        var workbook = Table();
        var filter = new RangeFilter { Criteria = new AdvancedCriteria(0, CriteriaRange) };
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "Sales", SheetIndex = 0, Range = Data, RangeFilter = filter });
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "Other", SheetIndex = 0, Range = new CellRange(new CellAddress(7, 0), new CellAddress(9, 1)), RangeFilter = filter });

        Assert.Contains("XLSX233", XlsxLosses(workbook));
        var read = ThroughXlsx(workbook);
        Assert.Equal(filter, read.DatabaseRanges.Find("Sales")!.RangeFilter);
        Assert.Null(read.DatabaseRanges.Find("Other")?.RangeFilter);
    }

    [Fact]
    public void Unique_records_on_a_named_range_keep_the_criteria_and_declare_the_rest()
    {
        var workbook = Table();
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "Sales", SheetIndex = 0, Range = Data, RangeFilter = new RangeFilter { Criteria = new AdvancedCriteria(0, CriteriaRange), UniqueOnly = true } });

        Assert.Contains("XLSX233", XlsxLosses(workbook));
        Assert.Equal(new RangeFilter { Criteria = new AdvancedCriteria(0, CriteriaRange) }, ThroughXlsx(workbook).DatabaseRanges.Find("Sales")!.RangeFilter);
    }

    [Fact]
    public void A_named_range_advanced_filter_beside_a_sheet_filter_is_declared_in_xlsx()
    {
        var workbook = Table();
        workbook.Sheets[0].AutoFilter.Activate(new CellRange(new CellAddress(7, 0), new CellAddress(9, 1)));
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "Sales", SheetIndex = 0, Range = Data, RangeFilter = new RangeFilter { Criteria = new AdvancedCriteria(0, CriteriaRange) } });

        Assert.Contains("XLSX233", XlsxLosses(workbook));
    }

    [Fact]
    public void A_standard_filter_beside_an_autofilter_on_another_range_is_declared_in_ods()
    {
        var workbook = Table();
        workbook.Sheets[0].AutoFilter.Activate(new CellRange(CellAddress.First, new CellAddress(5, 0)));
        workbook.Sheets[0].RangeFilter = new SheetRangeFilter(Data, true, new RangeFilter { Condition = Mixed });

        Assert.Contains("ODF272", OdsLosses(workbook));
    }

    [Fact]
    public void A_standard_filter_on_the_autofilter_range_is_written_with_it()
    {
        var workbook = Table();
        workbook.Sheets[0].AutoFilter.Activate(Data);
        workbook.Sheets[0].RangeFilter = new SheetRangeFilter(Data, true, new RangeFilter { Condition = Mixed });

        var read = ThroughOds(workbook).Sheets[0];

        Assert.Equal(Data, read.AutoFilter.Range);
        Assert.Equal(Mixed, read.RangeFilter!.Filter.Condition);
        Assert.DoesNotContain("ODF272", OdsLosses(workbook));
    }

    [Fact]
    public void A_filter_renumbered_after_a_column_deletion_crosses_both_formats()
    {
        var workbook = Table();
        var shifted = new CellRange(new CellAddress(0, 0), new CellAddress(5, 1));
        var original = new RangeFilter
        {
            Condition = new FilterGroup(FilterGroupJoin.And, [new FilterTerm(0, "x"), new FilterTerm(1, "North"), new FilterTerm(2, ">15")]),
            Criteria = new AdvancedCriteria(0, new CellRange(new CellAddress(0, 4), new CellAddress(1, 4))),
        };
        var moved = original.Moved(field => field == 0 ? null : field - 1, criteria => criteria with
        {
            Range = new CellRange(new CellAddress(0, 3), new CellAddress(1, 3)),
        })!;
        workbook.Sheets[0].RangeFilter = new SheetRangeFilter(shifted, true, moved with { Criteria = null });

        Assert.Equal(new FilterGroup(FilterGroupJoin.And, [new FilterTerm(0, "North"), new FilterTerm(1, ">15")]), moved.Condition);
        Assert.Equal(moved.Condition, ThroughOds(workbook).Sheets[0].RangeFilter!.Filter.Condition);
        var read = ThroughXlsx(workbook).Sheets[0];
        Assert.Equal(shifted, read.AutoFilter.Range);
        Assert.Equal("North", Assert.Single(read.AutoFilter.Filters, column => column.ColumnOffset == 0).Criteria);
        Assert.Equal(">15", Assert.Single(read.AutoFilter.Filters, column => column.ColumnOffset == 1).Criteria);

        workbook.Sheets[0].RangeFilter = new SheetRangeFilter(shifted, true, new RangeFilter { Criteria = moved.Criteria });
        Assert.Equal(moved.Criteria, ThroughOds(workbook).Sheets[0].RangeFilter!.Filter.Criteria);
        Assert.Equal(moved.Criteria, ThroughXlsx(workbook).Sheets[0].RangeFilter!.Filter.Criteria);
    }
}

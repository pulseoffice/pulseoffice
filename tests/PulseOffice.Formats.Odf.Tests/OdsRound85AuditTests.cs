using System.IO.Compression;
using System.Text.RegularExpressions;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdsRound85AuditTests
{
    private static string Content(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        using var reader = new StreamReader(archive.GetEntry("content.xml")!.Open());
        return reader.ReadToEnd();
    }

    private static Workbook RoundTrip(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    [Fact]
    public void A_whole_column_validation_keeps_its_rows_beyond_the_last_used_row()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Number(1));
        var range = new CellRange(new CellAddress(0, 2), new CellAddress(CellAddress.MaxRows - 1, 2));
        sheet.Validations.Add(new DataValidation { Range = range, Kind = ValidationKind.List, ListValues = ["x", "y"] });

        var read = RoundTrip(workbook).Sheets[0];

        Assert.Equal(range, Assert.Single(read.Validations).Range);
    }

    [Fact]
    public void A_validation_cell_inside_a_gap_carries_the_validation_name()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(3, 5), CellValue.Number(1));
        var range = new CellRange(new CellAddress(0, 0), new CellAddress(9, 9));
        sheet.Validations.Add(new DataValidation { Range = range, Kind = ValidationKind.List, ListValues = ["a"] });

        var read = RoundTrip(workbook).Sheets[0];

        Assert.Equal(range, Assert.Single(read.Validations).Range);
        Assert.Equal(1, read.GetValue(new CellAddress(3, 5)).AsNumber());
    }

    [Fact]
    public void Rows_of_a_whole_column_merge_are_written_with_repeated_rows()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Text("top"));
        sheet.SetValue(new CellAddress(500, 4), CellValue.Text("beside"));
        var region = new CellRange(CellAddress.First, new CellAddress(CellAddress.MaxRows - 1, 1));
        sheet.Merges.Add(region);

        var content = Content(workbook);

        Assert.True(Regex.Count(content, "<table:table-row[ >]") < 20, "Rows are not coalesced.");
        var read = RoundTrip(workbook).Sheets[0];
        Assert.Equal(region, Assert.Single(read.Merges.All));
        Assert.Equal("top", read.GetValue(CellAddress.First).AsText());
        Assert.Equal("beside", read.GetValue(new CellAddress(500, 4)).AsText());
    }

    [Fact]
    public void A_value_filter_that_admits_nothing_stays_a_value_filter()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        for (var row = 0; row < 4; row++) sheet.SetValue(new CellAddress(row, 0), CellValue.Text("v" + row));
        sheet.AutoFilter.Activate(new CellRange(CellAddress.First, new CellAddress(3, 0)));
        sheet.AutoFilter.Set(new ColumnFilter(0) { AllowedValues = new HashSet<string>(StringComparer.Ordinal), IncludeBlanks = false });

        var filter = Assert.Single(RoundTrip(workbook).Sheets[0].AutoFilter.Filters);

        Assert.NotNull(filter.AllowedValues);
        Assert.Empty(filter.AllowedValues!);
        Assert.False(filter.IncludeBlanks);
        Assert.True(string.IsNullOrEmpty(filter.Criteria));
    }

    [Fact]
    public void A_value_filter_that_admits_only_blanks_survives()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Text("h"));
        sheet.AutoFilter.Activate(new CellRange(CellAddress.First, new CellAddress(3, 0)));
        sheet.AutoFilter.Set(new ColumnFilter(0) { AllowedValues = new HashSet<string>(StringComparer.Ordinal), IncludeBlanks = true });

        var filter = Assert.Single(RoundTrip(workbook).Sheets[0].AutoFilter.Filters);

        Assert.Empty(filter.AllowedValues!);
        Assert.True(filter.IncludeBlanks);
    }
}

using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round261XlsxPivotTests
{
    private static Workbook Pivoted(PivotField row, PivotField value, bool repeat = false)
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Region"));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Text("Amount"));
        string[] regions = ["North", "South", "West"];
        for (var at = 0; at < regions.Length; at++)
        {
            sheet.SetValue(new CellAddress(at + 1, 0), CellValue.Text(regions[at]));
            sheet.SetValue(new CellAddress(at + 1, 1), CellValue.Number(at + 1));
        }
        workbook.Pivots.Add(new PivotDefinition
        {
            Id = "p",
            Name = "P",
            SourceSheet = sheet.Name,
            SourceRange = new CellRange(CellAddress.First, new CellAddress(3, 1)),
            DestinationSheet = sheet.Name,
            Destination = new CellAddress(0, 4),
            OutputRange = new CellRange(new CellAddress(0, 4), new CellAddress(4, 5)),
            Layout = PivotLayout.Tabular,
            RepeatItemLabels = repeat,
            Fields = [row, value],
        });
        return workbook;
    }

    private static PivotField Region => new() { SourceName = "Region", SourceColumnIndex = 0, Area = PivotArea.Row };

    private static PivotField Amount => new() { SourceName = "Amount", SourceColumnIndex = 1, Area = PivotArea.Value, Aggregation = PivotAggregation.Average };

    private static PivotDefinition Through(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        return Assert.Single(new XlsxReader().Read(stream).Value!.Pivots);
    }

    [Fact]
    public void Sort_by_value_and_top_filter_cross_xlsx()
    {
        var read = Through(Pivoted(Region with { SortByValue = 0, SortDescending = true, TopFilter = new PivotTopFilter(2, 0, FromBottom: true) }, Amount));

        var row = Assert.Single(read.In(PivotArea.Row));
        Assert.Equal(0, row.SortByValue);
        Assert.Equal(new PivotTopFilter(2, 0, true), row.TopFilter);
    }

    [Fact]
    public void Repeated_labels_cross_xlsx_without_a_declared_loss()
    {
        var workbook = Pivoted(Region, Amount, repeat: true);

        Assert.DoesNotContain(new XlsxSpreadsheetWriter().CheckFidelity(workbook).Losses, loss => loss.Code == "XLSX063");
        Assert.True(Through(workbook).RepeatItemLabels);
    }

    [Fact]
    public void A_non_sum_value_field_gets_its_own_default_caption_and_reads_back_unnamed()
    {
        var value = Assert.Single(Through(Pivoted(Region, Amount)).In(PivotArea.Value));

        Assert.Null(value.DisplayName);
        Assert.Equal(PivotAggregation.Average, value.Aggregation);
    }
}

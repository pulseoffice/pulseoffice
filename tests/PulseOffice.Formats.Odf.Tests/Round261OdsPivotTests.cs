using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round261OdsPivotTests
{
    [Fact]
    public void Sort_by_value_and_top_filter_cross_ods()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Region"));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Text("Amount"));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Text("North"));
        sheet.SetValue(new CellAddress(1, 1), CellValue.Number(4));
        workbook.Pivots.Add(new PivotDefinition
        {
            Id = "p",
            Name = "P",
            SourceSheet = sheet.Name,
            SourceRange = new CellRange(CellAddress.First, new CellAddress(1, 1)),
            DestinationSheet = sheet.Name,
            Destination = new CellAddress(0, 4),
            OutputRange = new CellRange(new CellAddress(0, 4), new CellAddress(2, 5)),
            Fields =
            [
                new PivotField { SourceName = "Region", SourceColumnIndex = 0, Area = PivotArea.Row, SortByValue = 0, TopFilter = new PivotTopFilter(3, 0) },
                new PivotField { SourceName = "Amount", SourceColumnIndex = 1, Area = PivotArea.Value },
            ],
        });

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        var row = Assert.Single(Assert.Single(new OdsReader().Read(stream).Value!.Pivots).In(PivotArea.Row));

        Assert.Equal(0, row.SortByValue);
        Assert.Equal(new PivotTopFilter(3, 0), row.TopFilter);
    }
}

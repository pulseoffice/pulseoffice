using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round264OdsPivotTests
{
    private static PivotField Through(PivotField value)
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

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        return Assert.Single(Assert.Single(new OdsReader().Read(stream).Value!.Pivots).In(PivotArea.Value));
    }

    private static PivotField Amount(ValueDisplayMode mode) =>
        new() { SourceName = "Amount", SourceColumnIndex = 2, Area = PivotArea.Value, DisplayMode = mode };

    [Theory]
    [InlineData(ValueDisplayMode.PercentOfParent)]
    [InlineData(ValueDisplayMode.RankAscending)]
    public void Extended_display_modes_and_their_base_field_cross_ods(ValueDisplayMode mode)
    {
        var read = Through(Amount(mode) with { BaseField = "Year" });

        Assert.Equal(mode, read.DisplayMode);
        Assert.Equal("Year", read.BaseField);
    }

    [Fact]
    public void The_base_field_of_a_standard_display_mode_crosses_ods()
    {
        Assert.Equal("Year", Through(Amount(ValueDisplayMode.RunningTotal) with { BaseField = "Year" }).BaseField);
    }

    [Fact]
    public void A_value_field_number_format_crosses_ods()
    {
        Assert.Equal("0.0 \"kg\"", Through(Amount(ValueDisplayMode.Normal) with { NumberFormat = "0.0 \"kg\"" }).NumberFormat);
    }
}

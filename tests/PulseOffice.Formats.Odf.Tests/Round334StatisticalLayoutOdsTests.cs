using PulseOffice.Charts;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round334StatisticalLayoutOdsTests
{
    [Fact]
    public void Statistical_layout_and_chart_style_cross_ods()
    {
        var layout = new ChartStatisticalLayout
        {
            BinCount = 4, Underflow = 1.5, IntervalClosedRight = false, QuartileMethod = ChartQuartileMethod.Inclusive,
            MeanMarker = false, Outliers = false, Subtotals = [2], ParentLabels = ChartParentLabels.None,
        };
        var style = new ChartStyleReference { StyleId = 12, ColorStyle = "<cs:colorStyle xmlns:cs=\"http://schemas.microsoft.com/office/drawing/2012/chartStyle\" id=\"10\"/>" };
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Data");
        for (var row = 0; row < 3; row++)
        {
            sheet.SetValue(new CellAddress(row, 0), CellValue.Text($"P{row}"));
            sheet.SetValue(new CellAddress(row, 1), CellValue.Number(row + 1));
        }
        sheet.Objects.Add(new PlacedObject
        {
            Id = "g",
            Anchor = new CellAddress(0, 3),
            Content = new ChartDefinition
            {
                Id = "g", Type = ChartType.BoxPlot, SourceSheet = "Data", SourceRange = "A1:B3", FirstRowIsHeader = false,
                Statistical = layout, Style = style,
            },
        });
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);

        stream.Position = 0;
        var chart = Assert.IsType<ChartDefinition>(Assert.Single(new OdsReader().Read(stream).Value!.Sheets[0].Objects).Content);
        Assert.Equal(layout, chart.Statistical);
        Assert.Equal(style, chart.Style);
    }
}

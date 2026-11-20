using PulseOffice.Charts;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round327PieSplitOdsTests
{
    [Theory]
    [InlineData(PieSplitKind.Value, 2.5d)]
    [InlineData(PieSplitKind.Custom, 0d)]
    public void Pie_of_pie_split_and_second_plot_size_cross_ods(PieSplitKind kind, double threshold)
    {
        var split = new ChartPieSplit
        {
            Kind = kind,
            Threshold = threshold,
            CustomPoints = kind == PieSplitKind.Custom ? [0, 2] : [],
            SecondPlotSize = 50,
        };
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Data");
        for (var row = 0; row < 4; row++)
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
                Id = "g", Type = ChartType.PieOfPie, SourceSheet = "Data", SourceRange = "A1:B4", FirstRowIsHeader = false, PieSplit = split,
            },
        });
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);

        stream.Position = 0;
        var read = new OdsReader().Read(stream);
        var chart = Assert.IsType<ChartDefinition>(Assert.Single(read.Value!.Sheets[0].Objects).Content);
        Assert.Equal(ChartType.PieOfPie, chart.Type);
        Assert.Equal(split, chart.PieSplit);
    }
}

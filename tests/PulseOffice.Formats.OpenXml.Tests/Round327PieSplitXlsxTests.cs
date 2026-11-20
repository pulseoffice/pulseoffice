using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using PulseOffice.Charts;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round327PieSplitXlsxTests
{
    [Theory]
    [InlineData(PieSplitKind.Position, 2d)]
    [InlineData(PieSplitKind.Percent, 12.5d)]
    [InlineData(PieSplitKind.Custom, 0d)]
    public void Pie_of_pie_split_and_second_plot_size_cross_xlsx(PieSplitKind kind, double threshold)
    {
        var split = new ChartPieSplit
        {
            Kind = kind,
            Threshold = threshold,
            CustomPoints = kind == PieSplitKind.Custom ? [1, 3] : [],
            SecondPlotSize = 140,
        };
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Data");
        for (var row = 0; row < 4; row++)
        {
            sheet[new CellAddress(row, 0)] = new Cell { Value = CellValue.Text($"P{row}") };
            sheet[new CellAddress(row, 1)] = new Cell { Value = CellValue.Number(row + 1) };
        }
        sheet.Objects.Add(new PlacedObject
        {
            Id = "g",
            Anchor = new CellAddress(0, 3),
            Content = new ChartDefinition
            {
                Id = "g", Type = ChartType.BarOfPie, SourceSheet = "Data", SourceRange = "A1:B4", FirstRowIsHeader = false, PieSplit = split,
            },
        });
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);

        stream.Position = 0;
        using (var package = SpreadsheetDocument.Open(stream, false))
        {
            var errors = new OpenXmlValidator(DocumentFormat.OpenXml.FileFormatVersions.Microsoft365).Validate(package, TestContext.Current.CancellationToken)
                .Where(error => error.Part?.Uri.OriginalString.Contains("/charts/", StringComparison.Ordinal) == true)
                .Select(error => error.Description).ToList();
            Assert.True(errors.Count == 0, string.Join("\n", errors));
        }
        stream.Position = 0;
        var read = new XlsxReader().Read(stream);
        var chart = Assert.IsType<ChartDefinition>(Assert.Single(read.Value!.Sheets[0].Objects).Content);
        Assert.Equal(ChartType.BarOfPie, chart.Type);
        Assert.Equal(split, chart.PieSplit);
    }
}

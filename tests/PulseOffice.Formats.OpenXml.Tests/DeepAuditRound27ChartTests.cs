using WorksheetExtensionList = DocumentFormat.OpenXml.Spreadsheet.WorksheetExtensionList;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class DeepAuditRound27ChartTests
{
    [Theory]
    [InlineData(1e9, 100)]
    [InlineData(double.PositiveInfinity, 0.75)]
    [InlineData(0.0001, 0.01)]
    public void LoSpessoreDelleSparklineLettoDaXlsxHaUnTetto(double weight, double expected)
    {
        var source = Workbook.CreateDefault().Sheets[0];
        source.Sparklines.Add(new SparklineGroup
        {
            Id = "g",
            Sparklines = [new Sparkline(new CellAddress(0, 3), new CellRange(new CellAddress(0, 0), new CellAddress(0, 2)))],
            Style = new SparklineStyle { LineWidth = weight },
        });
        var mapper = new XlsxSparklineMapper();
        var extensions = (WorksheetExtensionList)mapper.Build(source)!.CloneNode(true);

        var target = Workbook.CreateDefault().Sheets[0];
        mapper.Read(extensions, target);

        Assert.Equal(expected, target.Sparklines.Groups.Single().Style.LineWidth);
    }
}

using PulseOffice.Charts;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Engine.Operations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class XlsxPivotChartCorpusTests
{
    private const string Sample = "/Volumes/Dati/develop/libreoffice-ref/sc/qa/unit/data/xlsx/forum-mso-de-104083.xlsx";

    [Fact]
    public void IlGraficoPivotNonDisegnaIlTotaleComplessivo()
    {
        if (!File.Exists(Sample)) Assert.Skip("Reference corpus not available.");

        using var stream = File.OpenRead(Sample);
        var result = new XlsxReader().Read(stream);
        Assert.True(result.Succeeded);

        var chart = result.Value!.Sheets[0].Objects.Select(placed => placed.Content).OfType<ChartDefinition>().Single();

        Assert.Equal("A3:D59", chart.SourceRange);
    }

    [Fact]
    public void LeSerieDelGraficoPivotPortanoINomiDiExcelAncheDopoODSeXLSX()
    {
        if (!File.Exists(Sample)) Assert.Skip("Reference corpus not available.");

        using var stream = File.OpenRead(Sample);
        var workbook = new XlsxReader().Read(stream).Value!;
        string[] expected = ["2015", "2016", "2017"];
        Assert.Equal(expected, Names(workbook));

        using var xlsx = new MemoryStream();
        new XlsxWriter().Write(workbook, xlsx);
        xlsx.Position = 0;
        var throughXlsx = new XlsxReader().Read(xlsx).Value!;
        Assert.Equal(expected, Names(throughXlsx));

        using var ods = new MemoryStream();
        new OdsWriter().Write(workbook, ods);
        ods.Position = 0;
        var throughOds = new OdsReader().Read(ods).Value!;
        Assert.Equal(expected, Names(throughOds));
    }

    private static string[] Names(Workbook workbook)
    {
        var chart = workbook.Sheets.SelectMany(sheet => sheet.Objects).Select(placed => placed.Content).OfType<ChartDefinition>().Single();
        return [.. new ChartDataExtractor().Extract(workbook, chart).Series.Select(series => series.Name)];
    }
}

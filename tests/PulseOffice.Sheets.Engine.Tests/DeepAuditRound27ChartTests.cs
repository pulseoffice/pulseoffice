using PulseOffice.Charts;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class DeepAuditRound27ChartTests
{
    private static Workbook Workbook()
    {
        var workbook = PulseOffice.Sheets.Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Mese"));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Text("Vendite"));
        for (var row = 1; row <= 3; row++)
        {
            sheet.SetValue(new CellAddress(row, 0), CellValue.Text("M" + row));
            sheet.SetValue(new CellAddress(row, 1), CellValue.Number(row * 10));
        }
        return workbook;
    }

    private static ChartDefinition Chart(CellRange range, bool seriesInColumns = true) => new()
    {
        Id = "c",
        Type = ChartType.Column,
        SourceSheet = "Sheet1",
        SourceRange = range.ToString(),
        SeriesInColumns = seriesInColumns,
    };

    [Fact]
    public void UnGraficoSuColonneIntereLeggeSoloLaZonaUsata()
    {
        var range = new CellRange(new CellAddress(0, 0), new CellAddress(CellAddress.MaxRows - 1, 1));

        var data = new ChartDataExtractor().Extract(Workbook(), Chart(range));

        Assert.Equal(["M1", "M2", "M3"], data.Categories);
        Assert.Equal([10d, 20d, 30d], data.Series.Single().Values);
    }

    [Fact]
    public void UnGraficoSuRigheIntereLeggeSoloLaZonaUsata()
    {
        var range = new CellRange(new CellAddress(0, 0), new CellAddress(3, CellAddress.MaxColumns - 1));

        var data = new ChartDataExtractor().Extract(Workbook(), Chart(range, seriesInColumns: false));

        Assert.Equal(3, data.Series.Count);
        Assert.Equal(["Vendite"], data.Categories);
    }

    [Fact]
    public void LaScalaComuneDiUnGruppoDiSparklineNonRileggeIlGruppoPerOgniSparkline()
    {
        var workbook = PulseOffice.Sheets.Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        const int rows = 3000;
        const int width = 20;
        for (var row = 0; row < rows; row++)
            for (var column = 0; column < width; column++)
                sheet.SetValue(new CellAddress(row, column), CellValue.Number(row + column));
        var group = new SparklineGroup
        {
            Id = "g",
            SameScale = true,
            Sparklines = [.. Enumerable.Range(0, rows).Select(row =>
                new Sparkline(new CellAddress(row, width), new CellRange(new CellAddress(row, 0), new CellAddress(row, width - 1))))],
        };
        var data = new SparklineData();

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var points = group.Sparklines.Select(sparkline => data.Read(sheet, group, sparkline)).ToList();
        watch.Stop();

        Assert.All(points, point => Assert.Equal(rows - 1 + width - 1, point.Maximum));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), $"{watch.Elapsed}");
    }

    [Fact]
    public void LaScalaComuneSegueIValoriCambiati()
    {
        var workbook = Workbook();
        var sheet = workbook.Sheets[0];
        var group = new SparklineGroup
        {
            Id = "g",
            SameScale = true,
            Sparklines = [new Sparkline(new CellAddress(0, 3), new CellRange(new CellAddress(1, 1), new CellAddress(3, 1)))],
        };
        var data = new SparklineData();
        Assert.Equal(30, data.Read(sheet, group, group.Sparklines[0]).Maximum);

        sheet.SetValue(new CellAddress(2, 1), CellValue.Number(500));

        Assert.Equal(500, data.Read(sheet, group, group.Sparklines[0]).Maximum);
    }

    [Fact]
    public void UnGraficoSuColonneIntereDiUnFoglioVuotoNonHaDati()
    {
        var range = new CellRange(new CellAddress(0, 0), new CellAddress(CellAddress.MaxRows - 1, 1));

        Assert.True(new ChartDataExtractor().Extract(PulseOffice.Sheets.Workbook.CreateDefault(), Chart(range)).IsEmpty);
    }
}

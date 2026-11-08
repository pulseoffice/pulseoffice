using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Charts;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;
using C = DocumentFormat.OpenXml.Drawing.Charts;

namespace PulseOffice.Formats.OpenXml.Tests;

public class ChartSeriesSourcesFormatTests
{
    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static Workbook TwoSheets()
    {
        var workbook = new Workbook();
        var first = workbook.AddSheet("Nord");
        var second = workbook.AddSheet("Sud Est");
        first[A("A1")] = new Cell { Value = CellValue.Text("Mese") };
        first[A("B1")] = new Cell { Value = CellValue.Text("Nord") };
        second[A("C1")] = new Cell { Value = CellValue.Text("Sud") };
        for (var i = 0; i < 3; i++)
        {
            first[A($"A{i + 2}")] = new Cell { Value = CellValue.Text($"M{i + 1}") };
            first[A($"B{i + 2}")] = new Cell { Value = CellValue.Number(10 + i) };
            second[A($"C{i + 2}")] = new Cell { Value = CellValue.Number(100 + i) };
        }
        return workbook;
    }

    private static ChartDefinition PerSeries() => new()
    {
        Id = "grafico1",
        Type = ChartType.Column,
        SourceSheet = "Nord",
        SourceRange = "A1:B4",
        SeriesSources =
        [
            new ChartSeriesSource("Nord!B1", "Nord!B2:B4", "Nord!A2:A4"),
            new ChartSeriesSource("'Sud Est'!C1", "'Sud Est'!C2:C4", "Nord!A2:A4"),
        ],
    };

    private static void Place(Workbook workbook, ChartDefinition chart) =>
        workbook.Sheets[0].Objects.Add(new PlacedObject { Id = chart.Id, Content = chart, Anchor = A("E2") });

    private static Workbook Xlsx(Workbook source)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(source, stream);
        stream.Position = 0;
        var result = new XlsxReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    private static Workbook Ods(Workbook source)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(source, stream);
        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    private static ChartDefinition ChartOf(Workbook workbook) =>
        Assert.IsType<ChartDefinition>(Assert.Single(workbook.Sheets[0].Objects).Content);

    private static void AssertTwoSheetData(Workbook workbook)
    {
        var data = new ChartDataExtractor().Extract(workbook, ChartOf(workbook));
        Assert.Equal(["M1", "M2", "M3"], data.Categories);
        Assert.Equal(2, data.Series.Count);
        Assert.Equal("Nord", data.Series[0].Name);
        Assert.Equal([10d, 11d, 12d], data.Series[0].Values);
        Assert.Equal("Sud", data.Series[1].Name);
        Assert.Equal([100d, 101d, 102d], data.Series[1].Values);
    }

    [Fact]
    public void UnXlsxEsternoConSerieSuDueFogliSiLeggeSerieperSerie()
    {
        var workbook = TwoSheets();
        Place(workbook, new ChartDefinition { Id = "grafico1", Type = ChartType.Column, SourceSheet = "Nord", SourceRange = "A1:C4" });

        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        using (var package = SpreadsheetDocument.Open(stream, true))
        {
            var chart = package.WorkbookPart!.WorksheetParts.SelectMany(part => part.DrawingsPart?.ChartParts ?? []).Single();
            var second = chart.ChartSpace!.Descendants<C.BarChartSeries>().ElementAt(1);
            second.GetFirstChild<C.SeriesText>()!.Descendants<C.Formula>().Single().Text = "'Sud Est'!$C$1";
            second.GetFirstChild<C.Values>()!.Descendants<C.Formula>().Single().Text = "'Sud Est'!$C$2:$C$4";
            chart.ChartSpace.Save();
        }

        stream.Position = 0;
        var read = new XlsxReader().Read(stream).Value!;

        Assert.True(ChartOf(read).HasSeriesSources);
        AssertTwoSheetData(read);
    }

    [Fact]
    public void LeSerieSuDueFogliFannoIlGiroInXlsx()
    {
        var workbook = TwoSheets();
        Place(workbook, PerSeries());

        var read = Xlsx(workbook);

        Assert.Equal(2, ChartOf(read).SeriesSources!.Count);
        AssertTwoSheetData(read);
    }

    [Fact]
    public void LeSerieSuDueFogliFannoIlGiroInOds()
    {
        var workbook = TwoSheets();
        Place(workbook, PerSeries());

        var read = Ods(workbook);

        Assert.Equal(2, ChartOf(read).SeriesSources!.Count);
        AssertTwoSheetData(read);
    }

    [Fact]
    public void LeSerieSuDueFogliAttraversanoXlsxEOdsInSequenza()
    {
        var workbook = TwoSheets();
        Place(workbook, PerSeries());

        var read = Xlsx(Ods(Xlsx(workbook)));

        AssertTwoSheetData(read);
        Assert.True(new ChartSourceReferences().Same("'Sud Est'!C2:C4", ChartOf(read).SeriesSources![1].Values));
    }

    [Fact]
    public void IlGraficoAIntervalloUnicoRestaCompattoInEntrambiIFormati()
    {
        var workbook = TwoSheets();
        Place(workbook, new ChartDefinition { Id = "grafico1", Type = ChartType.Column, SourceSheet = "Nord", SourceRange = "A1:B4" });

        Assert.False(ChartOf(Xlsx(workbook)).HasSeriesSources);
        Assert.False(ChartOf(Ods(workbook)).HasSeriesSources);
        Assert.Equal("A1:B4", ChartOf(Ods(Xlsx(workbook))).SourceRange);
    }
}

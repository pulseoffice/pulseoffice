using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Charts;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;
using C = DocumentFormat.OpenXml.Drawing.Charts;

namespace PulseOffice.Formats.OpenXml.Tests;

public class Round318ChartPointFormatTests
{
    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static Workbook Book(ChartDefinition chart)
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        sheet[A("A1")] = new Cell { Value = CellValue.Text("Mese") };
        sheet[A("B1")] = new Cell { Value = CellValue.Text("Uno") };
        sheet[A("C1")] = new Cell { Value = CellValue.Text("Due") };
        sheet[A("D1")] = new Cell { Value = CellValue.Text("Tre") };
        for (var row = 0; row < 3; row++)
        {
            sheet[A($"A{row + 2}")] = new Cell { Value = CellValue.Text($"M{row + 1}") };
            sheet[A($"B{row + 2}")] = new Cell { Value = CellValue.Number(10 + row) };
            sheet[A($"C{row + 2}")] = new Cell { Value = CellValue.Number(20 + row) };
            sheet[A($"D{row + 2}")] = new Cell { Value = CellValue.Number(30 + row) };
        }
        sheet.Objects.Add(new PlacedObject { Id = chart.Id, Content = chart, Anchor = A("F2") });
        return workbook;
    }

    private static ChartDefinition Chart(ChartType type, string range = "A1:C4") =>
        new() { Id = "grafico1", Type = type, SourceSheet = "Dati", SourceRange = range };

    private static ChartDefinition RoundTrip(ChartDefinition chart, string format, Action<SpreadsheetDocument>? alter = null)
    {
        using var stream = new MemoryStream();
        if (format == "xlsx") new XlsxWriter().Write(Book(chart), stream);
        else new OdsWriter().Write(Book(chart), stream);
        if (alter is not null)
        {
            using var package = SpreadsheetDocument.Open(stream, true);
            alter(package);
        }
        stream.Position = 0;
        var result = format == "xlsx" ? new XlsxReader().Read(stream) : new OdsReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return Assert.IsType<ChartDefinition>(Assert.Single(result.Value!.Sheets[0].Objects).Content);
    }

    [Theory]
    [InlineData("xlsx", ChartType.Pie)]
    [InlineData("ods", ChartType.Pie)]
    [InlineData("xlsx", ChartType.Doughnut)]
    [InlineData("ods", ChartType.Doughnut)]
    public void A_single_slice_keeps_its_colour_and_explosion(string format, ChartType type)
    {
        var chart = Chart(type, "A1:B4").WithPointFormat(0, new ChartPointFormat(1) { ColorHex = "#123456", ExplosionPercent = 25 });

        var read = RoundTrip(chart, format);

        Assert.Equal(new ChartPointFormat(1) { ColorHex = "#123456", ExplosionPercent = 25 }, read.PointFormat(0, 1));
        Assert.True(read.PointFormat(0, 0).IsPlain);
        Assert.True(read.PointFormat(0, 2).IsPlain);
    }

    [Theory]
    [InlineData("xlsx")]
    [InlineData("ods")]
    public void A_plain_pie_reads_back_without_point_formats(string format)
    {
        var read = RoundTrip(Chart(ChartType.Pie, "A1:B4"), format);

        Assert.Null(read.SeriesFormats);
    }

    [Theory]
    [InlineData("xlsx", ChartType.Column)]
    [InlineData("ods", ChartType.Column)]
    [InlineData("xlsx", ChartType.Line)]
    [InlineData("ods", ChartType.Line)]
    [InlineData("xlsx", ChartType.Area)]
    [InlineData("ods", ChartType.Area)]
    public void One_point_of_a_series_keeps_its_colour_beside_the_series_colours(string format, ChartType type)
    {
        var chart = (Chart(type) with { SeriesColors = ["#FF0000", "#00FF00"] }).WithPointFormat(1, new ChartPointFormat(2) { ColorHex = "#ABCDEF" });

        var read = RoundTrip(chart, format);

        Assert.Equal("#ABCDEF", read.PointFormat(1, 2).ColorHex);
        Assert.True(read.PointFormat(1, 0).IsPlain);
        Assert.True(read.PointFormat(0, 2).IsPlain);
        Assert.Equal("#FF0000", read.ExplicitSeriesColor(0));
        Assert.Equal("#00FF00", read.ExplicitSeriesColor(1));
    }

    [Fact]
    public void Point_formats_replace_by_index_and_drop_when_plain()
    {
        var formats = ChartPointFormats.None
            .With(new ChartPointFormat(3) { ColorHex = "#000000" })
            .With(new ChartPointFormat(1) { ExplosionPercent = 9999 })
            .With(new ChartPointFormat(3));

        var only = Assert.Single(formats.Items);
        Assert.Equal(1, only.Index);
        Assert.Equal(ChartPointFormats.MaximumExplosion, only.ExplosionPercent);
        Assert.Equal(ChartPointFormats.None.With(new ChartPointFormat(1) { ExplosionPercent = 400 }), formats);
    }

    [Theory]
    [InlineData("xlsx", ChartType.Radar)]
    [InlineData("ods", ChartType.Radar)]
    [InlineData("xlsx", ChartType.Scatter)]
    [InlineData("ods", ChartType.Scatter)]
    public void Radar_and_marker_scatter_series_keep_their_colours(string format, ChartType type)
    {
        var read = RoundTrip(Chart(type) with { SeriesColors = ["#FF0000", "#0000FF"] }, format);

        Assert.Equal(type, read.Type);
        Assert.Equal("#FF0000", read.ExplicitSeriesColor(0));
    }

    [Fact]
    public void A_combo_from_another_application_keeps_its_line_series_whatever_its_index()
    {
        var chart = new ChartDefinition
        {
            Id = "grafico1", Type = ChartType.ColumnLine, SourceSheet = "Dati", SourceRange = "A1:D4", LineSeriesCount = 1,
        }.WithSeriesFormat(1, new ChartSeriesFormat { SecondaryAxis = true });

        var read = RoundTrip(chart, "xlsx", package =>
        {
            var part = package.WorkbookPart!.WorksheetParts.SelectMany(sheet => sheet.DrawingsPart?.ChartParts ?? []).Single();
            var line = part.ChartSpace!.Descendants<C.LineChartSeries>().Single();
            var bars = part.ChartSpace.Descendants<C.BarChartSeries>().ToList();
            line.GetFirstChild<C.Index>()!.Val = 0u;
            bars[0].GetFirstChild<C.Index>()!.Val = 1u;
            bars[1].GetFirstChild<C.Index>()!.Val = 2u;
            part.ChartSpace.Save();
        });

        Assert.Equal(ChartType.ColumnLine, read.Type);
        Assert.Equal(1, read.LineSeriesCount);
        Assert.True(read.SeriesFormat(1).SecondaryAxis);
        Assert.False(read.SeriesFormat(2).SecondaryAxis);
    }
}

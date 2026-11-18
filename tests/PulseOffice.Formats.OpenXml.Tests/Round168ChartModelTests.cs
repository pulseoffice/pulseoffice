using PulseOffice.Charts;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class Round168ChartModelTests
{
    private static readonly ChartAxisScale ValueScale = new()
    {
        Minimum = 1,
        Maximum = 1000,
        MajorUnit = 10,
        MinorUnit = 2,
        Logarithmic = true,
        Reversed = true,
        Crossing = AxisCrossing.Value,
        CrossingValue = 5,
        NumberFormat = "0.0",
    };

    private static readonly ChartSeriesFormat Analysed = new()
    {
        LineWidthPoints = 3,
        Dash = LineDash.Dash,
        Marker = MarkerSymbol.Diamond,
        MarkerSize = 9,
        Trendline = new ChartTrendline(TrendlineKind.Polynomial) { Order = 3, Forward = 1, DisplayEquation = true },
        ErrorBars = new ChartErrorBars(ErrorBarKind.Percentage, 10) { Direction = ErrorBarDirection.Plus },
    };

    private static readonly ChartDataLabelOptions Labels = new() { ShowValue = true, ShowCategory = true, Placement = DataLabelPlacement.Above };

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
        for (var row = 0; row < 3; row++)
        {
            sheet[A($"A{row + 2}")] = new Cell { Value = CellValue.Text($"M{row + 1}") };
            sheet[A($"B{row + 2}")] = new Cell { Value = CellValue.Number(10 + row) };
            sheet[A($"C{row + 2}")] = new Cell { Value = CellValue.Number(200 + row) };
        }
        sheet.Objects.Add(new PlacedObject { Id = chart.Id, Content = chart, Anchor = A("E2") });
        return workbook;
    }

    private static ChartDefinition Chart(ChartType type) => new ChartDefinition
    {
        Id = "grafico1",
        Type = type,
        SourceSheet = "Dati",
        SourceRange = "A1:C4",
        ShowDataLabels = true,
        DataLabels = Labels,
        ValueAxis = ValueScale,
        CategoryAxis = new ChartAxisScale { Reversed = true },
        SecondaryValueAxis = new ChartAxisScale { Maximum = 500 },
    }.WithSeriesFormat(0, Analysed).WithSeriesFormat(1, new ChartSeriesFormat { SecondaryAxis = true });

    private static ChartDefinition RoundTrip(ChartDefinition chart, string format)
    {
        using var stream = new MemoryStream();
        if (format == "xlsx") new XlsxWriter().Write(Book(chart), stream);
        else new OdsWriter().Write(Book(chart), stream);
        stream.Position = 0;
        var result = format == "xlsx" ? new XlsxReader().Read(stream) : new OdsReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return Assert.IsType<ChartDefinition>(Assert.Single(result.Value!.Sheets[0].Objects).Content);
    }

    [Theory]
    [InlineData("xlsx")]
    [InlineData("ods")]
    public void Axis_scales_cross_the_format(string format)
    {
        var read = RoundTrip(Chart(ChartType.Line), format);

        Assert.Equal(ValueScale, read.ValueAxis);
        Assert.Equal(new ChartAxisScale { Reversed = true }, read.CategoryAxis);
        Assert.Equal(new ChartAxisScale { Maximum = 500 }, read.SecondaryValueAxis);
    }

    [Theory]
    [InlineData("xlsx")]
    [InlineData("ods")]
    public void Series_formats_trendlines_and_error_bars_cross_the_format(string format)
    {
        var read = RoundTrip(Chart(ChartType.Line), format);

        Assert.Equal(Analysed, read.SeriesFormat(0));
        Assert.True(read.SeriesFormat(1).SecondaryAxis);
        Assert.Equal(new ChartSeriesFormat { SecondaryAxis = true }, read.SeriesFormat(1));
    }

    [Theory]
    [InlineData("xlsx")]
    [InlineData("ods")]
    public void Data_label_options_cross_the_format(string format)
    {
        var read = RoundTrip(Chart(ChartType.Line), format);

        Assert.True(read.ShowDataLabels);
        Assert.Equal(Labels, read.DataLabels);
    }

    [Theory]
    [InlineData("xlsx")]
    [InlineData("ods")]
    public void A_column_chart_keeps_its_secondary_series_and_type(string format)
    {
        var read = RoundTrip(Chart(ChartType.Column) with { DataLabels = ChartDataLabelOptions.ValuesOnly }, format);

        Assert.Equal(ChartType.Column, read.Type);
        Assert.False(read.SeriesFormat(0).SecondaryAxis);
        Assert.True(read.SeriesFormat(1).SecondaryAxis);
        Assert.Equal(TrendlineKind.Polynomial, read.SeriesFormat(0).Trendline?.Kind);
    }

    [Fact]
    public void A_plain_chart_reads_back_with_automatic_scales_and_no_formats()
    {
        var plain = new ChartDefinition { Id = "grafico1", Type = ChartType.Column, SourceSheet = "Dati", SourceRange = "A1:C4" };

        foreach (var format in new[] { "xlsx", "ods" })
        {
            var read = RoundTrip(plain, format);
            Assert.True(read.ValueAxis.IsAutomatic);
            Assert.True(read.CategoryAxis.IsAutomatic);
            Assert.Null(read.SeriesFormats);
            Assert.True(read.DataLabels.IsValuesOnly);
        }
    }
}

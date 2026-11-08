using PulseOffice.Charts;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class XlsxChartAndConditionalAuditTests
{
    private static Workbook RoundTrip(Workbook source)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(source, stream);
        stream.Position = 0;
        var result = new XlsxReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    private static Workbook Data(ChartDefinition chart)
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        sheet.SetValue(new CellAddress(0, 1), CellValue.Text("Q1"));
        sheet.SetValue(new CellAddress(0, 2), CellValue.Text("Q2"));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Text("Nord"));
        sheet.SetValue(new CellAddress(1, 1), CellValue.Number(4));
        sheet.SetValue(new CellAddress(1, 2), CellValue.Number(6));
        sheet.Objects.Add(new PlacedObject { Id = "g", Anchor = new CellAddress(4, 0), Content = chart });
        return workbook;
    }

    [Fact]
    public void TitoliDegliAssiEtichetteEGrigliaFannoIlGiro()
    {
        var chart = (ChartDefinition)RoundTrip(Data(new ChartDefinition
        {
            Id = "g", Type = ChartType.Column, SourceSheet = "Dati", SourceRange = "A1:C2",
            ChartTitle = "Totale", CategoryAxisTitle = "Zona", ValueAxisTitle = "Euro", ShowDataLabels = true, ShowGridLines = false,
        })).Sheets[0].Objects.Single().Content;

        Assert.Equal("Totale", chart.ChartTitle);
        Assert.Equal("Zona", chart.CategoryAxisTitle);
        Assert.Equal("Euro", chart.ValueAxisTitle);
        Assert.True(chart.ShowDataLabels);
        Assert.False(chart.ShowGridLines);
    }

    private static Workbook ThroughOds(Workbook source)
    {
        using var stream = new MemoryStream();
        new PulseOffice.Formats.Odf.OdsWriter().Write(source, stream);
        stream.Position = 0;
        return new PulseOffice.Formats.Odf.OdsReader().Read(stream).Value!;
    }

    [Theory]
    [InlineData("xlsx", ChartType.Column)]
    [InlineData("ods", ChartType.Column)]
    [InlineData("xlsx", ChartType.Line)]
    [InlineData("ods", ChartType.Line)]
    public void IColoriDelleSerieFannoIlGiroNeiDueFormati(string route, ChartType type)
    {
        var source = Data(new ChartDefinition
        {
            Id = "g", Type = type, SourceSheet = "Dati", SourceRange = "A1:C2", SeriesInColumns = false, SeriesColors = ["#FF0000"],
        });

        var chart = (ChartDefinition)(route == "xlsx" ? RoundTrip(source) : ThroughOds(source)).Sheets[0].Objects.Single().Content;

        Assert.Equal("#FF0000", chart.ExplicitSeriesColor(0));
        Assert.Equal("#FF0000", chart.Coloured(new ChartData(["Q1"], [new ChartSeries("Nord", [4]) { ColorHex = "#000000" }])).Series[0].ColorHex);
    }

    [Fact]
    public void UnTitoloDAsseNonDiventaIlTitoloDelGrafico()
    {
        var chart = (ChartDefinition)RoundTrip(Data(new ChartDefinition
        {
            Id = "g", Type = ChartType.Column, SourceSheet = "Dati", SourceRange = "A1:C2", ValueAxisTitle = "Euro",
        })).Sheets[0].Objects.Single().Content;

        Assert.Null(chart.ChartTitle);
        Assert.Equal("Euro", chart.ValueAxisTitle);
    }

    [Fact]
    public void LeSerieInRigaRestanoInRiga()
    {
        var chart = (ChartDefinition)RoundTrip(Data(new ChartDefinition
        {
            Id = "g", Type = ChartType.Line, SourceSheet = "Dati", SourceRange = "A1:C2", SeriesInColumns = false,
        })).Sheets[0].Objects.Single().Content;

        Assert.False(chart.SeriesInColumns);
        Assert.True(chart.FirstRowIsHeader);
        Assert.True(chart.FirstColumnIsCategory);
    }

    [Fact]
    public void SoglieEInsiemiDiIconeFannoIlGiro()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        var range = new CellRange(new CellAddress(0, 0), new CellAddress(9, 0));
        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = range, Visual = ConditionalVisual.DataBar,
            Thresholds = [new(ConditionalThresholdKind.Percentile, 10), new(ConditionalThresholdKind.Number, 90)],
        });
        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = range, Priority = 1, Visual = ConditionalVisual.IconSet, IconSetName = "4Rating", ShowValue = false,
            Thresholds = [new(ConditionalThresholdKind.Percent, 0), new(ConditionalThresholdKind.Number, 3), new(ConditionalThresholdKind.Percentile, 50), new(ConditionalThresholdKind.Number, 8)],
        });

        var reread = RoundTrip(workbook).Sheets[0].ConditionalRules;

        Assert.Equal(sheet.ConditionalRules[0].Thresholds, reread[0].Thresholds);
        Assert.Equal(ConditionalVisual.IconSet, reread[1].Visual);
        Assert.Equal("4Rating", reread[1].IconSetName);
        Assert.False(reread[1].ShowValue);
        Assert.Equal(sheet.ConditionalRules[1].Thresholds, reread[1].Thresholds);
    }

    [Fact]
    public void LePerditeDeiFormatiCondizionaliSonoDichiarate()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        var range = new CellRange(new CellAddress(0, 0), new CellAddress(9, 0));
        sheet.ConditionalRules.Add(new ConditionalRule { Range = range, Visual = ConditionalVisual.DataBar, NegativeBarColorHex = "#FF0000" });
        sheet.ConditionalRules.Add(new ConditionalRule { Range = range, Visual = ConditionalVisual.IconSet, IconSetName = "3ColorSmilies" });
        sheet.ConditionalRules.Add(new ConditionalRule { Range = range, Visual = ConditionalVisual.ColorScale, ScaleColors = ["#000000", "#111111", "#222222", "#333333"] });

        var codes = new XlsxSpreadsheetWriter().CheckFidelity(workbook).Losses.Select(loss => loss.Code).ToList();

        Assert.DoesNotContain("XLSX046", codes);
        Assert.Contains("XLSX047", codes);
        Assert.Contains("XLSX048", codes);
        Assert.Equal("3Arrows", RoundTrip(workbook).Sheets[0].ConditionalRules[1].IconSetName);
    }

    [Fact]
    public void InversioneESoglieStretteDegliInsiemiDiIconeFannoIlGiro()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = new CellRange(new CellAddress(0, 0), new CellAddress(9, 0)), Visual = ConditionalVisual.IconSet, IconSetName = "3Flags", ReverseIcons = true,
            Thresholds = [new(ConditionalThresholdKind.Percent, 0), new(ConditionalThresholdKind.Number, 3, null, false), new(ConditionalThresholdKind.Number, 8)],
        });

        var rule = RoundTrip(workbook).Sheets[0].ConditionalRules.Single();

        Assert.True(rule.ReverseIcons);
        Assert.Equal(sheet.ConditionalRules[0].Thresholds, rule.Thresholds);
    }

    [Fact]
    public void StelleTriangoliERiquadriViaggianoNellEstensioneX14()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        foreach (var (name, column) in new[] { ("3Stars", 0), ("3Triangles", 1), ("5Boxes", 2) })
            sheet.ConditionalRules.Add(new ConditionalRule
            {
                Range = new CellRange(new CellAddress(0, column), new CellAddress(9, column)), Priority = column + 1,
                Visual = ConditionalVisual.IconSet, IconSetName = name, ReverseIcons = column == 1, ShowValue = column != 2,
                Thresholds = [.. Enumerable.Range(0, name[0] - '0').Select(index => new ConditionalThreshold(ConditionalThresholdKind.Number, index * 2, null, index != 1))],
            });

        var reread = RoundTrip(workbook).Sheets[0].ConditionalRules.OrderBy(rule => rule.Priority).ToList();

        Assert.Equal(["3Stars", "3Triangles", "5Boxes"], reread.Select(rule => rule.IconSetName));
        Assert.True(reread[1].ReverseIcons);
        Assert.False(reread[2].ShowValue);
        Assert.Equal(sheet.ConditionalRules[2].Thresholds, reread[2].Thresholds);
        Assert.DoesNotContain("XLSX047", new XlsxSpreadsheetWriter().CheckFidelity(workbook).Losses.Select(loss => loss.Code));
    }

    [Fact]
    public void LeIconePersonalizzateFannoIlGiroNellEstensioneX14()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = new CellRange(new CellAddress(0, 0), new CellAddress(9, 0)), Visual = ConditionalVisual.IconSet, IconSetName = "3Arrows",
            CustomIcons = [new("3Flags", 0), new(ConditionalIconChoice.NoIcons, -1), new("3Stars", 2)],
        });

        var rule = RoundTrip(workbook).Sheets[0].ConditionalRules.Single();

        Assert.Equal(sheet.ConditionalRules[0].CustomIcons, rule.CustomIcons);
    }
}

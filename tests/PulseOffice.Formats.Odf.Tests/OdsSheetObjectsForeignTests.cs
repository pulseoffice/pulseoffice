using PulseOffice.Charts;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class OdsSheetObjectsForeignTests
{
    private static Workbook Fixture(string name)
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Calc", name));
        var copy = new MemoryStream();
        stream.CopyTo(copy);
        copy.Position = 0;
        var result = new OdsSpreadsheetReader().Read(copy);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    private static Workbook RoundTrip(Workbook workbook)
    {
        var stream = new MemoryStream();
        Assert.True(new OdsSpreadsheetWriter().Write(workbook, stream).Succeeded);
        stream.Position = 0;
        var result = new OdsSpreadsheetReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    private static ChartDefinition Chart(Workbook workbook, int index = 0) =>
        workbook.Sheets.SelectMany(sheet => sheet.Objects).Select(placed => placed.Content).OfType<ChartDefinition>().ElementAt(index);

    [Fact]
    public void LeEtichetteInPrimaRigaDiCalcNonDiventanoCategorie()
    {
        var workbook = Fixture("tdf31231.ods");
        var chart = Chart(workbook);

        Assert.True(chart.FirstRowIsHeader);
        Assert.False(chart.FirstColumnIsCategory);
        Assert.True(chart.SeriesInColumns);
        Assert.Equal(3, new ChartDataExtractor().Extract(workbook, chart).Series.Count);

        var reread = Chart(RoundTrip(workbook));
        Assert.True(reread.FirstRowIsHeader);
        Assert.False(reread.FirstColumnIsCategory);
    }

    [Fact]
    public void LeSerieInRigaDiCalcRestanoInRiga()
    {
        var chart = Chart(Fixture("copy-merged-number-formats.ods"));

        Assert.False(chart.SeriesInColumns);
        Assert.True(chart.FirstRowIsHeader);
        Assert.True(chart.FirstColumnIsCategory);
        Assert.Equal("A lame chart", chart.ChartTitle);
    }

    [Fact]
    public void ITitoliDegliAssiDiCalcSiLeggonoESiSalvano()
    {
        var workbook = Fixture("tdf121612.ods");
        var chart = Chart(workbook);

        Assert.False(string.IsNullOrEmpty(chart.CategoryAxisTitle));
        Assert.False(string.IsNullOrEmpty(chart.ValueAxisTitle));
        Assert.True(chart.ShowGridLines);

        var reread = Chart(RoundTrip(workbook));
        Assert.Equal(chart.CategoryAxisTitle, reread.CategoryAxisTitle);
        Assert.Equal(chart.ValueAxisTitle, reread.ValueAxisTitle);
    }

    [Fact]
    public void EtichetteDeiDatiEGrigliaSpentaFannoIlGiro()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        sheet[new CellAddress(0, 1)] = new Cell { Value = CellValue.Text("Vendite") };
        sheet[new CellAddress(1, 0)] = new Cell { Value = CellValue.Text("Nord") };
        sheet[new CellAddress(1, 1)] = new Cell { Value = CellValue.Number(5) };
        sheet.Objects.Add(new PlacedObject
        {
            Id = "grafico",
            Anchor = new CellAddress(3, 3),
            ZOrder = 4,
            OffsetX = Foundation.Units.Twip.FromCentimeters(0.5),
            Content = new ChartDefinition
            {
                Id = "grafico", Type = ChartType.Column, SourceSheet = sheet.Name, SourceRange = "A1:B2",
                CategoryAxisTitle = "Zona", ValueAxisTitle = "Euro", ShowDataLabels = true, ShowGridLines = false,
            },
        });

        var placed = RoundTrip(workbook).Sheets[0].Objects.Single();
        var chart = (ChartDefinition)placed.Content;

        Assert.Equal("Zona", chart.CategoryAxisTitle);
        Assert.Equal("Euro", chart.ValueAxisTitle);
        Assert.True(chart.ShowDataLabels);
        Assert.False(chart.ShowGridLines);
        Assert.Equal(4, placed.ZOrder);
        Assert.InRange(placed.OffsetX.Value, Foundation.Units.Twip.FromCentimeters(0.5).Value - 2, Foundation.Units.Twip.FromCentimeters(0.5).Value + 2);
    }

    [Fact]
    public void IGraficiAncoratiAllaPaginaConservanoPosizioneEOrdine()
    {
        var charts = Fixture("many_charts.ods").Sheets[1].Objects;

        Assert.Equal(20, charts.Count);
        Assert.Equal(20, charts.Select(placed => (placed.Anchor, placed.OffsetX.Value, placed.OffsetY.Value)).Distinct().Count());
        Assert.Equal(20, charts.Select(placed => placed.ZOrder).Distinct().Count());
    }

    [Fact]
    public void IColoriDelleSerieDiCalcSiConservano()
    {
        var charts = Fixture("many_charts.ods").Sheets[1].Objects.Select(placed => placed.Content).OfType<PulseOffice.Charts.ChartDefinition>().ToList();

        var colours = charts.Select(chart => chart.ExplicitSeriesColor(0)).Where(colour => colour is not null).Distinct().ToList();
        Assert.Contains("#FF0000", colours);
        Assert.Contains("#004586", colours);
        Assert.True(colours.Count >= 4);
        Assert.Equal(colours.ToHashSet(), RoundTrip(Fixture("many_charts.ods")).Sheets[1].Objects.Select(placed => placed.Content)
            .OfType<PulseOffice.Charts.ChartDefinition>().Select(chart => chart.ExplicitSeriesColor(0)).Where(colour => colour is not null).ToHashSet());
    }

    [Fact]
    public void LeEllissiDiCalcNonSpariscono()
    {
        var shapes = Fixture("cell-anchored-shapes.ods").Sheets[0].Objects;

        Assert.Equal(2, shapes.Count);
        Assert.All(shapes, placed => Assert.Equal(ShapeKind.Ellipse, ((ShapeObject)placed.Content).Shape));

        var reread = RoundTrip(Fixture("cell-anchored-shapes.ods")).Sheets[0].Objects;
        Assert.Equal(shapes.Select(placed => placed.Anchor), reread.Select(placed => placed.Anchor));
    }

    [Fact]
    public void GliInsiemiDiIconeDiCalcSiLeggonoESiSalvano()
    {
        var rules = Fixture("tdf101104.ods").Sheets[0].ConditionalRules;

        Assert.Contains(rules, rule => rule.Visual == ConditionalVisual.IconSet);
        var icons = rules.First(rule => rule.Visual == ConditionalVisual.IconSet);
        Assert.Equal("3Arrows", icons.IconSetName);
        Assert.Equal(
            [new ConditionalThreshold(ConditionalThresholdKind.Percent, 0), new ConditionalThreshold(ConditionalThresholdKind.Number, 0), new ConditionalThreshold(ConditionalThresholdKind.Number, 1)],
            icons.Thresholds);

        var reread = RoundTrip(Fixture("tdf101104.ods")).Sheets[0].ConditionalRules.First(rule => rule.Visual == ConditionalVisual.IconSet);
        Assert.Equal(icons.Thresholds, reread.Thresholds);
    }

    [Fact]
    public void SoglieBarreEScaleFannoIlGiro()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        var range = new CellRange(new CellAddress(0, 0), new CellAddress(9, 0));
        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = range, Visual = ConditionalVisual.DataBar, NegativeBarColorHex = "#FF0000",
            Thresholds = [new(ConditionalThresholdKind.Percentile, 10), new(ConditionalThresholdKind.Number, 90)],
        });
        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = range, Priority = 1, Visual = ConditionalVisual.ColorScale, ScaleColors = ["#FF0000", "#00FF00"],
            Thresholds = [new(ConditionalThresholdKind.Number, 2), new(ConditionalThresholdKind.Percent, 80)],
        });
        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = range, Priority = 2, Visual = ConditionalVisual.IconSet, IconSetName = "4Rating", ShowValue = false,
            Thresholds = [new(ConditionalThresholdKind.Percent, 0), new(ConditionalThresholdKind.Number, 3), new(ConditionalThresholdKind.Percentile, 50), new(ConditionalThresholdKind.Number, 8)],
        });

        var reread = RoundTrip(workbook).Sheets[0].ConditionalRules;

        Assert.Equal(sheet.ConditionalRules[0].Thresholds, reread[0].Thresholds);
        Assert.Equal("#FF0000", reread[0].NegativeBarColorHex);
        Assert.Equal(sheet.ConditionalRules[1].Thresholds, reread[1].Thresholds);
        Assert.Equal(ConditionalVisual.IconSet, reread[2].Visual);
        Assert.Equal("4Rating", reread[2].IconSetName);
        Assert.False(reread[2].ShowValue);
        Assert.Equal(sheet.ConditionalRules[2].Thresholds, reread[2].Thresholds);
    }

    [Fact]
    public void LoStileCompletoDellaCondizioneSiSalva()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        var style = Sheets.Styling.CellStyle.Default with
        {
            Font = Sheets.Styling.CellFont.Default with { Underline = true, Strikethrough = true, Size = 15 },
        };
        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = new CellRange(new CellAddress(0, 0), new CellAddress(3, 0)),
            Operator = ConditionOperator.GreaterThan, Operands = ["5"], Style = style,
        });

        var reread = RoundTrip(workbook).Sheets[0].ConditionalRules.Single().Style.Font;

        Assert.True(reread.Underline);
        Assert.True(reread.Strikethrough);
        Assert.Equal(15, reread.Size);
    }
}

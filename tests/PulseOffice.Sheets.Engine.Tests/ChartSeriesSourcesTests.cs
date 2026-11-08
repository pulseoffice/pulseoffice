using PulseOffice.Charts;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class ChartSeriesSourcesTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private (Workbook Workbook, Recalculation.CalculationEngine Engine, Worksheet Data, Worksheet Other) Scene()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var data = workbook.Sheets[0];
        var other = workbook.AddSheet("Altro");
        data[A("A1")] = new Cell { Value = CellValue.Text("Nord") };
        other[A("B1")] = new Cell { Value = CellValue.Text("Sud") };
        for (var i = 0; i < 3; i++)
        {
            data[A($"A{i + 2}")] = new Cell { Value = CellValue.Number(1 + i) };
            other[A($"B{i + 2}")] = new Cell { Value = CellValue.Number(10 + i) };
        }
        var chart = new ChartDefinition
        {
            Id = "grafico1",
            Type = ChartType.Line,
            SourceSheet = data.Name,
            SourceRange = "A1:A4",
            SeriesSources =
            [
                new ChartSeriesSource($"{data.Name}!A1", $"{data.Name}!A2:A4", null),
                new ChartSeriesSource("Altro!B1", "Altro!B2:B4", null),
            ],
        };
        data.Objects.Add(new PlacedObject { Id = chart.Id, Content = chart, Anchor = A("D2") });
        return (workbook, engine, data, other);
    }

    private static ChartDefinition Chart(Worksheet sheet) => Assert.IsType<ChartDefinition>(sheet.Objects[0].Content);

    [Fact]
    public void OgniSerieSiLeggeDalProprioFoglio()
    {
        var (workbook, _, data, _) = Scene();

        var read = new ChartDataExtractor().Extract(workbook, Chart(data));

        Assert.Equal(["Nord", "Sud"], read.Series.Select(series => series.Name));
        Assert.Equal([10d, 11d, 12d], read.Series[1].Values);
        Assert.Equal(["1", "2", "3"], read.Categories);
    }

    [Fact]
    public void UnaSerieAColonnaInteraSiFermaAllAreaUsata()
    {
        var (workbook, _, data, _) = Scene();
        data.Objects[0] = data.Objects[0] with { Content = Chart(data) with { SeriesSources = [new ChartSeriesSource(null, "Altro!B:B", null)] } };

        var read = new ChartDataExtractor().Extract(workbook, Chart(data));

        Assert.Equal(4, read.Series[0].Values.Count);
    }

    [Fact]
    public void InserireRigheSulFoglioDiUnaSerieSpostaIlSuoRiferimento()
    {
        var (_, engine, data, _) = Scene();

        new SheetStructure().InsertRows(engine, 1, 0, 2);

        var sources = Chart(data).SeriesSources!;
        Assert.Equal("Altro!B3", sources[1].Name);
        Assert.Equal("Altro!B4:B6", sources[1].Values);
        Assert.Equal("Sheet1!A2:A4", sources[0].Values);
    }

    [Fact]
    public void InserireRigheNelGraficoCompattoAllargaIlSuoIntervallo()
    {
        var (_, engine, data, _) = Scene();
        data.Objects[0] = data.Objects[0] with { Content = Chart(data) with { SeriesSources = null } };

        new SheetStructure().InsertRows(engine, 0, 2, 1);

        Assert.Equal("A1:A5", Chart(data).SourceRange);
    }

    [Fact]
    public void EliminareLeRigheDiUnaSerieLaRendeRef()
    {
        var (_, engine, data, _) = Scene();

        new SheetStructure().DeleteRows(engine, 1, 0, 4);

        Assert.Equal(ChartSourceReferences.Broken, Chart(data).SeriesSources![1].Values);
    }

    [Fact]
    public void RinominareUnFoglioAggiornaLeSerieEAnnullaInUnPasso()
    {
        var (workbook, engine, data, other) = Scene();
        var renaming = new SheetRenaming();

        var plan = renaming.Plan(workbook, other, "Sud Est");
        renaming.Apply(engine, plan);

        Assert.Equal("'Sud Est'!B2:B4", Chart(data).SeriesSources![1].Values);
        Assert.Equal([10d, 11d, 12d], new ChartDataExtractor().Extract(workbook, Chart(data)).Series[1].Values);

        renaming.Revert(engine, plan);

        Assert.Equal("Altro!B2:B4", Chart(data).SeriesSources![1].Values);
    }

    [Fact]
    public void RinominareIlFoglioDiUnGraficoCompattoNeSegueLaSorgente()
    {
        var (workbook, engine, data, _) = Scene();
        data.Objects[0] = data.Objects[0] with { Content = Chart(data) with { SeriesSources = null } };
        var renaming = new SheetRenaming();

        renaming.Apply(engine, renaming.Plan(workbook, data, "Dati"));

        Assert.Equal("Dati", Chart(data).SourceSheet);
        Assert.Equal([1d, 2d, 3d], new ChartDataExtractor().Extract(workbook, Chart(data)).Series[0].Values);
    }

    [Fact]
    public void EliminareUnFoglioRendeRefLeSueSerieEAnnullareLeRipristina()
    {
        var (workbook, _, data, other) = Scene();
        var charts = new ChartSheetReferences();

        var changes = charts.Removed(workbook, other);
        workbook.RemoveSheet(other);
        charts.Apply(changes, forward: true);

        Assert.Equal(ChartSourceReferences.Broken, Chart(data).SeriesSources![1].Values);
        Assert.Equal("Sheet1!A2:A4", Chart(data).SeriesSources![0].Values);

        workbook.InsertSheet(other, 1);
        charts.Apply(changes, forward: false);

        Assert.Equal("Altro!B2:B4", Chart(data).SeriesSources![1].Values);
    }

    [Fact]
    public void IlGraficoCompattoSiEspandeNelleSerieCheScriverebbeIlFormato()
    {
        var layout = new ChartSourceLayout();
        var compact = new ChartDefinition { Id = "g", Type = ChartType.Column, SourceSheet = "Dati", SourceRange = "A1:C4" };

        var expanded = layout.Expand(compact);

        Assert.Equal(2, expanded.Count);
        Assert.Equal(new ChartSeriesSource("Dati!C1", "Dati!C2:C4", "Dati!A2:A4"), expanded[1]);
        Assert.False(layout.Settle(compact, expanded).HasSeriesSources);
        Assert.True(layout.Settle(compact, [expanded[0], expanded[1] with { Values = "Altro!C2:C4" }]).HasSeriesSources);
    }
}

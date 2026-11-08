using PulseOffice.Charts;
using Xunit;

namespace PulseOffice.Charts.Tests;

public sealed class ChartSettingsMapperTests
{
    private readonly IChartSettingsMapper _mapper = new ChartSettingsMapper();

    private static ChartDefinition Chart() => new()
    {
        Id = "c1",
        Type = ChartType.Line,
        SourceSheet = "Dati",
        SourceRange = "A1:C5",
        ChartTitle = "Vendite",
        CategoryAxisTitle = "Mese",
        ValueAxisTitle = "Euro",
        Legend = LegendPosition.Bottom,
        FirstRowIsHeader = false,
        SeriesInColumns = false,
        ShowDataLabels = true,
        ShowGridLines = false,
    };

    [Fact]
    public void LaPrecompilazioneRiportaTuttiICampiDelGrafico()
    {
        var settings = _mapper.From(Chart());

        Assert.Equal(ChartType.Line, settings.Type);
        Assert.Equal("Vendite", settings.Title);
        Assert.Equal("Mese", settings.CategoryAxisTitle);
        Assert.Equal("Euro", settings.ValueAxisTitle);
        Assert.Equal(LegendPosition.Bottom, settings.Legend);
        Assert.False(settings.FirstRowIsHeader);
        Assert.False(settings.SeriesInColumns);
        Assert.True(settings.ShowDataLabels);
        Assert.False(settings.ShowGridLines);
        Assert.Equal("A1:C5", settings.SourceRange);
    }

    [Fact]
    public void SenzaModificheIlGraficoResteUguale()
    {
        var chart = Chart();

        Assert.Equal(chart, _mapper.Apply(chart, _mapper.From(chart)));
    }

    [Fact]
    public void IlRisultatoApplicaLeScelteEConservaIdentitaEFoglio()
    {
        var chart = Chart();
        var settings = _mapper.From(chart) with
        {
            Type = ChartType.Pie,
            Title = "  ",
            CategoryAxisTitle = " Trimestre ",
            SeriesInColumns = true,
            SourceRange = "B2:D9",
        };

        var updated = _mapper.Apply(chart, settings);

        Assert.Equal("c1", updated.Id);
        Assert.Equal("Dati", updated.SourceSheet);
        Assert.Equal(ChartType.Pie, updated.Type);
        Assert.Null(updated.ChartTitle);
        Assert.Equal("Trimestre", updated.CategoryAxisTitle);
        Assert.True(updated.SeriesInColumns);
        Assert.Equal("B2:D9", updated.SourceRange);
    }

    [Fact]
    public void UnGraficoSenzaIntervalloNonNeRiceveUno()
    {
        var inline = Chart() with { SourceRange = string.Empty, InlineData = ChartData.Empty };

        var settings = _mapper.From(inline);
        var updated = _mapper.Apply(inline, settings with { Type = ChartType.Bar });

        Assert.Null(settings.SourceRange);
        Assert.Equal(string.Empty, updated.SourceRange);
        Assert.Same(ChartData.Empty, updated.InlineData);
    }
}

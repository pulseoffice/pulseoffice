using System.Globalization;
using PulseOffice.Charts;
using Xunit;

namespace PulseOffice.Charts.Tests;

public sealed class ChartPickerLogicTests
{
    private readonly IChartFamilyCatalog _families = new ChartFamilyCatalog();
    private readonly IChartRecommender _recommender = new ChartRecommender();

    [Fact]
    public void OgniTipoAppartieneAUnaSolaFamiglia()
    {
        var all = _families.Families.SelectMany(info => info.Types).ToList();

        Assert.Equal(all.Count, all.Distinct().Count());
        Assert.Equal(Enum.GetValues<ChartType>().Length, all.Count);
        Assert.Equal(ChartFamily.Pie, _families.FamilyOf(ChartType.Doughnut));
        Assert.Equal(ChartType.Column, _families.Find(ChartFamily.Column).Primary);
    }

    [Fact]
    public void UnaSerieDiPartiProponeLaTorta()
    {
        var data = new ChartData(["Nord", "Sud", "Est"], [new ChartSeries("Vendite", [40, 30, 30])]);

        var result = _recommender.Recommend(data, CultureInfo.InvariantCulture);

        Assert.Equal(ChartType.Pie, result[0]);
        Assert.Contains(ChartType.Column, result);
    }

    [Fact]
    public void CategorieNumerichePropongonoLaDispersione()
    {
        var data = new ChartData(["1", "2", "3", "4"], [new ChartSeries("Y", [2, 4, 3, 8])]);

        var result = _recommender.Recommend(data, CultureInfo.InvariantCulture);

        Assert.Equal(ChartType.Scatter, result[0]);
        Assert.DoesNotContain(ChartType.Pie, result);
    }

    [Fact]
    public void UnaColonnaDiNumeriProponeColonneELinee()
    {
        var data = new ChartData(["A", "B"], [new ChartSeries("S", [-1, 4])]);

        var result = _recommender.Recommend(data, CultureInfo.InvariantCulture);

        Assert.Equal([ChartType.Column, ChartType.Line, ChartType.Bar], result);
    }

    [Fact]
    public void SerieLungheEMultipleProponganoAndamentoEImpilato()
    {
        var categories = Enumerable.Range(0, 20).Select(index => $"M{index}").ToList();
        var data = new ChartData(categories,
        [
            new ChartSeries("A", [.. categories.Select((_, index) => (double)index)]),
            new ChartSeries("B", [.. categories.Select((_, index) => (double)index * 2)]),
        ]);

        var result = _recommender.Recommend(data, CultureInfo.InvariantCulture);

        Assert.Equal(ChartType.Line, result[0]);
        Assert.Contains(ChartType.StackedColumn, result);
        Assert.True(result.Count <= ChartRecommender.MaximumSuggestions);
        Assert.Equal(result.Count, result.Distinct().Count());
    }

    [Fact]
    public void SenzaDatiCiSonoComunqueSuggerimenti()
    {
        Assert.NotEmpty(_recommender.Recommend(ChartData.Empty, CultureInfo.InvariantCulture));
    }
}

public sealed class OrdinalCategoryRecommendationTests
{
    [Fact]
    public void UnaSolaColonnaConCategorieGenerateNonProponeLaDispersione()
    {
        var data = new ChartData(["1", "2", "3", "4"], [new ChartSeries("A", [3, 8, -2, 5])]) { OrdinalCategories = true };

        var result = new ChartRecommender().Recommend(data, CultureInfo.InvariantCulture);

        Assert.Equal(ChartType.Column, result[0]);
        Assert.DoesNotContain(ChartType.Scatter, result);
    }
}

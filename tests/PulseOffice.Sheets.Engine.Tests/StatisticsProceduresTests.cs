using PulseOffice.Sheets.Engine.Operations;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class StatisticsProceduresTests
{
    private static readonly IStatisticsProcedures Statistics = new StatisticsProcedures();

    private static double Value(StatisticsTable table, string key, int column = 0) =>
        table.Rows.First(row => row.LabelKey == key).Values[column].AsNumber();

    private static readonly double[] Sample = [2, 4, 4, 4, 5, 5, 7, 9];

    [Fact]
    public void LeStatisticheDescrittiveDannoIValoriNoti()
    {
        var table = Statistics.Descriptive([Sample]);

        Assert.Equal(5, Value(table, "statistics.mean"), 6);
        Assert.Equal(4.5, Value(table, "statistics.median"), 6);
        Assert.Equal(4, Value(table, "statistics.mode"), 6);
        Assert.Equal(7, Value(table, "statistics.range"), 6);
        Assert.Equal(8, Value(table, "statistics.count"), 6);
        Assert.Equal(40, Value(table, "statistics.sum"), 6);
    }

    [Fact]
    public void SenzaValoriRipetutiLaModaNonEsiste()
    {
        var table = Statistics.Descriptive([[1, 2, 3, 4]]);
        var mode = table.Rows.First(row => row.LabelKey == "statistics.mode").Values[0];

        Assert.True(mode.IsError);
    }

    [Fact]
    public void LaCorrelazioneDiUnaSerieConSeStessaEUno()
    {
        var table = Statistics.Correlation([Sample, Sample]);

        Assert.Equal(1, table.Rows[1].Values[0].AsNumber(), 6);
    }

    [Fact]
    public void LaCorrelazioneNegativaSiRiconosce()
    {
        double[] increasing = [1, 2, 3, 4];
        double[] decreasing = [4, 3, 2, 1];

        var table = Statistics.Correlation([increasing, decreasing]);

        Assert.Equal(-1, table.Rows[1].Values[0].AsNumber(), 6);
    }

    [Fact]
    public void LaRegressioneTrovaPendenzaEIntercetta()
    {
        var table = Statistics.LinearRegression([1, 2, 3, 4], [3, 5, 7, 9]);

        Assert.Equal(2, Value(table, "statistics.slope"), 6);
        Assert.Equal(1, Value(table, "statistics.intercept"), 6);
        Assert.Equal(1, Value(table, "statistics.determination"), 6);
    }

    [Fact]
    public void LAnalisiDellaVarianzaDistingueGruppiDiversi()
    {
        var table = Statistics.AnovaSingleFactor([[1, 2, 3], [10, 11, 12]]);

        Assert.True(Value(table, "statistics.anova.between", 3) > 50);
        Assert.True(Value(table, "statistics.anova.between", 4) < 0.01);
    }

    [Fact]
    public void GruppiUgualiNonDannoDifferenzaSignificativa()
    {
        var table = Statistics.AnovaSingleFactor([[1, 2, 3], [1, 2, 3]]);

        Assert.Equal(0, Value(table, "statistics.anova.between", 3), 6);
        Assert.True(Value(table, "statistics.anova.between", 4) > 0.9);
    }

    [Fact]
    public void IlTestTAppaiatoMisuraLaDifferenzaMedia()
    {
        var table = Statistics.TTestPaired([10, 12, 14, 16], [11, 14, 15, 19]);

        Assert.True(Value(table, "statistics.tStatistic") < 0);
        Assert.InRange(Value(table, "statistics.pTwoTail"), 0, 1);
        Assert.Equal(3, Value(table, "statistics.degreesOfFreedom"), 6);
    }

    [Fact]
    public void UnaDifferenzaSenzaVarianzaNonHaUnValoreT()
    {
        var table = Statistics.TTestPaired([10, 12, 14, 16], [11, 13, 15, 17]);

        Assert.True(table.Rows.First(row => row.LabelKey == "statistics.tStatistic").Values[0].IsError);
    }

    [Fact]
    public void IlTestFConfrontaLeVarianze()
    {
        var table = Statistics.FTest([1, 2, 3, 4], [1, 1.5, 2, 2.5]);

        Assert.True(Value(table, "statistics.fStatistic") > 1);
        Assert.InRange(Value(table, "statistics.pOneTail"), 0, 1);
    }

    [Fact]
    public void IlTestZUsaLeVarianzeNote()
    {
        var table = Statistics.ZTest([10, 12, 14], [9, 11, 13], 4, 4);

        Assert.Equal(0.612, Value(table, "statistics.zStatistic"), 2);
        Assert.InRange(Value(table, "statistics.pTwoTail"), 0, 1);
    }

    [Fact]
    public void IlChiQuadroMisuraLIndipendenza()
    {
        var table = Statistics.ChiSquareTest([[10, 20], [20, 40]]);

        Assert.Equal(0, Value(table, "statistics.chiSquare"), 6);
        Assert.Equal(1, Value(table, "statistics.degreesOfFreedom"), 6);
        Assert.Equal(1, Value(table, "statistics.pValue"), 6);
    }

    [Fact]
    public void LaMediaMobileIniziaQuandoLaFinestraEPiena()
    {
        var table = Statistics.MovingAverage([1, 2, 3, 4], window: 2);

        Assert.True(table.Rows[0].Values[0].IsError);
        Assert.Equal(1.5, table.Rows[1].Values[0].AsNumber(), 6);
        Assert.Equal(3.5, table.Rows[3].Values[0].AsNumber(), 6);
    }

    [Fact]
    public void LoSmorzamentoEsponenzialeSegueILDatiConRitardo()
    {
        var table = Statistics.ExponentialSmoothing([10, 20, 30], alpha: 0.5);

        Assert.True(table.Rows[0].Values[0].IsError);
        Assert.Equal(10, table.Rows[1].Values[0].AsNumber(), 6);
        Assert.Equal(15, table.Rows[2].Values[0].AsNumber(), 6);
    }

    [Fact]
    public void LaTrasformataDiFourierEQuellaInversaSiAnnullano()
    {
        double[] values = [1, 2, 3, 4];

        var direct = Statistics.Fourier(values);

        Assert.Equal(10, direct.Rows[0].Values[0].AsNumber(), 6);
        Assert.Equal(0, direct.Rows[0].Values[1].AsNumber(), 6);
        Assert.Equal(4, direct.Rows.Count);
    }

    [Fact]
    public void IlCampionamentoPeriodicoPrendeAPassiRegolari()
    {
        var sample = Statistics.Sample([1, 2, 3, 4, 5, 6], 3, SamplingMode.Periodic);

        Assert.Equal([1, 3, 5], sample);
    }

    [Fact]
    public void IlCampionamentoCasualeConLoStessoSemeSiRipete()
    {
        var first = Statistics.Sample([1, 2, 3, 4, 5, 6], 3, SamplingMode.Random, seed: 42);
        var second = Statistics.Sample([1, 2, 3, 4, 5, 6], 3, SamplingMode.Random, seed: 42);

        Assert.Equal(first, second);
        Assert.Equal(3, first.Count);
        Assert.Equal(first.Count, first.Distinct().Count());
    }

    private static Values.CellValue Cell(StatisticsTable table, string key, int column = 0) =>
        table.Rows.First(row => row.LabelKey == key).Values[column];

    [Fact]
    public void LaRegressioneConXCostanteDaErroreSenzaCadere()
    {
        var table = Statistics.LinearRegression([3, 3, 3], [1, 2, 3]);

        Assert.True(Cell(table, "statistics.slope").IsError);
        Assert.True(Cell(table, "statistics.correlation").IsError);
        Assert.Equal(3, Value(table, "statistics.observations"));
    }

    [Fact]
    public void IlTestFConFMinoreDiUnoUsaLaCodaSinistra()
    {
        var table = Statistics.FTest([1, 1.5, 2, 2.5], [1, 2, 3, 4]);

        Assert.True(Value(table, "statistics.fStatistic") < 1);
        Assert.True(Value(table, "statistics.pOneTail") < 0.5);
    }

    [Fact]
    public void LaCorrelazioneDiColonneDiLunghezzaDiversaTroncaEntrambe()
    {
        var table = Statistics.Correlation([[1, 2, 3, 100], [2, 4, 6]]);

        Assert.Equal(1, Value(table, "statistics.column", 0) , 6);
        Assert.Equal(1, table.Rows[1].Values[0].AsNumber(), 6);
    }

    [Fact]
    public void LaMatriceNonContieneMaiNaN()
    {
        var table = Statistics.Correlation([[1, 1, 1], [1, 2, 3]]);

        Assert.True(table.Rows[1].Values[0].IsError);
    }

    [Fact]
    public void LaModaAParitaPrendeIlValorePiuPiccoloComeCalc()
    {
        var table = Statistics.Descriptive([[5, 5, 2, 2, 9]]);

        Assert.Equal(2, Value(table, "statistics.mode"));
    }
}

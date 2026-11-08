using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class ConditionalFormattingTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private ConditionalFormattingEvaluator Evaluator() =>
        new(fixture.Evaluator);

    private static Worksheet Sheet()
    {
        var sheet = new Worksheet("Foglio1");
        double[] valori = [10, 50, 100];

        for (var i = 0; i < valori.Length; i++)
            sheet.SetValue(new CellAddress(i, 0), CellValue.Number(valori[i]));

        return sheet;
    }

    private static ConditionalRule Rule(ConditionOperator op, params string[] operands) => new()
    {
        Range = new CellRange(A("A1"), A("A3")),
        Operator = op,
        Operands = operands,
        Style = new CellStyle { BackgroundHex = "#FF0000" },
    };

    [Fact]
    public void SenzaRegoleNonCambiaNulla() =>
        Assert.True(Evaluator().Evaluate(Sheet(), A("A1")).IsEmpty);

    [Fact]
    public void LaRegolaSiApplicaSoloDoveEsoddisfatta()
    {
        var sheet = Sheet();
        sheet.ConditionalRules.Add(Rule(ConditionOperator.GreaterThan, "40"));

        var evaluator = Evaluator();

        Assert.True(evaluator.Evaluate(sheet, A("A1")).IsEmpty);
        Assert.Equal("#FF0000", evaluator.Evaluate(sheet, A("A2")).Style?.BackgroundHex);
        Assert.Equal("#FF0000", evaluator.Evaluate(sheet, A("A3")).Style?.BackgroundHex);
    }

    [Fact]
    public void GliOperandiDecimaliSiLeggonoInFormaCanonicaAncheInItaliano()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("it-IT");
        try
        {
            var sheet = Sheet();
            sheet.ConditionalRules.Add(Rule(ConditionOperator.GreaterThan, "49.5"));

            Assert.Equal("#FF0000", Evaluator().Evaluate(sheet, A("A2")).Style?.BackgroundHex);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void LaRegolaNonModificaLaCella()
    {
        var sheet = Sheet();
        sheet.ConditionalRules.Add(Rule(ConditionOperator.GreaterThan, "0"));

        Evaluator().Evaluate(sheet, A("A1"));

        Assert.Equal(0, sheet[A("A1")].StyleId);
    }

    [Theory]
    [InlineData(ConditionOperator.Equal, "50", "A2")]
    [InlineData(ConditionOperator.LessThan, "20", "A1")]
    [InlineData(ConditionOperator.GreaterOrEqual, "100", "A3")]
    public void GliOperatoriDiConfrontoFunzionano(ConditionOperator op, string operand, string expected)
    {
        var sheet = Sheet();
        sheet.ConditionalRules.Add(Rule(op, operand));

        Assert.False(Evaluator().Evaluate(sheet, A(expected)).IsEmpty);
    }

    [Fact]
    public void LIntervalloEsclusivoEInclusivoSiDistinguono()
    {
        var sheet = Sheet();
        sheet.ConditionalRules.Add(Rule(ConditionOperator.Between, "10", "50"));

        var evaluator = Evaluator();

        Assert.False(evaluator.Evaluate(sheet, A("A1")).IsEmpty);
        Assert.False(evaluator.Evaluate(sheet, A("A2")).IsEmpty);
        Assert.True(evaluator.Evaluate(sheet, A("A3")).IsEmpty);
    }

    [Fact]
    public void LeRegoleSulTestoGuardanoIlContenuto()
    {
        var sheet = new Worksheet("Foglio1");
        sheet.SetValue(A("A1"), CellValue.Text("Milano"));
        sheet.SetValue(A("A2"), CellValue.Text("Roma"));

        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = new CellRange(A("A1"), A("A2")),
            Operator = ConditionOperator.ContainsText,
            Operands = ["mila"],
            Style = new CellStyle { BackgroundHex = "#00FF00" },
        });

        var evaluator = Evaluator();

        Assert.False(evaluator.Evaluate(sheet, A("A1")).IsEmpty);
        Assert.True(evaluator.Evaluate(sheet, A("A2")).IsEmpty);
    }

    [Fact]
    public void AParitaDiAttributoVinceLaRegolaConPrioritaPiuAlta()
    {
        var sheet = Sheet();

        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = new CellRange(A("A1"), A("A3")),
            Priority = 1,
            Operator = ConditionOperator.GreaterThan,
            Operands = ["0"],
            Style = new CellStyle { BackgroundHex = "#111111" },
        });

        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = new CellRange(A("A1"), A("A3")),
            Priority = 2,
            Operator = ConditionOperator.GreaterThan,
            Operands = ["40"],
            Style = new CellStyle { BackgroundHex = "#222222" },
        });

        Assert.Equal("#111111", Evaluator().Evaluate(sheet, A("A2")).Style?.BackgroundHex);
        Assert.Equal("#111111", Evaluator().Evaluate(sheet, A("A1")).Style?.BackgroundHex);
    }

    [Fact]
    public void InterrompiSeVeraFermaLeRegoleSuccessive()
    {
        var sheet = Sheet();

        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = new CellRange(A("A1"), A("A3")),
            Priority = 1,
            Operator = ConditionOperator.GreaterThan,
            Operands = ["0"],
            Style = new CellStyle { BackgroundHex = "#111111" },
            StopIfTrue = true,
        });

        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = new CellRange(A("A1"), A("A3")),
            Priority = 2,
            Operator = ConditionOperator.GreaterThan,
            Operands = ["40"],
            Style = new CellStyle { BackgroundHex = "#222222" },
        });

        Assert.Equal("#111111", Evaluator().Evaluate(sheet, A("A2")).Style?.BackgroundHex);
    }

    [Fact]
    public void LeBarreDatiSonoProporzionaliAllIntervallo()
    {
        var sheet = Sheet();
        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = new CellRange(A("A1"), A("A3")),
            Visual = ConditionalVisual.DataBar,
        });

        var evaluator = Evaluator();

        Assert.Equal(0, evaluator.Evaluate(sheet, A("A1")).BarFraction);
        Assert.Equal(1, evaluator.Evaluate(sheet, A("A3")).BarFraction);

        var middle = evaluator.Evaluate(sheet, A("A2")).BarFraction;
        Assert.InRange(middle!.Value, 0.4, 0.5);
    }

    [Fact]
    public void LaBarraDatiCheNascondeIlValoreLoNascondeEPortaLaForma()
    {
        var sheet = Sheet();
        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = new CellRange(A("A1"), A("A3")),
            Visual = ConditionalVisual.DataBar,
            ShowValue = false,
        });

        var appearance = Evaluator().Evaluate(sheet, A("A3"));

        Assert.True(appearance.HideValue);
        Assert.Equal(1, appearance.BarShape!.End, 6);
    }

    [Fact]
    public void LaScalaDiColoriInterpolaFraGliEstremi()
    {
        var sheet = Sheet();
        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = new CellRange(A("A1"), A("A3")),
            Visual = ConditionalVisual.ColorScale,
            ScaleColors = ["#000000", "#FFFFFF"],
        });

        var evaluator = Evaluator();

        Assert.Equal("#000000", evaluator.Evaluate(sheet, A("A1")).ScaleColorHex);
        Assert.Equal("#FFFFFF", evaluator.Evaluate(sheet, A("A3")).ScaleColorHex);

        var middle = evaluator.Evaluate(sheet, A("A2")).ScaleColorHex;
        Assert.NotEqual("#000000", middle);
        Assert.NotEqual("#FFFFFF", middle);
    }

    [Fact]
    public void UnaScalaATreColoriHaIlCentroAMeta()
    {
        var sheet = new Worksheet("Foglio1");
        sheet.SetValue(A("A1"), CellValue.Number(0));
        sheet.SetValue(A("A2"), CellValue.Number(50));
        sheet.SetValue(A("A3"), CellValue.Number(100));

        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = new CellRange(A("A1"), A("A3")),
            Visual = ConditionalVisual.ColorScale,
            ScaleColors = ["#000000", "#FF0000", "#FFFFFF"],
        });

        Assert.Equal("#FF0000", Evaluator().Evaluate(sheet, A("A2")).ScaleColorHex);
    }

    [Fact]
    public void LeCelleVuoteNonRicevonoBarre()
    {
        var sheet = Sheet();
        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = new CellRange(A("A1"), A("A5")),
            Visual = ConditionalVisual.DataBar,
        });

        Assert.Null(Evaluator().Evaluate(sheet, A("A5")).BarFraction);
    }

    [Fact]
    public void LeRegoleFuoriIntervalloNonSiApplicano()
    {
        var sheet = Sheet();
        sheet.SetValue(A("C1"), CellValue.Number(999));
        sheet.ConditionalRules.Add(Rule(ConditionOperator.GreaterThan, "0"));

        Assert.True(Evaluator().Evaluate(sheet, A("C1")).IsEmpty);
    }
}

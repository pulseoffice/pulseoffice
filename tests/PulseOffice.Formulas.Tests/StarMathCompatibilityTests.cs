using Xunit;

namespace PulseOffice.Formulas.Tests;

public class StarMathCompatibilityTests
{
    private readonly IFormulaParser _parser = new FormulaParser();

    [Fact]
    public void IlSimboloConPercentualeEUnaLetteraGreca()
    {
        var root = _parser.Parse("x = %alpha + %iGamma").Root;

        var row = Assert.IsType<MathRow>(root);
        Assert.Contains(row.Children, node => node is MathIdentifier { Text: "α" });
        Assert.Contains(row.Children, node => node is MathIdentifier { Text: "Γ" });
        Assert.DoesNotContain(row.Children, node => node is MathOperator { Text: "%" });
    }

    [Fact]
    public void IlSegnapostoDiStarMathEUnQuadrato()
    {
        var row = Assert.IsType<MathRow>(_parser.Parse("<?> cdot <?> times <?>").Root);

        Assert.Equal(3, row.Children.Count(node => node is MathText { Content: "\u25A1" }));
        Assert.DoesNotContain(row.Children, node => node is MathOperator { Text: "<" or ">" or "?" });
    }

    [Fact]
    public void OverEUnaFrazioneComeInStarMath()
    {
        var fraction = Assert.IsType<MathFraction>(_parser.Parse("a over b").Root);

        Assert.Equal(new MathIdentifier("a"), fraction.Numerator);
        Assert.Equal(new MathIdentifier("b"), fraction.Denominator);
    }

    [Fact]
    public void LeftERightDelimitanoIlContenuto()
    {
        var fenced = Assert.IsType<MathFenced>(_parser.Parse("left [ x right )").Root);

        Assert.Equal("[", fenced.Open);
        Assert.Equal(")", fenced.Close);
        Assert.Equal(new MathIdentifier("x"), fenced.Content);
    }

    [Fact]
    public void LaDiagnosticaAccettaLeftRightEPercentuale()
    {
        Assert.Empty(new FormulaDiagnostics().Check("left [ 0 , 1 right ) + %ialpha + left lbrace x right none"));
    }

    [Fact]
    public void LaMatriceStarMathUsaCancellettoEDoppioCancelletto()
    {
        var matrix = Assert.IsType<MathMatrix>(_parser.Parse("matrix {a # b + 1 ## c # d}").Root);

        Assert.Equal(2, matrix.Rows.Count);
        Assert.Equal(2, matrix.Rows[0].Count);
        Assert.IsType<MathRow>(matrix.Rows[0][1]);
        Assert.Equal(new MathIdentifier("d"), matrix.Rows[1][1]);
    }

    [Fact]
    public void LAnnotazioneMathMlEScrittaInStarMath()
    {
        var source = new StarMathSource();

        Assert.Equal("matrix {a # b ## c # d}", source.ToStarMath("matrix {a & b # c & d}"));
        Assert.Equal("%alpha <= %beta +- x union y", source.ToStarMath("alpha leq beta pm x cup y"));
        Assert.Equal("matrix {a # b ## c # d}", source.ToStarMath("matrix {a # b ## c # d}"));
        Assert.Equal("\"alpha\" + %alpha", source.ToStarMath("\"alpha\" + %alpha"));
    }

    [Fact]
    public void IlTestoStarMathSiRilegge()
    {
        var row = Assert.IsType<MathRow>(_parser.Parse("x <= y +- z").Root);

        Assert.Equal(new MathOperator("≤"), row.Children[1]);
        Assert.Equal(new MathOperator("±"), row.Children[3]);
    }
}

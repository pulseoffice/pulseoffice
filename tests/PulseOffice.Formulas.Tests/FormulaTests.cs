using PulseOffice.Formulas;
using Xunit;

namespace PulseOffice.Formulas.Tests;

public sealed class FormulaParserTests
{
    private readonly FormulaParser _parser = new();

    [Fact]
    public void UnaSommaDiventaUnaSequenza()
    {
        var formula = _parser.Parse("a + b");

        var row = Assert.IsType<MathRow>(formula.Root);

        Assert.Equal(3, row.Children.Count);
        Assert.Equal("a", Assert.IsType<MathIdentifier>(row.Children[0]).Text);
        Assert.Equal("+", Assert.IsType<MathOperator>(row.Children[1]).Text);
    }

    [Fact]
    public void UnaFrazioneHaNumeratoreEDenominatore()
    {
        var fraction = Assert.IsType<MathFraction>(_parser.Parse("frac a b").Root);

        Assert.Equal("a", Assert.IsType<MathIdentifier>(fraction.Numerator).Text);
        Assert.Equal("b", Assert.IsType<MathIdentifier>(fraction.Denominator).Text);
    }

    [Fact]
    public void LApiceDiventaUnEsponente()
    {
        var power = Assert.IsType<MathSuperscript>(_parser.Parse("x^2").Root);

        Assert.Equal("2", Assert.IsType<MathNumber>(power.Exponent).Text);
    }

    [Fact]
    public void ApiceEPediceInsiemeDannoUnSoloNodo()
    {
        var both = Assert.IsType<MathSubSuperscript>(_parser.Parse("x_i^2").Root);

        Assert.Equal("i", Assert.IsType<MathIdentifier>(both.Index).Text);
        Assert.Equal("2", Assert.IsType<MathNumber>(both.Exponent).Text);
    }

    [Fact]
    public void LaRadiceQuadrataNonHaIndice()
    {
        var root = Assert.IsType<MathRoot>(_parser.Parse("sqrt x").Root);

        Assert.Null(root.Index);
    }

    [Fact]
    public void LaRadiceEnnesimaHaIlProprioIndice()
    {
        var root = Assert.IsType<MathRoot>(_parser.Parse("nroot 3 x").Root);

        Assert.Equal("3", Assert.IsType<MathNumber>(root.Index!).Text);
    }

    [Fact]
    public void LeParentesiRestanoNellaFormula()
    {
        var fenced = Assert.IsType<MathFenced>(_parser.Parse("(a + b)").Root);

        Assert.Equal("(", fenced.Open);
        Assert.Equal(")", fenced.Close);
    }

    [Fact]
    public void LeGraffeRaggruppanoSenzaComparire()
    {
        var power = Assert.IsType<MathSuperscript>(_parser.Parse("x^{a + b}").Root);

        Assert.IsType<MathRow>(power.Exponent);
    }

    [Fact]
    public void UnaSommatoriaPortaISuoiEstremi()
    {
        var large = Assert.IsType<MathLargeOperator>(_parser.Parse("sum_{i=1}^n x_i").Root);

        Assert.Equal("∑", large.Symbol);
        Assert.NotNull(large.Lower);
        Assert.NotNull(large.Upper);
    }

    [Fact]
    public void UnaFunzioneNotaPrendeIlProprioArgomento()
    {
        var function = Assert.IsType<MathFunction>(_parser.Parse("sin x").Root);

        Assert.Equal("sin", function.Name);
    }

    [Fact]
    public void ISimboliGreciDiventanoLettere()
    {
        var row = Assert.IsType<MathRow>(_parser.Parse("alpha + beta").Root);

        Assert.Equal("α", Assert.IsType<MathIdentifier>(row.Children[0]).Text);
    }

    [Fact]
    public void UnaMatriceHaRigheECelle()
    {
        var matrix = Assert.IsType<MathMatrix>(_parser.Parse("matrix {a & b # c & d}").Root);

        Assert.Equal(2, matrix.Rows.Count);
        Assert.Equal(2, matrix.Rows[0].Count);
    }

    [Fact]
    public void IlTestoFraVirgolettePassaIntero()
    {
        var text = Assert.IsType<MathText>(_parser.Parse("\"se e solo se\"").Root);

        Assert.Equal("se e solo se", text.Content);
    }

    [Fact]
    public void UnaFormulaVuotaNonHaNodi()
    {
        Assert.True(_parser.Parse("   ").IsEmpty);
    }

    [Fact]
    public void LaSorgenteSiConservaAccantoAllAlbero()
    {
        Assert.Equal("x^2", _parser.Parse("x^2").Source);
    }
}

public sealed class MathMlSyntaxTests
{
    private readonly FormulaParser _parser = new();
    private readonly MathMlSyntax _mathml = new();

    private MathFormula RoundTrip(string source) => _mathml.Read(_mathml.Write(_parser.Parse(source)));

    [Fact]
    public void LaFrazioneAttraversaMathMl()
    {
        var fraction = Assert.IsType<MathFraction>(RoundTrip("frac a b").Root);

        Assert.Equal("a", Assert.IsType<MathIdentifier>(fraction.Numerator).Text);
    }

    [Fact]
    public void LaPotenzaAttraversaMathMl()
    {
        var power = Assert.IsType<MathSuperscript>(RoundTrip("x^2").Root);

        Assert.Equal("2", Assert.IsType<MathNumber>(power.Exponent).Text);
    }

    [Fact]
    public void LaRadiceAttraversaMathMl()
    {
        Assert.IsType<MathRoot>(RoundTrip("sqrt x").Root);
        Assert.NotNull(Assert.IsType<MathRoot>(RoundTrip("nroot 3 x").Root).Index);
    }

    [Fact]
    public void LaMatriceAttraversaMathMl()
    {
        var matrix = Assert.IsType<MathMatrix>(RoundTrip("matrix {a & b # c & d}").Root);

        Assert.Equal(2, matrix.Rows.Count);
    }

    [Fact]
    public void LaSorgenteRestaNellAnnotazione()
    {
        Assert.Equal("x^2", RoundTrip("x^2").Source);
    }

    [Fact]
    public void IlMathMlUsaIlProprioSpazioDeiNomi()
    {
        var element = _mathml.Write(_parser.Parse("x"));

        Assert.Equal(MathMlSyntax.Namespace, element.Name.Namespace);
        Assert.Equal("math", element.Name.LocalName);
    }
}

public sealed class OmmlSyntaxTests
{
    private readonly FormulaParser _parser = new();
    private readonly OmmlSyntax _omml = new();

    private MathFormula RoundTrip(string source) => _omml.Read(_omml.Write(_parser.Parse(source)));

    [Fact]
    public void LaFrazioneAttraversaOmml()
    {
        var fraction = Assert.IsType<MathFraction>(RoundTrip("frac a b").Root);

        Assert.Equal("b", Assert.IsType<MathIdentifier>(fraction.Denominator).Text);
    }

    [Fact]
    public void LaPotenzaAttraversaOmml()
    {
        Assert.IsType<MathSuperscript>(RoundTrip("x^2").Root);
    }

    [Fact]
    public void LaSommatoriaAttraversaOmml()
    {
        var large = Assert.IsType<MathLargeOperator>(RoundTrip("sum_{i=1}^n x").Root);

        Assert.NotNull(large.Lower);
        Assert.NotNull(large.Upper);
    }

    [Fact]
    public void LeParentesiAttraversanoOmml()
    {
        var fenced = Assert.IsType<MathFenced>(RoundTrip("[a]").Root);

        Assert.Equal("[", fenced.Open);
        Assert.Equal("]", fenced.Close);
    }

    [Fact]
    public void LaMatriceAttraversaOmml()
    {
        var matrix = Assert.IsType<MathMatrix>(RoundTrip("matrix {a & b # c & d}").Root);

        Assert.Equal(2, matrix.Rows.Count);
    }

    [Fact]
    public void LOmmlUsaIlProprioSpazioDeiNomi()
    {
        var element = _omml.Write(_parser.Parse("x"));

        Assert.Equal(OmmlSyntax.Namespace, element.Name.Namespace);
        Assert.Equal("oMath", element.Name.LocalName);
    }
}

public sealed class MathParityTests
{
    private readonly FormulaParser _parser = new();
    private readonly MathMlSyntax _mathml = new();
    private readonly OmmlSyntax _omml = new();

    [Theory]
    [InlineData("a + b")]
    [InlineData("frac a b")]
    [InlineData("x^2")]
    [InlineData("x_i")]
    [InlineData("sqrt x")]
    [InlineData("(a + b)")]
    [InlineData("sin x")]
    [InlineData("sum_{i=1}^n x")]
    [InlineData("matrix {a & b # c & d}")]
    public void OgniFormulaAttraversaIDueFormatiAllaStessaManiera(string source)
    {
        var original = _parser.Parse(source);

        var throughMathMl = _mathml.Read(_mathml.Write(original));
        var throughOmml = _omml.Read(_omml.Write(original));

        Assert.Equal(original.Root.GetType(), throughMathMl.Root.GetType());
        Assert.Equal(original.Root.GetType(), throughOmml.Root.GetType());
    }
}

using System.Xml.Linq;
using PulseOffice.Formulas;
using Xunit;

namespace PulseOffice.Formulas.Tests;

public sealed class FormulaRobustnessTests
{
    private static readonly XNamespace M = OmmlSyntax.Namespace;
    private static readonly XNamespace Ml = MathMlSyntax.Namespace;

    private readonly FormulaParser _parser = new();

    [Fact]
    public void UnaParentesiChiusaIsolataNonCancellaIlResto()
    {
        var row = Assert.IsType<MathRow>(_parser.Parse("a ) b + c").Root);

        Assert.Equal(["a", "b", "c"], row.Children.OfType<MathIdentifier>().Select(node => node.Text));
    }

    [Fact]
    public void UnApiceInizialeSenzaBaseNonCancellaIlResto()
    {
        var row = Assert.IsType<MathRow>(_parser.Parse("^2 + x").Root);

        Assert.IsType<MathSuperscript>(row.Children[0]);
        Assert.Equal("x", Assert.IsType<MathIdentifier>(row.Children[^1]).Text);
    }

    [Fact]
    public void LaVirgolaNonFaParteDelNumero()
    {
        var row = Assert.IsType<MathRow>(_parser.Parse("1,2").Root);

        Assert.Equal(3, row.Children.Count);
        Assert.Equal(",", Assert.IsType<MathOperator>(row.Children[1]).Text);
    }

    [Fact]
    public void UnaCoppiaSurrogataEUnSoloSegno()
    {
        var formula = _parser.Parse("\U0001D465 + \U0001F600");
        var row = Assert.IsType<MathRow>(formula.Root);

        Assert.Equal("\U0001D465", Assert.IsType<MathIdentifier>(row.Children[0]).Text);
        Assert.Equal("\U0001F600", Assert.IsType<MathOperator>(row.Children[2]).Text);
    }

    [Fact]
    public void IlMathMlSenzaSemanticsLeggeTuttiIFigli()
    {
        var math = new XElement(Ml + "math", new XElement(Ml + "mi", "x"), new XElement(Ml + "mo", "+"), new XElement(Ml + "mn", "1"));

        var row = Assert.IsType<MathRow>(new MathMlSyntax().Read(math).Root);

        Assert.Equal(3, row.Children.Count);
    }

    [Fact]
    public void IlDelimitatoreOmmlLeggeTutteLeParti()
    {
        var d = new XElement(M + "d",
            new XElement(M + "e", Run("a")),
            new XElement(M + "e", Run("b")));

        var fenced = Assert.IsType<MathFenced>(new OmmlSyntax().Read(new XElement(M + "oMath", d)).Root);
        var row = Assert.IsType<MathRow>(fenced.Content);

        Assert.Equal(3, row.Children.Count);
        Assert.Equal("|", Assert.IsType<MathOperator>(row.Children[1]).Text);
    }

    [Fact]
    public void GliOperatoriEGliAccentiOmmlHannoIlSimboloPredefinito()
    {
        var omml = new OmmlSyntax();

        var nary = Assert.IsType<MathLargeOperator>(omml.Read(new XElement(M + "oMath", new XElement(M + "nary", new XElement(M + "e", Run("x"))))).Root);
        var accent = Assert.IsType<MathAccent>(omml.Read(new XElement(M + "oMath", new XElement(M + "acc", new XElement(M + "e", Run("x"))))).Root);

        Assert.Equal("∫", nary.Symbol);
        Assert.Equal("̂", accent.Accent);
    }

    [Fact]
    public void UnTestoOmmlDiPiuCaratteriSiAnalizza()
    {
        var row = Assert.IsType<MathRow>(new OmmlSyntax().Read(new XElement(M + "oMath", Run("x+1"))).Root);

        Assert.Equal(3, row.Children.Count);
        Assert.IsType<MathNumber>(row.Children[2]);
    }

    private static XElement Run(string text) => new(M + "r", new XElement(M + "t", text));
}

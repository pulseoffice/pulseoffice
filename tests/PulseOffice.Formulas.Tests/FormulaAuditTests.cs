using PulseOffice.Formulas;
using Xunit;

namespace PulseOffice.Formulas.Tests;

public sealed class FormulaAuditTests
{
    [Fact]
    public void UnaFormulaAnnidataMoltoInProfonditaNonFaCadereIlParser()
    {
        var source = new string('{', 20000) + "x" + new string('}', 20000);

        var formula = new FormulaParser().Parse(source);

        Assert.NotNull(formula.Root);
    }

    [Fact]
    public void ParserCondivisoFraThreadDaRisultatiCoerenti()
    {
        var parser = new FormulaParser();

        Parallel.For(0, 200, index =>
        {
            var parsed = parser.ParseWithSpans($"frac {{a{index}}} {{b}}");
            Assert.IsType<MathFraction>(parsed.Formula.Root);
            Assert.True(parsed.Spans.TryGet(parsed.Formula.Root, out _));
        });
    }

    [Fact]
    public void LeVirgoletteApertePuntanoAllaPrimaNonChiusa()
    {
        var problems = new FormulaDiagnostics().Check("\"a\" + \"b");

        var unclosed = Assert.Single(problems, problem => problem.Key == "unclosedText");
        Assert.Equal(6, unclosed.Position);
    }
}

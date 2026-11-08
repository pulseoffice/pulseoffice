using PulseOffice.Formulas;
using Xunit;

namespace PulseOffice.Formulas.Tests;

public sealed class FormulaDiagnosticsTests
{
    private readonly IFormulaDiagnostics _diagnostics = new FormulaDiagnostics();

    [Fact]
    public void AWellFormedFormulaHasNoProblems() =>
        Assert.Empty(_diagnostics.Check("frac {a} {b} + sqrt {x}"));

    [Fact]
    public void AnUnclosedBraceIsReportedWithItsPosition()
    {
        var problems = _diagnostics.Check("frac {a} {b");

        Assert.Equal("unclosed", problems[0].Key);
        Assert.Equal(9, problems[0].Position);
    }

    [Fact]
    public void AMismatchedClosingIsNotTheSameAsAnUnclosedOne() =>
        Assert.Equal("mismatched", _diagnostics.Check("{a)").Single(problem => problem.Key != "unclosed").Key);

    [Fact]
    public void AnUnknownWordIsReportedAndKnownOnesAreNot()
    {
        var problems = _diagnostics.Check("alpha + sqrt {x} + sinx");

        Assert.Equal("unknownWord", Assert.Single(problems).Key);
        Assert.Equal("sinx", Assert.Single(problems).Detail);
    }

    [Fact]
    public void BracesInsideQuotedTextAreNotCounted() =>
        Assert.Empty(_diagnostics.Check("\"{a\""));

    [Fact]
    public void QuotesLeftOpenAreReported() =>
        Assert.Equal("unclosedText", Assert.Single(_diagnostics.Check("\"abc")).Key);
}

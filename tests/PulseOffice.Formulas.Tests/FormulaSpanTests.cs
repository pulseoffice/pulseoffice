using PulseOffice.Formulas;
using Xunit;

namespace PulseOffice.Formulas.Tests;

public sealed class FormulaSpanTests
{
    private readonly IFormulaParser _parser = new FormulaParser();

    [Fact]
    public void TheWholeFormulaCoversTheWholeSource()
    {
        var parsed = _parser.ParseWithSpans("a + b");

        Assert.True(parsed.Spans.TryGet(parsed.Formula.Root, out var span));
        Assert.Equal(0, span.Start);
        Assert.Equal(5, span.End);
    }

    [Fact]
    public void TheCaretInsideTheDenominatorFindsTheDenominator()
    {
        const string source = "frac {a} {bcd}";

        var parsed = _parser.ParseWithSpans(source);
        var node = parsed.Spans.DeepestAt(parsed.Formula.Root, source.IndexOf("bcd", System.StringComparison.Ordinal) + 1);

        Assert.IsType<MathIdentifier>(node);
        Assert.Equal("bcd", ((MathIdentifier)node!).Text);
    }

    [Fact]
    public void AFractionKnowsWhereItStartsAndEnds()
    {
        var parsed = _parser.ParseWithSpans("1 + frac {a} {b}");
        var fraction = Find<MathFraction>(parsed.Formula.Root);

        Assert.True(parsed.Spans.TryGet(fraction, out var span));
        Assert.Equal(4, span.Start);
        Assert.Equal(16, span.End);
    }

    [Fact]
    public void TheDeepestNodeIsTheNarrowestOne()
    {
        const string source = "x^2";

        var parsed = _parser.ParseWithSpans(source);
        var node = parsed.Spans.DeepestAt(parsed.Formula.Root, 2);

        Assert.IsType<MathNumber>(node);
    }

    private static T Find<T>(MathNode node)
    where T : MathNode
    {
        if (node is T found) return found;

        foreach (var child in new MathNodes().Children(node))
            if (Find<T>(child) is { } inner) return inner;

        return null!;
    }
}

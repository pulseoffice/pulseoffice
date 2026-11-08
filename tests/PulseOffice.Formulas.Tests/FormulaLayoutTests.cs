using PulseOffice.Formulas;
using Xunit;

namespace PulseOffice.Formulas.Tests;

public sealed class FormulaLayoutTests
{
    private readonly IFormulaLayout _layout = new FormulaLayout();
    private readonly IFormulaParser _parser = new FormulaParser();

    [Fact]
    public void TheColumnsOfAMatrixAreAsWideAsTheirWidestCell()
    {
        var matrix = (MathMatrix)Find(_parser.Parse("matrix {a & bbbb # cccccc & d}").Root);

        var metrics = _layout.MeasureMatrix(matrix, 20);

        Assert.Equal(2, metrics.Columns.Count);
        Assert.True(metrics.Columns[0] > metrics.Columns[1]);
    }

    [Fact]
    public void ARowIsAsTallAsItsTallestChildAboveAndBelowTheBaseline()
    {
        var row = _parser.Parse("x + frac {a} {b}").Root;
        var fraction = Find(row);

        var rowBox = _layout.Measure(row, 20);
        var fractionBox = _layout.Measure(fraction, 20);

        Assert.True(rowBox.Baseline >= fractionBox.Baseline);
        Assert.True(rowBox.Height >= fractionBox.Height);
    }

    [Fact]
    public void ASuperscriptRaisesTheBaselineOfWhatItSitsOn()
    {
        var plain = _layout.Measure(_parser.Parse("x").Root, 20);
        var raised = _layout.Measure(_parser.Parse("x^2").Root, 20);

        Assert.True(raised.Baseline > plain.Baseline);
    }

    private static MathNode Find(MathNode node)
    {
        if (node is MathMatrix or MathFraction) return node;

        foreach (var child in new MathNodes().Children(node))
            if (Find(child) is { } found && found is MathMatrix or MathFraction) return found;

        return node;
    }
}

using System.Globalization;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.UI.Analysis;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class SolverConstraintRowsTests
{
    private readonly SolverConstraintRows _rows = new(CultureInfo.GetCultureInfo("it-IT"));

    [Fact]
    public void BuildsComparisonWithLocalNumber()
    {
        Assert.True(_rows.TryBuild(new SolverConstraintRow(" b2 ", ConstraintOperator.LessOrEqual, "10,5"), 1, out var constraint));
        Assert.Equal(ConstraintOperator.LessOrEqual, constraint.Operator);
        Assert.Equal(10.5, constraint.Value);
        Assert.Equal(1, constraint.Cell.SheetIndex);
    }

    [Fact]
    public void IntegerAndBinaryIgnoreTheValue()
    {
        Assert.True(_rows.TryBuild(new SolverConstraintRow("C3", ConstraintOperator.Integer, "abc"), 0, out var integer));
        Assert.Equal(ConstraintOperator.Integer, integer.Operator);
        Assert.True(_rows.TryBuild(new SolverConstraintRow("C3", ConstraintOperator.Binary, string.Empty), 0, out _));
    }

    [Fact]
    public void BuildSkipsBlankRowsAndReportsInvalidOnes()
    {
        var outcome = _rows.Build(
        [
            new SolverConstraintRow("A1", ConstraintOperator.GreaterOrEqual, "0"),
            new SolverConstraintRow(" ", ConstraintOperator.Equal, ""),
            new SolverConstraintRow("ZZ", ConstraintOperator.Equal, "1"),
            new SolverConstraintRow("A2", ConstraintOperator.Equal, "x"),
        ], 0);

        Assert.Single(outcome.Constraints);
        Assert.Equal([2, 3], outcome.Invalid);
        Assert.False(outcome.Valid);
    }
}

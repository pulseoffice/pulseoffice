using PulseOffice.Sheets.Engine.Operations;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round277ReferenceCyclerTests
{
    private static readonly ReferenceCycler Cycler = new();

    [Theory]
    [InlineData("=SUM(A:C)", 6, "=SUM($A:$C)")]
    [InlineData("=SUM($A:$C)", 6, "=SUM(A:C)")]
    [InlineData("=SUM(2:5)", 6, "=SUM($2:$5)")]
    [InlineData("=SUM(A1:B2)", 6, "=SUM($A$1:$B$2)")]
    [InlineData("=\"A:C\"&B1", 9, "=\"A:C\"&$B$1")]
    public void Whole_columns_and_rows_cycle_between_relative_and_absolute(string formula, int caret, string expected) =>
        Assert.Equal(expected, Cycler.Cycle(formula, caret));
}

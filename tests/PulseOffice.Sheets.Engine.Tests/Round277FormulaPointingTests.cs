using PulseOffice.Sheets.Engine.Operations;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round277FormulaPointingTests
{
    private static readonly FormulaPointing Pointing = new();

    [Theory]
    [InlineData("=", 1, true)]
    [InlineData("=SUM(", 5, true)]
    [InlineData("=A1+", 4, true)]
    [InlineData("=SUM(A1;)", 8, true)]
    [InlineData("=A1:", 4, true)]
    [InlineData("=A1", 3, false)]
    [InlineData("=\"x+", 4, false)]
    [InlineData("text+", 5, false)]
    [InlineData("=SUM(A1", 5, false)]
    public void A_reference_is_accepted_only_after_an_operator_or_separator(string text, int caret, bool expected) =>
        Assert.Equal(expected, Pointing.AcceptsReference(text, caret));

    [Fact]
    public void Placing_replaces_the_previously_pointed_reference()
    {
        var first = Pointing.Place("=SUM()", 5, 0, "B2");
        var second = Pointing.Place(first.Text, first.Start, first.Length, "B2:C4");

        Assert.Equal("=SUM(B2)", first.Text);
        Assert.Equal("=SUM(B2:C4)", second.Text);
        Assert.Equal(10, second.Caret);
    }
}

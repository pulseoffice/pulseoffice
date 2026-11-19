using PulseOffice.Sheets.Engine.Operations;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round277FormulaCompletionTests
{
    private static readonly FormulaCompletion Completion = new();
    private static readonly string[] Names = ["SUM", "SUMIF", "SUMPRODUCT", "AVERAGE", "Sales"];

    [Theory]
    [InlineData("=su", "=suM")]
    [InlineData("=1+AV", "=1+AVERAGE")]
    [InlineData("=SUM(sa", "=SUM(sales")]
    [InlineData("=SUM", "=SUMIF")]
    public void Completion_extends_the_name_being_typed(string typed, string expected) =>
        Assert.Equal(expected, Completion.Complete(typed, typed.Length, Names), ignoreCase: true);

    [Theory]
    [InlineData("=\"su")]
    [InlineData("=A1")]
    [InlineData("su")]
    [InlineData("=$A")]
    public void Completion_stays_out_of_text_references_and_constants(string typed) =>
        Assert.Null(Completion.Complete(typed, typed.Length, Names));

    [Fact]
    public void The_call_at_the_caret_names_the_innermost_function_and_its_argument()
    {
        Assert.Equal(new FunctionCall("IF", 1), Completion.CallAt("=SUM(1,IF(A1>0,", 15, ','));
        Assert.Equal(new FunctionCall("SUM", 3), Completion.CallAt("=SUM(1,IF(A1,2,3),{1,2},", 24, ','));
        Assert.Equal(new FunctionCall("SUM", 0), Completion.CallAt("=SUM(\"a,b", 5, ','));
        Assert.Null(Completion.CallAt("=SUM(1)", 7, ','));
    }
}

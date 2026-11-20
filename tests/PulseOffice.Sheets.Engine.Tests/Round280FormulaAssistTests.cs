using PulseOffice.Sheets.Engine.Evaluation;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Operations;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round280FormulaAssistTests
{
    private static readonly FunctionSignatures Signatures = new();
    private static readonly FormulaCompletion Completion = new();
    private static readonly FormulaPointing Pointing = new();
    private static readonly string[] Names = ["SUM", "SUMIF", "SUMPRODUCT", "AVERAGE", "Sales"];

    [Fact]
    public void Every_required_function_has_its_argument_names()
    {
        var missing = FunctionInventory.Required.Where(name => !Signatures.Knows(name)).ToList();

        Assert.Empty(missing);
        Assert.Equal(["lookup_value", "table_array", "col_index_num", "range_lookup"], Signatures.Arguments("vlookup"));
        Assert.Empty(Signatures.Arguments("TODAY"));
    }

    [Theory]
    [InlineData("SUM", 4, "number5")]
    [InlineData("SUMIFS", 5, "criteria_range3")]
    [InlineData("SUMIFS", 6, "criteria3")]
    [InlineData("IF", 3, "")]
    public void Repeated_arguments_continue_their_numbering(string function, int index, string expected) =>
        Assert.Equal(expected, Signatures.ArgumentName(function, index));

    [Fact]
    public void Matches_list_every_name_that_starts_with_the_typed_text()
    {
        Assert.Equal(["SUM", "SUMIF", "SUMPRODUCT"], Completion.Matches("=1+su", 5, Names));
        Assert.Equal(["Sales"], Completion.Matches("=SUM(SA", 7, Names));
        Assert.Empty(Completion.Matches("=\"su", 4, Names));
        Assert.Empty(Completion.Matches("=su", 2, Names));
    }

    [Fact]
    public void Accepting_a_function_replaces_the_typed_name_and_opens_its_parenthesis()
    {
        Assert.Equal("=1+SUMIF(", Completion.Accept("=1+su", 5, "SUMIF", isFunction: true));
        Assert.Equal("=SUM(Sales)", Completion.Accept("=SUM(sa)", 7, "Sales", isFunction: false));
    }

    [Theory]
    [InlineData("Data", "Data!B3")]
    [InlineData("Data 2", "'Data 2'!B3")]
    [InlineData("A1", "'A1'!B3")]
    [InlineData("R1C1", "'R1C1'!B3")]
    [InlineData("O'Neil", "'O''Neil'!B3")]
    [InlineData("Revenue", "Revenue!B3")]
    public void A_reference_on_another_sheet_is_qualified_and_quoted_when_needed(string sheet, string expected) =>
        Assert.Equal(expected, Pointing.Qualify(sheet, "B3"));
}

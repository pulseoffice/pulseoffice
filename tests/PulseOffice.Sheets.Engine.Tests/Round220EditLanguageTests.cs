using PulseOffice.Sheets.Engine.Localization;
using PulseOffice.Sheets.Engine.Parsing;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round220EditLanguageTests
{
    private readonly FormulaLocalization _formulas = new(new FormulaParser(), new FunctionNameVocabulary());

    [Fact]
    public void A_complete_formula_moves_to_the_new_separators_and_function_names()
    {
        Assert.Equal("=SUM(1,2.5)", _formulas.Translate("=SOMMA(1;2,5)", "it", "en"));
        Assert.Equal("=SOMMA(1;2,5)", _formulas.Translate("=SUM(1,2.5)", "en", "it"));
    }

    [Fact]
    public void An_unfinished_formula_is_translated_as_typed_so_far()
    {
        Assert.Equal("=SUM(A1,", _formulas.Translate("=SOMMA(A1;", "it", "en"));
    }

    [Fact]
    public void Plain_text_and_the_same_language_are_left_alone()
    {
        Assert.Equal("1,5", _formulas.Translate("1,5", "it", "en"));
        Assert.Equal("=SOMMA(1;2)", _formulas.Translate("=SOMMA(1;2)", "it", "it"));
    }
}

using PulseOffice.Sheets.Engine.Localization;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round210FormulaDisplayTests
{
    private readonly FormulaTextTranslator _translator = new();

    private string Local(string canonical) =>
        _translator.ToLocal(canonical, new FormulaPunctuation(',', ',', ';', '.'), name => name, name => name);

    [Fact]
    public void This_row_references_are_shown_in_the_short_form()
    {
        Assert.Equal("=Sales[@Amount]*2+Sales[@[Unit Price]]", Local("=Sales[[#This Row],[Amount]]*2+Sales[[#This Row],[Unit Price]]"));
    }

    [Fact]
    public void Other_table_references_and_typed_short_forms_are_left_as_they_are()
    {
        Assert.Equal("=SUM(Sales[[#Headers],[Name]],Sales[Amount])", Local("=SUM(Sales[[#Headers],[Name]];Sales[Amount])"));
        Assert.Equal("=Sales[@Amount]", Local("=Sales[@Amount]"));
    }
}

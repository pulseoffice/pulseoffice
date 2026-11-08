using System.Globalization;
using System.Text;
using PulseOffice.Localization;
using PulseOffice.Sheets.UI.Dialogs;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class CsvImportChoicesTests
{
    private readonly CsvImportPrompt _prompt = new(null!, new TextCatalog(UiLanguage.Italian), null!, null!);

    [Fact]
    public void Una_codifica_rilevata_fuori_elenco_resta_fra_le_scelte()
    {
        var choices = _prompt.EncodingChoices(Encoding.Latin1);

        Assert.Contains(choices, choice => choice.CodePage == Encoding.Latin1.CodePage);
        Assert.Single(_prompt.EncodingChoices(Encoding.UTF8), choice => choice.CodePage == Encoding.UTF8.CodePage);
    }

    [Fact]
    public void Una_lingua_rilevata_fuori_elenco_resta_fra_le_scelte()
    {
        var portuguese = CultureInfo.GetCultureInfo("pt-BR");

        Assert.Contains(_prompt.LanguageChoices(portuguese), choice => choice.Culture == "pt-BR");
        Assert.Single(_prompt.LanguageChoices(CultureInfo.GetCultureInfo("it-IT")), choice => choice.Culture == "it-IT");
    }
}

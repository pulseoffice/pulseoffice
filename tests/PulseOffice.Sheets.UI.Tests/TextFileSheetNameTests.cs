using PulseOffice.Sheets.UI;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class TextFileSheetNameTests
{
    [Theory]
    [InlineData("/dati/vendite.csv", "vendite")]
    [InlineData("/dati/conti [2026]: Q1?.csv", "conti _2026__ Q1_")]
    [InlineData("/dati/un nome di file davvero molto lungo per un foglio.csv", "un nome di file davvero molto l")]
    public void Il_foglio_di_un_CSV_prende_il_nome_del_file(string path, string expected) =>
        Assert.Equal(expected, new TextFileSheetName().FromFile(path));
}

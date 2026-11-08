using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class DefinedNameSuggestionTests
{
    private readonly DefinedNameRules _rules = new();

    private CellRange R(string a1)
    {
        Assert.True(CellRange.TryParseA1(a1, out var range));
        return range;
    }

    [Fact]
    public void UnaRigaPrendeLEtichettaASinistraAncheOltreUnaColonnaVuota()
    {
        var sheet = new Worksheet("Bilancio");
        sheet.SetValue(new CellAddress(11, 0), CellValue.Text("Totale entrate"));
        sheet.SetValue(new CellAddress(10, 2), CellValue.Number(42000));

        Assert.Equal("Totale_entrate", _rules.Suggested(sheet, R("C12:N12")));
    }

    [Fact]
    public void UnaColonnaPrendeLIntestazioneSopraEUnNumeroNonEUnNome()
    {
        var sheet = new Worksheet("Bilancio");
        sheet.SetValue(new CellAddress(2, 2), CellValue.Text("Gen"));
        Assert.Equal("Gen", _rules.Suggested(sheet, R("C4:C11")));

        sheet.SetValue(new CellAddress(2, 2), CellValue.Number(7));
        Assert.Equal(string.Empty, _rules.Suggested(sheet, R("C4:C11")));
    }
}

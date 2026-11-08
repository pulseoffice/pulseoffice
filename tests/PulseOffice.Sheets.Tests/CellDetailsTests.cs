using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class CellDetailsTests
{
    private static readonly CellRange Area = new(new CellAddress(0, 0), new CellAddress(2, 1));

    [Fact]
    public void I_dettagli_rari_ai_valori_predefiniti_non_cambiano_l_uguaglianza()
    {
        var plain = new Cell { Value = CellValue.Number(3) };
        var reset = plain with { StyleName = null, Hyperlink = null, SpillRange = null, IsSpilled = false, ArrayFormulaRange = null, IsArrayAnchor = false };

        Assert.Equal(plain, reset);
        Assert.Equal(plain.GetHashCode(), reset.GetHashCode());
    }

    [Fact]
    public void I_dettagli_rari_sopravvivono_alle_copie_e_si_tolgono_uno_per_volta()
    {
        var cell = new Cell { Formula = "=1", ArrayFormulaRange = Area, IsArrayAnchor = true, Hyperlink = "https://example.org", StyleName = "Titolo" };
        var copy = cell with { Value = CellValue.Number(1) };

        Assert.Equal(Area, copy.ArrayFormulaRange);
        Assert.True(copy.IsArrayAnchor);
        Assert.Equal("https://example.org", copy.Hyperlink);
        Assert.Equal("Titolo", copy.StyleName);

        var withoutLink = copy with { Hyperlink = null };
        Assert.Null(withoutLink.Hyperlink);
        Assert.Equal("https://example.org", copy.Hyperlink);
        Assert.True(withoutLink.IsPartOfArrayFormula);
        Assert.NotEqual(copy, withoutLink);
    }
}

using PulseOffice.Text.Spelling;
using Xunit;

namespace PulseOffice.Text.Spelling.Tests;

public class ShiftedReplacementsTests
{
    [Fact]
    public void LeParoleSuccessiveDellaStessaCellaSiSpostanoDellaDifferenza()
    {
        var replacements = new ShiftedReplacements<string>();
        const string original = "teh cat teh dog";

        var first = replacements.Replace("A1", original, new WordSpan(0, 3, "teh"), "the big");
        var second = replacements.Replace("A1", first!, new WordSpan(8, 3, "teh"), "the");

        Assert.Equal("the big cat the dog", second);
    }

    [Fact]
    public void UnTestoCambiatoNonVieneToccato()
    {
        var replacements = new ShiftedReplacements<string>();

        Assert.Null(replacements.Replace("A1", "altro testo", new WordSpan(0, 3, "teh"), "the"));
    }
}

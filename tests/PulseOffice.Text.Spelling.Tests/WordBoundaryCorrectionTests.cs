using PulseOffice.Text.Spelling;
using Xunit;

namespace PulseOffice.Text.Spelling.Tests;

public sealed class WordBoundaryCorrectionTests
{
    private readonly WordBoundaryCorrection _boundary = new(new AutoCorrect(new AutoCorrectFixture().Table, new WordTokenizer()));
    private readonly AutoCorrectOptions _options = AutoCorrectOptions.Default with { SmartQuotes = false, Language = "en-US" };

    [Fact]
    public void LoSpazioCorreggeLaParolaAppenaConclusa()
    {
        var correction = _boundary.AfterTyping("Questo è teh", "Questo è teh ", 13, _options);
        Assert.NotNull(correction);
        Assert.Equal("Questo è the ", correction.Text);
        Assert.Equal(13, correction.Caret);
        Assert.Equal((9, 4, "the "), (correction.Start, correction.Length, correction.Replacement));
    }

    [Fact]
    public void IlCursoreSegueUnaSostituzionePiuCorta()
    {
        var correction = _boundary.AfterTyping("Dimmi qual'è", "Dimmi qual'è oggi", 13, _options with { Language = "it" });
        Assert.Null(correction);

        var typed = _boundary.AfterTyping("Dimmi qual'è oggi", "Dimmi qual'è, oggi", 13, _options with { Language = "it" });
        Assert.NotNull(typed);
        Assert.Equal("Dimmi qual è, oggi", typed.Text);
        Assert.Equal(13, typed.Caret);
    }

    [Fact]
    public void IlRitornoACapoDiWindowsCorreggeLaParola()
    {
        var correction = _boundary.AfterTyping("Prova teh", "Prova teh\r\n", 11, _options);
        Assert.NotNull(correction);
        Assert.Equal("Prova the\r\n", correction.Text);
        Assert.Equal(11, correction.Caret);
    }

    [Fact]
    public void SiCorreggeSoloLUltimaParola()
    {
        var correction = _boundary.AfterTyping("teh ciao. come", "teh ciao. come ", 15, _options);
        Assert.NotNull(correction);
        Assert.Equal("teh ciao. Come ", correction.Text);
    }

    [Fact]
    public void SenzaConfineNonSiCorregge()
    {
        Assert.Null(_boundary.AfterTyping("tutto teh", "tutto tehx", 10, _options));
        Assert.Null(_boundary.AfterTyping("Va bene", "Va bene ", 8, _options));
        Assert.Null(_boundary.AfterTyping("abc", "abcde", 5, _options));
    }

    [Fact]
    public void AllaFineSiCorreggeLUltimaParolaSenzaConfine()
    {
        Assert.Equal("Scrivo the", _boundary.AtEnd("Scrivo teh", _options)?.Text);
        Assert.Null(_boundary.AtEnd("Scrivo teh ", _options));
    }

    [Fact]
    public void LApostrofoNonConcludeLaParola()
    {
        Assert.False(_boundary.IsBoundary('\''));
        Assert.False(_boundary.IsBoundary('’'));
        Assert.Null(_boundary.AfterTyping("Prova teh", "Prova teh'", 10, _options));
    }
}

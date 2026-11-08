using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Text.Rich.Tests;

public class CaretWordTests
{
    private readonly ICaretWord _word = new CaretWord();

    [Theory]
    [InlineData("Uno due tre", 5, 4, 7)]
    [InlineData("Uno due tre", 1, 0, 3)]
    [InlineData("dell'anno nuovo", 3, 0, 9)]
    [InlineData("Uno due tre", 3, 3, 3)]
    [InlineData("Uno due tre", 4, 4, 4)]
    [InlineData("Uno due tre", 11, 11, 11)]
    [InlineData("", 0, 0, 0)]
    public void FindsTheWordOnlyWhenTheCaretIsInsideIt(string text, int caret, int start, int end) =>
        Assert.Equal((start, end), _word.Around(text, caret));

    [Fact]
    public void ACombiningMarkIsPartOfTheWord()
    {
        var text = "caffé nero";
        Assert.Equal((0, 6), new CaretWord().Around(text, 2));
    }

    [Fact]
    public void ALetterOutsideTheBasicPlaneIsPartOfTheWord()
    {
        var text = "a\U0001D400b c";
        Assert.Equal((0, 4), new CaretWord().Around(text, 1));
    }

    [Fact]
    public void TheWordEnclosingTheEndOfAWordIsTheWholeWord()
    {
        Assert.Equal((0, 7), _word.Enclosing("dell'io x", 7));
        Assert.Equal((0, 6), _word.Enclosing("caffé", 6));
    }
}

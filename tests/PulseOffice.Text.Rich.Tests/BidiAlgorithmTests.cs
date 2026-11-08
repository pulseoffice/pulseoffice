using PulseOffice.Text.Rich.Bidi;
using Xunit;

namespace PulseOffice.Text.Rich.Tests;

public sealed class BidiAlgorithmTests
{
    private readonly BidiAlgorithm _algorithm = new();
    private readonly BidiReordering _reordering = new();
    private readonly BidiMirroring _mirroring = new();

    private string Visual(string text, TextDirection? direction)
    {
        var paragraph = _algorithm.Resolve(text, direction);
        var levels = _reordering.LineLevels(paragraph, 0, text.Length);
        var order = _reordering.VisualOrder(levels);
        return string.Concat(order.Select(index => (levels[index] & 1) == 1 ? (char)_mirroring.Mirror(text[index]) : text[index]));
    }

    [Fact]
    public void LatinTextStaysAtLevelZero()
    {
        var paragraph = _algorithm.Resolve("abc", null);

        Assert.Equal(0, paragraph.BaseLevel);
        Assert.Equal([0, 0, 0], paragraph.Levels);
        Assert.True(paragraph.IsUniform);
    }

    [Fact]
    public void HebrewTextMakesARightToLeftParagraphAndIsReversed()
    {
        var paragraph = _algorithm.Resolve("אבג", null);

        Assert.True(paragraph.RightToLeft);
        Assert.Equal([1, 1, 1], paragraph.Levels);
        Assert.Equal("גבא", Visual("אבג", null));
    }

    [Fact]
    public void EuropeanDigitsInsideHebrewKeepTheirOrder()
    {
        Assert.Equal([1, 1, 1, 1, 2, 2, 2], _algorithm.Resolve("אבג 123", null).Levels);
        Assert.Equal("123 גבא", Visual("אבג 123", null));
    }

    [Fact]
    public void HebrewWordInsideALatinSentenceIsReversedInPlace()
    {
        Assert.Equal("abc גבא def", Visual("abc אבג def", TextDirection.LeftToRight));
    }

    [Fact]
    public void PairedBracketsTakeTheEmbeddingDirectionAndAreMirrored()
    {
        Assert.Equal([1, 1, 2, 1], _algorithm.Resolve("א(b)", null).Levels);
        Assert.Equal("(b)א", Visual("א(b)", null));
    }

    [Fact]
    public void RightToLeftOverrideReversesLatinLetters()
    {
        var levels = _algorithm.Resolve("‮abc‬", TextDirection.LeftToRight).Levels;

        Assert.Equal([1, 1, 1], levels[1..4]);
    }

    [Fact]
    public void RightToLeftIsolateRaisesLatinToLevelTwoAndLeavesTheOutsideAlone()
    {
        var levels = _algorithm.Resolve("⁧abc⁩ x", TextDirection.LeftToRight).Levels;

        Assert.Equal([0, 2, 2, 2, 0, 0, 0], levels);
    }

    [Fact]
    public void FirstStrongIsolateFollowsItsContent()
    {
        var levels = _algorithm.Resolve("x⁨אב⁩", TextDirection.LeftToRight).Levels;

        Assert.Equal([0, 0, 1, 1, 0], levels);
    }

    [Fact]
    public void DigitsAfterArabicLettersBecomeArabicNumbers()
    {
        Assert.Equal([1, 1, 2, 2], _algorithm.Resolve("ب 12", null).Levels);
    }

    [Fact]
    public void SeparatorBetweenDigitsJoinsTheNumber()
    {
        Assert.Equal([0, 0, 0], _algorithm.Resolve("1+2", TextDirection.LeftToRight).Levels);
        Assert.Equal([2, 2, 2], _algorithm.Resolve("1+2", TextDirection.RightToLeft).Levels);
    }

    [Fact]
    public void CurrencyTerminatorBeforeDigitsJoinsTheNumber()
    {
        Assert.Equal([1, 1, 2, 2, 2], _algorithm.Resolve("א $12", null).Levels);
    }

    [Fact]
    public void TrailingWhitespaceReturnsToTheParagraphLevel()
    {
        var paragraph = _algorithm.Resolve("‪abc ‬", TextDirection.RightToLeft);

        Assert.Equal(2, paragraph.Levels[4]);
        Assert.Equal([1, 2, 2, 2, 1, 1], _reordering.LineLevels(paragraph, 0, 6));
    }

    [Fact]
    public void RunEmbeddingsActLikeInvisibleEmbeddingControls()
    {
        var paragraph = _algorithm.Resolve("ab cd", TextDirection.LeftToRight, [new BidiEmbedding(3, 5, TextDirection.RightToLeft)]);

        Assert.Equal([0, 0, 0, 2, 2], paragraph.Levels);
    }

    [Fact]
    public void ExplicitDirectionWinsOverTheFirstStrongCharacter()
    {
        Assert.Equal(0, _algorithm.Resolve("אבג", TextDirection.LeftToRight).BaseLevel);
        Assert.Equal(1, _algorithm.Resolve("abc", TextDirection.RightToLeft).BaseLevel);
    }

    [Fact]
    public void VisualOrderReversesFromTheHighestLevel()
    {
        Assert.Equal([0, 1, 4, 3, 2, 5], _reordering.VisualOrder([0, 0, 1, 1, 1, 0]));
        Assert.Equal([3, 1, 2, 0], _reordering.VisualOrder([1, 2, 2, 1]));
    }

    [Fact]
    public void SurrogatePairsKeepBothUnitsTogether()
    {
        var text = "𐤀𐤁";

        Assert.Equal([1, 1, 1, 1], _algorithm.Resolve(text, null).Levels);
    }

    [Fact]
    public void DeepEmbeddingsStopAtTheMaximumDepth()
    {
        var text = string.Concat(Enumerable.Repeat("‫", 200)) + "a";

        Assert.True(_algorithm.Resolve(text, TextDirection.LeftToRight).Levels[^1] <= 126);
    }
}

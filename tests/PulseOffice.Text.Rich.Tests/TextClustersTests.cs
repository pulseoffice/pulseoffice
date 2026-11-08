using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Text.Rich.Tests;

public sealed class TextClustersTests
{
    private readonly ITextClusters _clusters = new TextClusters();

    [Theory]
    [InlineData("a👍🏽b", 1, 5)]
    [InlineData("a🇮🇹b", 1, 5)]
    [InlineData("aéb", 1, 3)]
    [InlineData("ab", 1, 2)]
    public void UnGrafemaSiAttraversaInUnPassoSolo(string text, int from, int to)
    {
        Assert.Equal(to, _clusters.Next(text, from));
        Assert.Equal(from, _clusters.Previous(text, to));
    }

    [Fact]
    public void LeParoleSiSeparanoAlCambioDiScrittura()
    {
        var words = new WordMotion(_clusters);

        Assert.Equal(3, words.Next("日本語のテキスト", 0));
        Assert.Equal(4, words.Next("日本語のテキスト", 3));
        Assert.Equal(8, words.Next("l'acqua è", 0));
        Assert.Equal(0, words.Previous("uno, due", 3));
        Assert.Equal(5, words.Previous("uno, due", 8));
    }
}

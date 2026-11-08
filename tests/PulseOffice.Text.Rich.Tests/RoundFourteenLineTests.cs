using System.Diagnostics;
using Xunit;

namespace PulseOffice.Text.Rich.Tests;

public sealed class RoundFourteenLineTests
{
    private readonly ParagraphLineBreaker _breaker = new();

    private double Measured(string content, int start, int end)
    {
        var width = 0.0;
        for (var index = start; index < end; index++) width += content[index] == ' ' ? 4 : 7;
        return width;
    }

    [Fact]
    public void ALongJustifiedParagraphIsBrokenInLinearTime()
    {
        var content = string.Join(' ', Enumerable.Range(0, 30_000).Select(index => "parola" + index % 7));
        var watch = Stopwatch.StartNew();
        var lines = _breaker.Break(content, 400, 400, (start, end) => Measured(content, start, end));
        var elapsed = watch.Elapsed;
        Assert.True(lines.Count > 3000, $"lines: {lines.Count}");
        Assert.True(elapsed < TimeSpan.FromSeconds(1), $"broken in {elapsed}");
    }

    [Fact]
    public void BreakingFromAnOffsetMatchesBreakingTheRemainder()
    {
        const string content = "uno due tre　漢字かなカナ quattro cinque sei sette otto";
        var breaker = new FirstLineBreaker();
        for (var start = 0; start < content.Length; start++)
        {
            var from = start;
            var alone = breaker.Break(content[from..], 60, end => end * 7.0);
            var offset = breaker.Break(content, from, 60, end => end * 7.0);
            Assert.Equal(alone, offset);
        }
    }

    [Fact]
    public void TheLinesAreTheSameAsTheGreedyChoice()
    {
        var content = "alfa beta gamma delta epsilon zeta eta theta iota kappa lambda mu nu xi omicron pi rho sigma tau";
        var lines = _breaker.Break(content, 90, 120, (start, end) => Measured(content, start, end));
        foreach (var line in lines.Take(lines.Count - 1))
        {
            Assert.True(Measured(content, line.Start, line.End) <= (line.Start == 0 ? 90 : 120));
            var nextWord = content.IndexOf(' ', line.Next);
            var longer = nextWord < 0 ? content.Length : nextWord;
            Assert.True(Measured(content, line.Start, longer) > (line.Start == 0 ? 90 : 120));
        }
    }
}

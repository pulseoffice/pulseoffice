using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Text.Rich.Tests;

public class ParagraphLinesTests
{
    private readonly ParagraphLineBreaker _breaker = new();
    private readonly LineJustifier _justifier = new();
    private readonly PlainTextPositions _positions = new();

    private double Width(string content, int start, int end) => content[start..end].TrimEnd().Length * 10.0;

    [Fact]
    public void LaPrimaRigaUsaLaSuaLarghezzaELeAltreQuellaPiena()
    {
        const string content = "uno due tre quattro cinque";

        var lines = _breaker.Break(content, 75, 115, (s, e) => Width(content, s, e));

        Assert.Equal(["uno due", "tre quattro", "cinque"], lines.Select(l => content[l.Start..l.End]));
        Assert.False(lines[0].Last);
        Assert.False(lines[1].Last);
        Assert.True(lines[2].Last);
    }

    [Fact]
    public void IlRitornoACapoChiudeLaRigaComeUltima()
    {
        const string content = "ab\vcd";

        var lines = _breaker.Break(content, 500, 500, (s, e) => Width(content, s, e));

        Assert.Equal(2, lines.Count);
        Assert.True(lines[0].Last);
        Assert.Equal("cd", content[lines[1].Start..lines[1].End]);
    }

    [Fact]
    public void UnParagrafoVuotoHaUnaRiga()
    {
        Assert.Single(_breaker.Break(string.Empty, 10, 10, (_, _) => 0));
    }

    [Fact]
    public void LaRigaGiustificataArrivaAlMargine()
    {
        const string content = "aa bb cc";
        var line = new BrokenLine(0, content.Length, content.Length, false);

        var pieces = _justifier.Spread(content, line, 100, (s, e) => (e - s) * 10.0);

        Assert.Equal(3, pieces.Count);
        Assert.Equal(0, pieces[0].X);
        Assert.Equal(30 + 10, pieces[1].X, 3);
        Assert.Equal(60 + 20, pieces[2].X, 3);
        Assert.Equal(100, pieces[2].X + 20, 3);
    }

    [Fact]
    public void LUltimaRigaNonSiAllarga()
    {
        const string content = "aa bb";
        var pieces = _justifier.Spread(content, new BrokenLine(0, 5, 5, true), 100, (s, e) => (e - s) * 10.0);

        Assert.Equal(30, pieces[1].X, 3);
    }

    [Theory]
    [InlineData("ab\ncd", 3, 1, 0)]
    [InlineData("ab\r\ncd", 4, 1, 0)]
    [InlineData("ab\r\ncd", 6, 1, 2)]
    [InlineData("ab\ncd", 2, 0, 2)]
    public void LePosizioniSeguonoIParagrafi(string text, int index, int paragraph, int offset)
    {
        var position = _positions.Locate(text, index);

        Assert.Equal(new TextPosition(paragraph, offset), position);
        Assert.Equal(index, _positions.Index(text, position));
    }

    [Fact]
    public void LaLunghezzaDelParagrafoEsclusoIlSeparatore()
    {
        Assert.Equal(3, _positions.ParagraphLength("ab\r\ncde", 1));
        Assert.Equal(5, _positions.Index("ab\ncde", new TextPosition(1, 99)) - 1);
    }

    [Fact]
    public void AnIndexNeverFallsInsideAnEmoji()
    {
        var text = "a\U0001F600b";
        Assert.Equal(1, new PlainTextPositions().Index(text, new TextPosition(0, 2)));
    }
}

using Xunit;
using PulseOffice.Text.Rich;

namespace PulseOffice.Text.Rich.Tests;

public sealed class RichTextFormatterTests
{
    private readonly IRichTextFormatter _fmt = new RichTextFormatter();

    private RichText One(string text) =>
        new(PlaceholderRole.Body, [new TextParagraph(text)]);

    [Fact]
    public void ToggleBoldWholePlaceholder()
    {
        var back = _fmt.ToggleChar(One("Hello"), null, CharAttribute.Bold);
        var run = Assert.Single(back.Paragraphs[0].EffectiveRuns());
        Assert.True(run.Bold);

        var off = _fmt.ToggleChar(back, null, CharAttribute.Bold);
        Assert.False(off.Paragraphs[0].EffectiveRuns()[0].Bold);
    }

    [Fact]
    public void ToggleBoldRangeSplitsRuns()
    {
        var back = _fmt.ToggleChar(One("Hello world"), (0, 5), CharAttribute.Bold);
        var runs = back.Paragraphs[0].Runs;

        Assert.Equal(2, runs.Count);
        Assert.Equal("Hello", runs[0].Text);
        Assert.True(runs[0].Bold);
        Assert.Equal(" world", runs[1].Text);
        Assert.False(runs[1].Bold);
    }

    [Fact]
    public void ColorAndScaleAndUnderlineOnRange()
    {
        var colored = _fmt.SetColor(One("abcdef"), (2, 4), 0xFFFF0000, null);
        Assert.Equal("cd", colored.Paragraphs[0].Runs.Single(r => r.Color == 0xFFFF0000).Text);

        var scaled = _fmt.Scale(One("abc"), null, 1.2);
        Assert.Equal(18 * 1.2, scaled.Paragraphs[0].EffectiveRuns()[0].FontSize);

        var underlined = _fmt.ToggleChar(One("abcdef"), (0, 3), CharAttribute.Underline);
        Assert.True(underlined.Paragraphs[0].Runs[0].Underline);
        Assert.False(underlined.Paragraphs[0].Runs[1].Underline);
    }

    [Fact]
    public void AlignAndBulletsAndIndentAreParagraphLevel()
    {
        var centered = _fmt.SetAlign(One("x"), null, TextAlign.Center);
        Assert.Equal(TextAlign.Center, centered.Paragraphs[0].Align);

        var bulleted = _fmt.ToggleBullets(One("x"), null);
        Assert.True(bulleted.Paragraphs[0].Bullet);
        Assert.False(_fmt.ToggleBullets(bulleted, null).Paragraphs[0].Bullet);

        var indented = _fmt.Indent(One("x"), null, 1);
        Assert.Equal(1, indented.Paragraphs[0].BulletLevel);
    }

    [Fact]
    public void RangeAcrossTwoParagraphsFormatsBoth()
    {
        var text = new RichText(PlaceholderRole.Body,
            [new TextParagraph("aaa"), new TextParagraph("bbb")]);
        var back = _fmt.ToggleChar(text, (1, 5), CharAttribute.Bold);

        Assert.Contains(back.Paragraphs[0].Runs, r => r.Text == "aa" && r.Bold);
        Assert.Contains(back.Paragraphs[1].Runs, r => r.Bold);
    }

    [Fact]
    public void SetFontAndSizeAbsolute()
    {
        var back = _fmt.SetFont(One("abc"), null, "Georgia");
        Assert.Equal("Georgia", back.Paragraphs[0].EffectiveRuns()[0].Font);

        var sized = _fmt.SetFontSize(back, null, 40);
        Assert.Equal(40, sized.Paragraphs[0].EffectiveRuns()[0].FontSize);

        var cleared = _fmt.SetFont(back, null, "");
        Assert.Null(cleared.Paragraphs[0].EffectiveRuns()[0].Font);
    }

    [Fact]
    public void SetCharForcesStateRegardlessOfCurrent()
    {
        var on = _fmt.SetChar(One("abc"), null, CharAttribute.Italic, true);
        Assert.True(on.Paragraphs[0].EffectiveRuns()[0].Italic);

        var stillOn = _fmt.SetChar(on, null, CharAttribute.Italic, true);
        Assert.True(stillOn.Paragraphs[0].EffectiveRuns()[0].Italic);

        var off = _fmt.SetChar(on, null, CharAttribute.Italic, false);
        Assert.False(off.Paragraphs[0].EffectiveRuns()[0].Italic);
    }

    [Fact]
    public void SetBulletAndLevelAbsolute()
    {
        var text = new RichText(PlaceholderRole.Body,
            [new TextParagraph("a"), new TextParagraph("b")]);

        var bulleted = _fmt.SetBullet(text, null, true);
        Assert.All(bulleted.Paragraphs, p => Assert.True(p.Bullet));

        var levelled = _fmt.SetBulletLevel(bulleted, null, 3);
        Assert.All(levelled.Paragraphs, p => Assert.Equal(3, p.BulletLevel));

        var clamped = _fmt.SetBulletLevel(bulleted, null, 99);
        Assert.All(clamped.Paragraphs, p => Assert.Equal(8, p.BulletLevel));
    }

    [Fact]
    public void ReadCharacterReportsCommonAndMixed()
    {
        var uniform = _fmt.SetChar(_fmt.SetFontSize(One("hello"), null, 22), null, CharAttribute.Bold, true);
        var read = _fmt.ReadCharacter(uniform, null);
        Assert.Equal(22, read.Size);
        Assert.True(read.Bold);
        Assert.False(read.Italic);

        var mixed = _fmt.ToggleChar(One("hello"), (0, 2), CharAttribute.Bold);
        Assert.Null(_fmt.ReadCharacter(mixed, null).Bold);
    }

    [Fact]
    public void HighlightAppliesToRangeAndIsReadBack()
    {
        var marked = _fmt.SetHighlight(One("hello world"), (0, 5), 0xFFFFFF00);
        Assert.Equal(0xFFFFFF00u, _fmt.ReadCharacter(marked, (0, 5)).Highlight);
        Assert.Null(_fmt.ReadCharacter(marked, (6, 11)).Highlight);
        Assert.Null(_fmt.ReadCharacter(_fmt.ClearDirectFormatting(marked, null), null).Highlight);
    }

    [Fact]
    public void CollapsedRangeReadsTheRunBeforeTheCaret()
    {
        var text = _fmt.SetChar(One("hello world"), (0, 5), CharAttribute.Bold, true);
        Assert.True(_fmt.ReadCharacter(text, (3, 3)).Bold);
        Assert.True(_fmt.ReadCharacter(text, (5, 5)).Bold);
        Assert.False(_fmt.ReadCharacter(text, (8, 8)).Bold);
        Assert.True(_fmt.ReadCharacter(text, (0, 0)).Bold);
    }

    [Fact]
    public void ReadParagraphReportsCommonAndMixed()
    {
        var uniform = _fmt.SetAlign(One("hello"), null, TextAlign.Center);
        var read = _fmt.ReadParagraph(uniform, null);
        Assert.Equal(TextAlign.Center, read.Align);

        var text = new RichText(PlaceholderRole.Body,
        [
            new TextParagraph("a") { Align = TextAlign.Left },
            new TextParagraph("b") { Align = TextAlign.Right },
        ]);
        Assert.Null(_fmt.ReadParagraph(text, null).Align);
    }

    [Fact]
    public void LOmbraSuUnaLetteraInMezzoNonSiPerde()
    {
        var back = _fmt.ToggleChar(One("abc"), (1, 2), CharAttribute.Shadow);

        Assert.Equal(3, back.Paragraphs[0].Runs.Count);
        Assert.True(back.Paragraphs[0].Runs[1].Shadow);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void A_font_size_that_is_not_a_number_leaves_the_text_unchanged(double size)
    {
        var text = One("abc");
        Assert.Same(text, _fmt.SetFontSize(text, null, size));
    }

    [Theory]
    [InlineData(double.NaN, 1.0)]
    [InlineData(-4.0, 0.1)]
    [InlineData(1e9, 10.0)]
    public void Line_spacing_is_kept_within_bounds(double requested, double expected)
    {
        var back = _fmt.SetLineSpacing(One("abc"), null, requested);
        Assert.Equal(expected, back.Paragraphs[0].LineSpacing);
    }
}

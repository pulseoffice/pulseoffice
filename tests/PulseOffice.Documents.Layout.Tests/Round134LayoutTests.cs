using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round134LayoutTests
{
    [Fact]
    public void Ideographic_text_breaks_between_characters_after_a_latin_word()
    {
        var f = new LayoutFixture(width: 60);
        f.Add("ab 一二三四五六七八九十");

        Assert.Equal(["ab 一二三四五六七", "八九十"], f.LineTexts(f.Layout()));
    }

    [Fact]
    public void Ideographic_line_never_starts_with_closing_punctuation()
    {
        var f = new LayoutFixture(width: 60);
        f.Add("一二三四五六七八九十。");

        Assert.Equal(["一二三四五六七八九", "十。"], f.LineTexts(f.Layout()));
    }

    [Fact]
    public void Ideographic_line_never_ends_with_opening_bracket()
    {
        var f = new LayoutFixture(width: 60);
        f.Add("一二三四五六七八九「十」");

        Assert.Equal(["一二三四五六七八九", "「十」"], f.LineTexts(f.Layout()));
    }

    [Fact]
    public void Ideographic_lines_are_not_emergency_breaks()
    {
        var f = new LayoutFixture(width: 60);
        f.Add("一二三四五六七八九十一二三");

        Assert.All(f.Lines(f.Layout()), line => Assert.False(line.Line.Broken));
    }

    [Fact]
    public void Justified_ideographic_line_without_spaces_reaches_the_right_margin()
    {
        var f = new LayoutFixture(width: 63);
        f.Add("一二三四五六七八九十一二三", p => p with { Align = TextAlign.Justify, Explicit = ParagraphOverrides.Align });

        var first = f.Lines(f.Layout())[0];
        var right = first.Line.Runs.Where(run => run.Kind == GlyphRunKind.Text).Max(run => run.X + run.Width);

        Assert.Equal(63, right, 3);
    }

    [Fact]
    public void Keep_with_next_heading_follows_the_table_to_the_next_page()
    {
        var f = new LayoutFixture(width: 60, height: 120);
        f.Add(string.Join(' ', Enumerable.Repeat("aaaa", 16)), p => p with { Widows = 0, Orphans = 0 });
        var title = f.Add("Titolo", p => p with { KeepWithNext = true });
        var cell = new TableCell(new BlockContainer([new ParagraphBlock("c1", "cella")]));
        f.Document.Body.Add(new TableBlock("t", [new TableRow([cell])], [Twip.FromPoints(60)]));

        var layout = f.Layout();

        Assert.True(layout.Pages.Count >= 2);
        Assert.DoesNotContain(layout.Pages[0].Lines, line => line.ParagraphId == title.Id);
        Assert.Equal(title.Id, layout.Pages[1].Lines[0].ParagraphId);
    }
}

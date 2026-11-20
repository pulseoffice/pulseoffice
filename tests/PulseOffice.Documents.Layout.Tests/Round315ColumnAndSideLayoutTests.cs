using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round315ColumnAndSideLayoutTests
{
    [Fact]
    public void The_column_separator_follows_its_line_height_and_alignment()
    {
        var f = new LayoutFixture(width: 200, height: 400);
        var line = new ColumnSeparatorLine(BorderLineStyle.Dashed, 2, 0xFF2563EB) { HeightPercent = 50, Alignment = ColumnSeparatorAlignment.Bottom };
        f.Document.Body.Add(new SectionBlock("s1", "Section1", new BlockContainer([.. Enumerable.Range(0, 8).Select(index => new ParagraphBlock("p" + index, "line " + index))]))
        {
            Columns = new SectionColumns(2, new Twip(200)) { Separator = true, SeparatorLine = line },
        });

        var page = f.Layout().Pages[0];
        var rule = Assert.Single(page.Decorations, box => box.Kind == DecorationKind.ColumnSeparator);
        var column = page.Columns.First();

        Assert.Equal(0xFF2563EB, rule.Color);
        Assert.Equal(2, rule.StrokeWidth, 3);
        Assert.Equal(BorderLineStyle.Dashed, rule.LineStyle);
        Assert.Equal(column.Bounds.Height / 2, rule.Bounds.Height, 3);
        Assert.Equal(column.Bounds.Bottom, rule.Bounds.Bottom, 3);
    }

    [Fact]
    public void A_right_page_style_starts_on_an_odd_page_after_a_blank_one()
    {
        var f = new LayoutFixture(width: 200, height: 400);
        var right = f.PageOf("Right", 200, 400);
        f.Document.Styles.Page.Add(right);
        right.Usage = PageUsage.Right;
        right.NextStyleName = LayoutFixture.Page;
        f.Add("one");
        f.Add("two", p => p with { PageStyleName = "Right" });

        var pages = f.Layout().Pages;

        Assert.Equal(3, pages.Count);
        Assert.Empty(pages[1].Lines);
        Assert.Equal("Right", pages[2].StyleName);
    }
}

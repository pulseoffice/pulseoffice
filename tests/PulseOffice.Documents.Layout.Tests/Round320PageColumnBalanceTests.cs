using PulseOffice.Documents.Blocks;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round320PageColumnBalanceTests
{
    private static LayoutFixture Fixture(bool balancedBeforeSection, bool sectionFollows)
    {
        var f = new LayoutFixture(width: 200, height: 400);
        f.Document.Styles.Page.Find(LayoutFixture.Page)!.Columns = new SectionColumns(2, new Twip(200)) { BalancedBeforeSection = balancedBeforeSection };
        for (var index = 0; index < 6; index++) f.Add("line " + index);
        if (sectionFollows)
            f.Document.Body.Add(new SectionBlock("s1", "Section1", new BlockContainer([new ParagraphBlock("after", "after")])) { Columns = SectionColumns.Single });
        return f;
    }

    private static int RightColumnLines(LayoutFixture f) =>
        f.Layout().Pages[0].Lines.Count(line => line.ParagraphId != "after" && line.Left > 100);

    [Fact]
    public void Word_page_columns_balance_before_a_continuous_section()
    {
        Assert.Equal(3, RightColumnLines(Fixture(true, true)));
    }

    [Fact]
    public void Page_columns_from_odf_fill_the_first_column_first()
    {
        Assert.Equal(0, RightColumnLines(Fixture(false, true)));
    }

    [Fact]
    public void Word_page_columns_do_not_balance_at_the_end_of_the_document()
    {
        Assert.Equal(0, RightColumnLines(Fixture(true, false)));
    }

    [Fact]
    public void The_blank_page_before_a_right_page_style_keeps_the_previous_style()
    {
        var f = new LayoutFixture(width: 200, height: 400);
        var right = f.PageOf("Right", 200, 400);
        right.Usage = PulseOffice.Documents.Styles.PageUsage.Right;
        right.NextStyleName = LayoutFixture.Page;
        f.Document.Styles.Page.Add(right);
        f.Add("one");
        f.Add("two", p => p with { PageStyleName = "Right" });

        Assert.Equal(LayoutFixture.Page, f.Layout().Pages[1].StyleName);
    }
}

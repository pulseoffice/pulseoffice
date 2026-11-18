using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round198FolioPageQueueTests
{
    [Fact]
    public void Columns_of_different_widths_place_the_second_column_after_the_narrow_first()
    {
        var f = new LayoutFixture(width: 130, height: 400);
        f.Document.Styles.Page.Find(LayoutFixture.Page)!.Columns = new SectionColumns(2, Twip.FromPoints(10))
        {
            Spans = [new ColumnSpan(Twip.FromPoints(30), Twip.FromPoints(10)), new ColumnSpan(Twip.FromPoints(90), Twip.Zero)],
        };
        f.Add("uno");
        var second = f.Add("due", p => p with { ColumnBreakBefore = true });

        var layout = f.Layout();

        Assert.Equal(40, layout.Pages[0].Lines.Single(line => line.ParagraphId == second.Id).Left, 3);
    }

    [Fact]
    public void A_restarted_page_number_keeps_the_physical_side_of_a_mirrored_page()
    {
        var f = new LayoutFixture(width: 200, height: 400);
        var style = f.Document.Styles.Page.Find(LayoutFixture.Page)!;
        style.Margins = new Margins(Twip.FromPoints(20), Twip.Zero, Twip.Zero, Twip.Zero);
        style.Usage = PageUsage.Mirrored;
        f.Add("uno");
        f.Add("due", p => p with { PageStyleName = LayoutFixture.Page, PageNumberRestart = 1 });

        var layout = f.Layout();

        Assert.Equal(2, layout.Pages.Count);
        Assert.Equal(0, layout.Pages[1].Content.X, 3);
    }

    [Fact]
    public void Line_numbers_begin_at_the_start_value_and_restart_with_the_page_style()
    {
        var f = new LayoutFixture(width: 200, height: 400);
        f.Document.Styles.Page.Add(f.PageOf("Other", 200, 400));
        f.Add("uno");
        f.Add("due", p => p with { PageStyleName = "Other" });

        var numbers = new PulseOffice.Documents.Layout.Queries.LineNumbers().Number(f.Layout(), new LineNumbering(1) { RestartEachSection = true, Start = 5 });

        Assert.Equal([5, 5], numbers.Values.Order());
    }

    [Fact]
    public void The_gutter_moves_the_text_away_from_the_binding()
    {
        var f = new LayoutFixture(width: 200, height: 400);
        var style = f.Document.Styles.Page.Find(LayoutFixture.Page)!;
        style.Margins = new Margins(Twip.FromPoints(10), Twip.Zero, Twip.Zero, Twip.Zero);
        style.Gutter = Twip.FromPoints(15);
        f.Add("uno");

        var layout = f.Layout();

        Assert.Equal(25, layout.Pages[0].Content.X, 3);
    }
}

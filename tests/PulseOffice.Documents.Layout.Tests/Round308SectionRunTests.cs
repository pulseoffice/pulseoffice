using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.Layout.Tree;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round308SectionRunTests
{
    private static readonly ColumnLayout Single = new(1, 0, false, false);

    private static PageBox Page(int index, int flowIndex) =>
        new(index, "A", 100, 100, new LayoutRect(0, 0, 100, 100), [], new PageStart(flowIndex, 0, "A"));

    [Fact]
    public void Continuous_sections_inside_one_page_style_count_as_sections()
    {
        var first = new SectionBlock("s1", "One", new BlockContainer());
        var second = new SectionBlock("s2", "Two", new BlockContainer());
        FlowItem[] flow = [new SectionStartItem(first, Single), new SectionEndItem(first), new SectionStartItem(second, Single), new SectionEndItem(second)];

        var sections = new SectionPageRuns().Sections([Page(0, 0), Page(1, 2), Page(2, 3)], flow);

        Assert.Equal([new SectionPages(1, 1), new SectionPages(2, 2), new SectionPages(2, 2)], sections);
    }
}

public sealed class Round308SectionHeaderHeightTests
{
    [Fact]
    public void The_header_height_reserved_before_pagination_uses_the_section_number_not_its_stored_text()
    {
        var f = new LayoutFixture(width: 200, height: 120);
        var style = f.Document.Styles.Page.Find(LayoutFixture.Page)!;
        style.Header.Enabled = true;
        style.Header.Spacing = PulseOffice.Foundation.Units.Twip.Zero;
        var stored = new string('a', 60);
        style.Header.Default.Add(new ParagraphBlock(f.Document.NextId("h"), new PulseOffice.Text.Rich.TextParagraph(string.Empty).WithRuns(
            [new PulseOffice.Text.Rich.TextRun(stored) { Field = PulseOffice.Text.Rich.TextFieldKind.SectionNumber }])));
        f.Add("riga");

        var layout = f.Layout();

        Assert.Single(layout.Pages[0].Header!.Lines);
        Assert.Equal(12, layout.Pages[0].Lines[0].Top, 3);
    }
}

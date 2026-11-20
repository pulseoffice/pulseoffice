using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round305SectionFieldTests
{
    private static PageBox Page(int index, string style) =>
        new(index, style, 100, 100, new LayoutRect(0, 0, 100, 100), [], new PageStart(0, 0, style));

    [Fact]
    public void Runs_of_one_page_style_are_numbered_sections_with_their_page_counts()
    {
        var sections = new SectionPageRuns().Sections([Page(0, "A"), Page(1, "A"), Page(2, "B"), Page(3, "A")]);

        Assert.Equal([new SectionPages(1, 2), new SectionPages(1, 2), new SectionPages(2, 1), new SectionPages(3, 1)], sections);
    }

    [Fact]
    public void Section_fields_show_the_section_number_and_page_count_in_their_numerals()
    {
        TextRun[] runs =
        [
            new("0") { Field = TextFieldKind.SectionNumber },
            new("/"),
            new("0") { Field = TextFieldKind.SectionPageCount, FieldDetail = new FieldDetail { Value = SequenceFormats.LowerRoman } },
        ];

        var resolved = new PageFieldText(new SequenceNumerals()).Resolve(new TextParagraph("0/0") { Runs = runs }, "1", "9", 1, new SectionPages(2, 4));

        Assert.Equal("2/iv", resolved.Text);
    }
}

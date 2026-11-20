using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round326FindReplaceTests
{
    private readonly DocumentSearch _search = new();

    private static ParagraphBlock Paragraph(string id, params TextRun[] runs) =>
        new(id, new TextParagraph(string.Concat(runs.Select(run => run.Text))) { Runs = runs });

    [Fact]
    public void Paragraph_marks_are_found_across_paragraphs_and_plain_text_does_not_cross_them()
    {
        ParagraphBlock[] paragraphs = [new("p1", "alfa"), new("p2", "beta")];

        var match = Assert.Single(_search.Matches([new SearchStory(paragraphs)], new SearchQuery("fa^pbe")));

        Assert.Equal(new DocumentRange(new DocumentPosition("p1", 2), new DocumentPosition("p2", 2)), match.Range);
        Assert.Empty(_search.FindAll(paragraphs, new SearchQuery("alfabeta")));
        Assert.Equal("x\ny", _search.Replacement(match, new SearchQuery("fa^pbe"), "x^py"));
    }

    [Fact]
    public void Page_column_and_section_breaks_are_found_where_the_paragraph_starts()
    {
        ParagraphBlock[] paragraphs =
        [
            new("p1", "uno"),
            new("p2", new TextParagraph("due") { PageBreakBefore = true }),
            new("p3", new TextParagraph("tre") { ColumnBreakBefore = true }),
            new("p4", new TextParagraph("quattro") { PageStyleName = "Altro" }),
        ];
        var story = new SearchStory(paragraphs);

        var page = Assert.Single(_search.Matches([story], new SearchQuery("^m")));
        Assert.Equal(DocumentRange.At(new DocumentPosition("p2", 0)), page.Range);
        Assert.Equal([new SearchBreakAt("p2", SearchBreak.Page)], page.Breaks);
        Assert.Equal("p3", Assert.Single(_search.Matches([story], new SearchQuery("^n"))).Range.Start.ParagraphId);
        Assert.Equal("p4", Assert.Single(_search.Matches([story], new SearchQuery("^b"))).Range.Start.ParagraphId);
        Assert.Equal(new DocumentPosition("p1", 3), Assert.Single(_search.Matches([story], new SearchQuery("^p^m"))).Range.Start);
    }

    [Fact]
    public void Hidden_text_is_not_matched()
    {
        var paragraph = Paragraph("p", new TextRun("vedo "), new TextRun("segreto") { Hidden = true }, new TextRun(" fine"));

        Assert.Empty(_search.FindAll([paragraph], new SearchQuery("segreto")));
        Assert.Equal(DocumentRange.In("p", 0, 17), Assert.Single(_search.FindAll([paragraph], new SearchQuery("vedo  fine"))));
    }

    [Fact]
    public void Word_wildcards_are_translated()
    {
        var paragraph = new ParagraphBlock("p", "Rossi, Mario; bello buono aab");
        var swap = new SearchQuery("(<[A-Z][a-z]@>), (<[A-Z]*>)") { Wildcards = true };

        var match = Assert.Single(_search.Matches([new SearchStory([paragraph])], swap));
        Assert.Equal("Mario Rossi", _search.Replacement(match, swap, "\\2 \\1"));
        Assert.Equal(2, _search.FindAll([paragraph], new SearchQuery("<b[a-z]@o>") { Wildcards = true }).Count);
        Assert.Equal(DocumentRange.In("p", 26, 28), Assert.Single(_search.FindAll([paragraph], new SearchQuery("a{2}") { Wildcards = true })));
        Assert.Equal(2, _search.FindAll([paragraph], new SearchQuery("[!a-z ,;]") { Wildcards = true }).Count);
        Assert.Empty(_search.FindAll([paragraph], new SearchQuery("rossi") { Wildcards = true }));
        Assert.False(_search.IsValid(new SearchQuery("(<a") { Wildcards = true }));
    }

    [Fact]
    public void Formats_and_styles_narrow_the_search_and_alone_find_formatted_spans()
    {
        var bold = Paragraph("p1", new TextRun("alfa "), new TextRun("alfa") { Bold = true }, new TextRun(" beta"));
        var titled = new ParagraphBlock("p2", new TextParagraph("alfa") { StyleName = "Titolo" });
        var story = new SearchStory([bold, titled]);

        Assert.Equal(DocumentRange.In("p1", 5, 9), Assert.Single(_search.Matches([story], new SearchQuery("alfa") { Format = new SearchFormat(Bold: true) })).Range);
        Assert.Equal("p2", Assert.Single(_search.Matches([story], new SearchQuery("alfa") { Format = new SearchFormat("Titolo") })).Range.Start.ParagraphId);
        Assert.Equal(DocumentRange.In("p1", 5, 9), Assert.Single(_search.Matches([story], new SearchQuery(string.Empty) { Format = new SearchFormat(Bold: true) })).Range);
    }

    [Fact]
    public void Comments_are_searched_last_and_headers_of_unused_page_styles_are_not()
    {
        var document = new TextDocument();
        document.Body.Replace([new ParagraphBlock("b", "alfa")]);
        var used = document.Styles.Page.Find(document.InitialPageStyle) ?? Added(document, document.InitialPageStyle);
        used.Header.Enabled = true;
        used.Header.Default.Replace([new ParagraphBlock("h", "alfa")]);
        used.Header.First = new BlockContainer([new ParagraphBlock("hf", "alfa")]);
        var unused = Added(document, "Unused");
        unused.Header.Enabled = true;
        unused.Header.Default.Replace([new ParagraphBlock("u", "alfa")]);
        document.Comments.Add(new Comment("c1", "Anna", DateTimeOffset.UnixEpoch, new BlockContainer([new ParagraphBlock("c", "alfa")])));
        var stories = new SearchStories(new StoryCatalog(new BlockTree()), new BlockTree()).From(document, "b");

        var found = _search.Matches(stories, new SearchQuery("alfa"));

        Assert.Equal(["b", "h", "c"], found.Select(match => match.Range.Start.ParagraphId));
        Assert.Equal("c1", found[^1].CommentId);
    }

    private static PageStyle Added(TextDocument document, string name)
    {
        var style = new PageStyle(name);
        document.Styles.Page.Add(style);
        return style;
    }
}

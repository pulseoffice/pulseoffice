using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round331FindReplaceTests
{
    private readonly DocumentSearch _search = new();

    [Fact]
    public void Bold_inherited_from_a_paragraph_style_is_matched()
    {
        var styles = new StyleSheet();
        styles.Paragraph.Add(new ParagraphStyle("Loud") { Character = new CharacterProperties { Bold = true } });
        ParagraphBlock[] paragraphs = [new("p1", "alfa"), new("p2", "alfa", "Loud")];
        var story = new SearchStory(paragraphs) { Styles = styles };

        var bold = Assert.Single(_search.Matches([story], new SearchQuery("alfa") { Format = new SearchFormat(Bold: true) }));
        var plain = Assert.Single(_search.Matches([story], new SearchQuery("alfa") { Format = new SearchFormat(Bold: false) }));

        Assert.Equal("p2", bold.Range.Start.ParagraphId);
        Assert.Equal("p1", plain.Range.Start.ParagraphId);
    }

    [Fact]
    public void Section_block_boundaries_are_section_break_matches()
    {
        var document = new TextDocument();
        var section = new SectionBlock("s", "Section1", new BlockContainer([new ParagraphBlock("in", "dentro")]));
        document.Body.Replace([new ParagraphBlock("before", "prima"), section, new ParagraphBlock("after", "dopo")]);
        var stories = new SearchStories(new StoryCatalog(new BlockTree()), new BlockTree()).From(document, "before");

        var breaks = _search.Matches(stories, new SearchQuery("^b"));

        Assert.Equal(["in", "after"], breaks.Select(match => match.Range.Start.ParagraphId));
    }

    [Fact]
    public void Section_break_code_is_accepted_in_the_replacement()
    {
        var paragraph = new ParagraphBlock("p", "uno due");
        var query = new SearchQuery(" ");
        var match = Assert.Single(_search.Matches([new SearchStory([paragraph])], query));

        Assert.Equal("\u001D", _search.Replacement(match, query, "^b"));
    }
}

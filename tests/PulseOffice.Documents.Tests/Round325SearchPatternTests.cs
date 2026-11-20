using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round325SearchPatternTests
{
    private readonly DocumentSearch _search = new();

    [Fact]
    public void A_regular_expression_finds_and_its_groups_reach_the_replacement()
    {
        var paragraph = new ParagraphBlock("p", "Rossi, Mario e Bianchi, Anna");
        var query = new SearchQuery(@"(\w+), (\w+)", Regex: true);

        var found = _search.FindAll([paragraph], query);

        Assert.Equal(2, found.Count);
        Assert.Equal("Mario Rossi", _search.Replacement(paragraph, found[0], query, "$2 $1"));
    }

    [Fact]
    public void Caret_codes_find_tabs_and_line_breaks_and_insert_them()
    {
        var paragraph = new ParagraphBlock("p", "a\tb\u2028c");
        var query = new SearchQuery("^t");

        var found = Assert.Single(_search.FindAll([paragraph], query));

        Assert.Equal(DocumentRange.In("p", 1, 2), found);
        Assert.Single(_search.FindAll([paragraph], new SearchQuery("^l")));
        Assert.Equal("[\t]\u2028", _search.Replacement(paragraph, found, query, "[^&]^l"));
    }

    [Fact]
    public void Caret_wildcards_match_digits_and_any_character()
    {
        var paragraph = new ParagraphBlock("p", "a1 b2 cc");

        Assert.Equal(2, _search.FindAll([paragraph], new SearchQuery("^$^#")).Count);
        Assert.Equal(2, _search.FindAll([paragraph], new SearchQuery("^?^? ")).Count);
    }

    [Fact]
    public void An_invalid_regular_expression_finds_nothing_and_is_reported()
    {
        var query = new SearchQuery("(", Regex: true);

        Assert.False(_search.IsValid(query));
        Assert.Empty(_search.FindAll([new ParagraphBlock("p", "(")], query));
        Assert.True(_search.IsValid(new SearchQuery("(")));
    }

    [Fact]
    public void Whole_words_also_filter_regular_expression_matches()
    {
        var paragraph = new ParagraphBlock("p", "casa casale");

        Assert.Equal(DocumentRange.In("p", 0, 4), Assert.Single(_search.FindAll([paragraph], new SearchQuery("cas[a-z]", WholeWords: true, Regex: true))));
    }
}

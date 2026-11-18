using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class Round236AnnotatedSpanMappingTests
{
    private readonly IAnnotatedSpanMapping _mapping = new AnnotatedSpanMapping();

    [Fact]
    public void Equal_text_keeps_the_span() =>
        Assert.Equal(new CommentTextSpan(1, 2), _mapping.Shown("abcd", "abcd", new CommentTextSpan(1, 2)));

    [Fact]
    public void Text_inside_a_literal_format_moves_by_the_prefix() =>
        Assert.Equal(new CommentTextSpan(7, 3), _mapping.Shown("north", "Zone: north", new CommentTextSpan(1, 3)));

    [Fact]
    public void A_formatted_number_maps_through_its_separators() =>
        Assert.Equal(new CommentTextSpan(2, 2), _mapping.Shown("1234.5", "1,234.50", new CommentTextSpan(1, 2)));

    [Fact]
    public void A_span_whose_characters_are_not_shown_has_no_highlight() =>
        Assert.Null(_mapping.Shown("0.25", "25%", new CommentTextSpan(0, 1)));
}

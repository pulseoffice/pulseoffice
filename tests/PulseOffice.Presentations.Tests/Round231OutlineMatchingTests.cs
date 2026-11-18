using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class Round231OutlineMatchingTests
{
    private readonly IDeckOutlineText _outline = new DeckOutlineText();
    private readonly IOutlineSlideMatching _matching = new OutlineSlideMatching();

    private const string Original = "One\n\ta\nTwo\n\tb\nThree\n\tc";

    [Fact]
    public void Deleting_a_title_line_merges_its_body_into_the_slide_before_and_drops_that_slide()
    {
        var matches = _matching.Match(_outline.Parse(Original), _outline.Parse("One\n\ta\n\tb\nThree\n\tc"));

        Assert.Equal(new int?[] { 0, 2 }, matches);
    }

    [Fact]
    public void Moved_title_blocks_keep_their_slides_in_the_new_order()
    {
        var matches = _matching.Match(_outline.Parse(Original), _outline.Parse("Three\n\tc\nOne\n\ta\nTwo\n\tb"));

        Assert.Equal(new int?[] { 2, 0, 1 }, matches);
    }

    [Fact]
    public void An_edited_title_keeps_its_slide_and_a_new_line_becomes_a_new_slide()
    {
        var matches = _matching.Match(_outline.Parse(Original), _outline.Parse("One\n\ta\nTwo renamed\n\tb\nFour\nThree\n\tc"));

        Assert.Equal(new int?[] { 0, 1, null, 2 }, matches);
    }

    [Fact]
    public void Deleting_every_line_of_a_slide_drops_it()
    {
        var matches = _matching.Match(_outline.Parse(Original), _outline.Parse("One\n\ta\nThree\n\tc"));

        Assert.Equal(new int?[] { 0, 2 }, matches);
    }

    [Fact]
    public void Alt_shift_up_swaps_the_caret_block_with_the_one_above()
    {
        var (text, line) = new OutlineBlockMove(_outline).Move(Original, 3, up: true);

        Assert.Equal("Two\n\tb\nOne\n\ta\nThree\n\tc", text);
        Assert.Equal(1, line);
    }

    [Fact]
    public void Alt_shift_down_on_the_last_block_changes_nothing()
    {
        var (text, line) = new OutlineBlockMove(_outline).Move(Original, 4, up: false);

        Assert.Equal(Original, text);
        Assert.Equal(4, line);
    }
}

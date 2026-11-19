using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class Round249OutlineBlockTransferTests
{
    private const string Text = "Same\n\ta\nOther\nSame\n\ta";
    private readonly IDeckOutlineText _outline = new DeckOutlineText();
    private readonly IOutlineBlockTransfer _transfer = new OutlineBlockTransfer(new DeckOutlineText());

    [Fact]
    public void A_selection_of_whole_blocks_is_a_block_range()
    {
        Assert.Equal(new OutlineBlockRange(0, 1), _transfer.Covered(Text, 0, 8));
        Assert.Equal(new OutlineBlockRange(0, 1), _transfer.Covered(Text, 0, 7));
        Assert.Equal(new OutlineBlockRange(1, 2), _transfer.Covered(Text, 8, Text.Length - 8));
        Assert.Null(_transfer.Covered(Text, 1, 6));
        Assert.Null(_transfer.Covered(Text, 0, 3));
    }

    [Fact]
    public void Pasted_blocks_are_found_at_their_block_position()
    {
        var after = _outline.Parse("Other\nSame\n\ta");
        var inserted = _outline.Parse("Same\n\ta");

        Assert.Equal(1, _transfer.InsertedAt(after, _outline.Parse("Other\nSame\n\ta\nSame\n\ta"), inserted));
        Assert.Null(_transfer.InsertedAt(after, _outline.Parse("Other\nSame\n\tb\nSame\n\ta"), inserted));
    }
}

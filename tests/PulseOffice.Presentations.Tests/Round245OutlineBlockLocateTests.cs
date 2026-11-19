using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class Round245OutlineBlockLocateTests
{
    private readonly IOutlineBlockMove _move = new OutlineBlockMove(new DeckOutlineText());

    [Fact]
    public void The_caret_line_is_located_in_its_title_block_so_identical_blocks_can_follow_a_move()
    {
        const string twins = "Same\n\tx\nSame\n\tx\nOther";

        Assert.Equal((0, 3), _move.Locate(twins, 1));
        Assert.Equal((1, 3), _move.Locate(twins, 2));
        Assert.Equal((2, 3), _move.Locate(twins, 4));
    }
}

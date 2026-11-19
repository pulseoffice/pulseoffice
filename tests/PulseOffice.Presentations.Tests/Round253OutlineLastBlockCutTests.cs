using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class Round253OutlineLastBlockCutTests
{
    private readonly IDeckOutlineText _outline = new DeckOutlineText();
    private readonly IOutlineBlockTransfer _transfer = new OutlineBlockTransfer(new DeckOutlineText());

    [Theory]
    [InlineData("First\nLast\n\ta", 6, 5)]
    [InlineData("First\r\nLast\r\n\ta", 7, 5)]
    public void Cutting_the_last_block_also_removes_the_line_break_before_it(string text, int start, int removal)
    {
        var from = _transfer.RemovalStart(text, start, text.Length - start);

        Assert.Equal(removal, from);
        Assert.Single(_outline.Parse(text[..from]));
    }

    [Fact]
    public void Cutting_a_block_that_is_not_last_keeps_its_start()
    {
        Assert.Equal(0, _transfer.RemovalStart("First\nLast", 0, 6));
        Assert.Equal(0, _transfer.RemovalStart("Only", 0, 4));
    }
}

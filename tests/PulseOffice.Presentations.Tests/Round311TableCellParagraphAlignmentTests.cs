using PulseOffice.Document.Embedding;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class Round311TableCellParagraphAlignmentTests
{
    private static TableCellText Aligned() => new(0, 0,
    [
        new TableCellParagraph([new TableTextRun("ab")]),
        new TableCellParagraph([new TableTextRun("cd")]) { Alignment = CellHorizontalAlign.Right },
    ]);

    [Fact]
    public void An_aligned_paragraph_makes_the_cell_text_rich()
    {
        Assert.False(Aligned().IsPlain);
        Assert.NotEqual(Aligned(), Aligned() with { Paragraphs = [.. Aligned().Paragraphs.Select(paragraph => paragraph with { Alignment = null })] });
    }

    [Fact]
    public void Typing_keeps_each_paragraph_alignment()
    {
        var rebased = Aligned().Rebased("abX\ncd")!;

        Assert.Null(rebased.Paragraphs[0].Alignment);
        Assert.Equal(CellHorizontalAlign.Right, rebased.Paragraphs[1].Alignment);
    }

    [Fact]
    public void The_paragraph_alignment_overrides_the_cell_look_and_matching_ones_are_dropped()
    {
        var look = TableCellLook.Plain with { Horizontal = CellHorizontalAlign.Right };

        Assert.Equal(CellHorizontalAlign.Right, Aligned().Paragraphs[0].AlignedOver(look));
        var beyond = Aligned().AlignedBeyond(look);
        Assert.Equal(CellHorizontalAlign.Left, beyond.Paragraphs[0].Alignment);
        Assert.Null(beyond.Paragraphs[1].Alignment);
    }
}

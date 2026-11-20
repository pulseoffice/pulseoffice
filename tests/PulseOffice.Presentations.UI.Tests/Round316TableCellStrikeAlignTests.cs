using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round316TableCellStrikeAlignTests
{
    private readonly IDeckTableCellShapes _shapes = new DeckTableCellShapes();
    private readonly IShapeRichText _rich = new ShapeRichText();

    private static readonly TableCellShapeLook Look = new(TableCellLook.Plain, CellVerticalAlign.Top, 14);

    private static TableCellText Cell() => new(0, 0,
    [
        new TableCellParagraph([new TableTextRun("ab")]),
        new TableCellParagraph([new TableTextRun("cd")]) { Alignment = CellHorizontalAlign.Right },
    ]);

    [Fact]
    public void Strikethrough_can_be_toggled_on_part_of_a_cell_while_editing()
    {
        var shape = _shapes.Shape("t#cell", Cell(), Look);
        var rich = _rich.ToRich(shape);
        var struck = rich with
        {
            Paragraphs = [rich.Paragraphs[0] with { Runs = [.. rich.Paragraphs[0].Runs.Select(run => run with { Strikethrough = true })] }, rich.Paragraphs[1]],
        };

        var cell = _shapes.Cell(_rich.FromRich(shape, struck), Cell(), Look);

        Assert.True(cell.Paragraphs[0].Runs.Single().Strikethrough);
        Assert.Null(cell.Paragraphs[1].Runs.Single().Strikethrough);
    }

    [Fact]
    public void Each_paragraph_is_composed_with_its_own_alignment_and_one_paragraph_can_be_realigned()
    {
        var shape = _shapes.Shape("t#cell", Cell(), Look);
        var rich = _rich.ToRich(shape);
        Assert.Equal([TextAlign.Left, TextAlign.Right], rich.Paragraphs.Select(paragraph => paragraph.Align));

        var centred = rich with { Paragraphs = [rich.Paragraphs[0] with { Align = TextAlign.Center }, rich.Paragraphs[1]] };
        var cell = _shapes.Cell(_rich.FromRich(shape, centred), Cell(), Look);

        Assert.Equal(CellHorizontalAlign.Center, cell.Paragraphs[0].Alignment);
        Assert.Equal(CellHorizontalAlign.Right, cell.Paragraphs[1].Alignment);
    }

    [Theory]
    [InlineData(CellTextDirection.Down, TextFlow.Vertical)]
    [InlineData(CellTextDirection.Up, TextFlow.Vertical270)]
    [InlineData(CellTextDirection.Stacked, TextFlow.Stacked)]
    [InlineData(CellTextDirection.Horizontal, TextFlow.Horizontal)]
    public void Turned_and_stacked_cells_are_edited_in_their_own_flow(CellTextDirection direction, TextFlow flow)
    {
        var shape = _shapes.Shape("t#cell", Cell(), Look with { Direction = direction });

        Assert.Equal(flow, shape.Style.Text.Layout.Flow);
    }
}

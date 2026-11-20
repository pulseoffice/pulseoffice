using PulseOffice.Document.Embedding;
using PulseOffice.Document.Tables;
using PulseOffice.Drawing.Shapes;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round311TableCellShapeTextTests
{
    private readonly IDeckTableCellShapes _shapes = new DeckTableCellShapes();
    private readonly IShapeTextEditing _editing = new ShapeTextEditing();
    private readonly IDeckTableKeys _keys = new DeckTableKeys(new TableStructure(new TableSpans(), new ColumnWidths()));

    private static readonly TableCellShapeLook Look = new(TableCellLook.Plain, CellVerticalAlign.Top, 14);

    private static TableCellText Rich() => new(0, 0,
    [
        new TableCellParagraph([new TableTextRun("ab") { Bold = true }, new TableTextRun("cd") { Strikethrough = true }]),
        new TableCellParagraph([new TableTextRun("ef")]) { Alignment = CellHorizontalAlign.Right },
    ]);

    [Fact]
    public void A_cell_crosses_the_shape_text_editor_unchanged()
    {
        var shape = _shapes.Shape("t#cell", Rich(), Look);

        Assert.Equal("abcd\nef", _editing.PlainText(shape));
        Assert.Equal(Rich(), _shapes.Cell(shape, Rich(), Look));
    }

    [Fact]
    public void Part_of_a_cell_can_be_formatted_and_strikes_and_alignment_follow_the_text()
    {
        var shape = _shapes.Shape("t#cell", Rich(), Look);
        var italic = _editing.Format(_editing.WithText(shape, "abXcd\nef"), 6, 8, run => run with { Italic = true });

        var cell = _shapes.Cell(italic, Rich(), Look);

        Assert.Equal("abXcd\nef", cell.PlainText);
        Assert.True(cell.Paragraphs[0].Runs[0].Bold);
        Assert.Equal("abX", cell.Paragraphs[0].Runs[0].Text);
        Assert.True(cell.Paragraphs[0].Runs.Single(run => run.Text == "cd").Strikethrough);
        Assert.True(cell.Paragraphs[1].Runs.Single().Italic);
        Assert.Equal(CellHorizontalAlign.Right, cell.Paragraphs[1].Alignment);
    }

    [Fact]
    public void A_line_break_inside_a_paragraph_stays_a_break()
    {
        var withBreak = new TableCellText(0, 0, [new TableCellParagraph([new TableTextRun("a\nb") { Italic = true }])]);

        var shape = _shapes.Shape("t#cell", withBreak, Look);

        Assert.Contains(ShapeTextRun.LineBreak, _editing.PlainText(shape));
        Assert.Equal(withBreak, _shapes.Cell(shape, withBreak, Look));
    }

    [Fact]
    public void Writing_rich_text_replaces_the_cell_runs_and_value()
    {
        var table = new DataTableObject
        {
            Id = "t",
            Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
            Snapshot = new DataSnapshot { Columns = [new DataColumn("a", DataValueKind.Text)], Rows = [["x"]] },
        };

        var written = _keys.WriteRich(table, new TableCellRange(0, 0, 0, 0), Rich().PlainText, Rich());

        Assert.Equal("abcd\nef", written.Snapshot.Rows[0][0]);
        Assert.Equal(Rich(), Assert.Single(written.CellTexts));
        Assert.Empty(_keys.WriteRich(written, new TableCellRange(0, 0, 0, 0), "plain", null).CellTexts);
    }
}

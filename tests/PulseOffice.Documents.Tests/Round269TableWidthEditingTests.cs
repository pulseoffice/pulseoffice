using PulseOffice.Document.Commands;
using PulseOffice.Document.Tables;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing.Tables;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round269TableWidthEditingTests
{
    private readonly TableGeometryEditing _geometry = new(new ColumnWidths());

    private static (TextDocument Document, UndoStack History, TableBlock Table) Percent()
    {
        var document = new TextDocument();
        var table = new TableBlock("t", [new TableRow([new TableCell(new BlockContainer([new ParagraphBlock("a", "")])), new TableCell(new BlockContainer([new ParagraphBlock("b", "")]))])],
            [Twip.FromPoints(100), Twip.FromPoints(100)]) { WidthPercent = 50 };
        document.Body.Add(table);
        return (document, new UndoStack(), table);
    }

    [Fact]
    public void Widening_the_last_column_of_a_percent_table_widens_its_percent()
    {
        var (document, history, table) = Percent();

        _geometry.ResizeColumn(document, history, table, 1, 200);

        Assert.Equal(75, table.WidthPercent!.Value, 3);
    }

    [Fact]
    public void The_width_choice_switches_between_fixed_percent_and_automatic()
    {
        var (document, history, table) = Percent();

        _geometry.SetTableWidth(document, history, table, new TableWidthChoice(TableWidthMode.Fixed, 400, 0));
        Assert.Null(table.WidthPercent);
        Assert.Equal(400, table.Columns.Sum(column => column.ToPoints()), 0);

        _geometry.SetTableWidth(document, history, table, new TableWidthChoice(TableWidthMode.Automatic, 0, 0));
        Assert.True(table.AutoWidth);

        _geometry.SetTableWidth(document, history, table, new TableWidthChoice(TableWidthMode.Percent, 0, 80));
        Assert.Equal(80, table.WidthPercent);
        Assert.False(table.AutoWidth);
    }

    [Fact]
    public void A_drag_on_a_scaled_percent_table_keeps_the_edge_under_the_pointer()
    {
        var (document, history, table) = Percent();
        var displayed = 400.0;

        var delta = _geometry.StoredColumnDelta(table, 40, displayed);
        _geometry.ResizeColumn(document, history, table, 1, 100 + delta);

        var shown = table.Columns[1].ToPoints() * displayed * (table.WidthPercent!.Value / 50) / table.Columns.Sum(column => column.ToPoints());
        Assert.Equal(20, delta, 3);
        Assert.Equal(240, shown, 1);
    }
}

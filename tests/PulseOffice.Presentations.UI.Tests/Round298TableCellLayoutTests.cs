using PulseOffice.Document.Embedding;
using PulseOffice.Document.Tables;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round298TableCellLayoutTests
{
    private readonly IDeckTableCellLayout _layout = new DeckTableCellLayout();

    private static DataTableObject Table() => new()
    {
        Id = "t",
        Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
        Snapshot = new DataSnapshot { Columns = [new DataColumn("a", DataValueKind.Text), new DataColumn("b", DataValueKind.Text)], Rows = [["1", "2"]] },
        Style = new DataTableStyle { HeaderRow = false },
        CellFormats = [new TableCellFormat(0, 1, CellVerticalAlign.Bottom)],
    };

    [Fact]
    public void Layout_reaches_only_the_selected_cells_and_keeps_their_alignment()
    {
        var chosen = new DeckCellLayout(new TableCellMargins(1, 2, 3, 4), CellTextDirection.Up);

        var laid = _layout.Apply(Table(), new TableCellRange(0, 1, 0, 1), chosen);

        var format = Assert.Single(laid.CellFormats);
        Assert.Equal(CellVerticalAlign.Bottom, format.VerticalAlign);
        Assert.Equal(chosen, _layout.Of(laid, new TableCellRange(0, 1, 0, 1)));
        Assert.Equal(CellTextDirection.Horizontal, _layout.Of(laid, new TableCellRange(0, 0, 0, 0)).Direction);
        Assert.Equal(2, _layout.Apply(Table(), null, chosen).CellFormats.Count(cell => cell.Direction == CellTextDirection.Up));
    }
}

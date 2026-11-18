using PulseOffice.Document.Embedding;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round173TableStyleOptionsTests
{
    private readonly DeckTableStyleOptions _options = new();

    private DataTableObject Table() => new()
    {
        Id = "t",
        Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
        Snapshot = new DataSnapshot
        {
            Columns = [new DataColumn("a", DataValueKind.Text), new DataColumn("b", DataValueKind.Text)],
            Rows = [["1", "2"], ["3", "4"]],
        },
        Style = new DataTableStyle { HeaderRow = true, BandedRows = false, OwnLook = true },
        CellFormats =
        [
            new TableCellFormat(0, 0, CellVerticalAlign.Top) { Look = new TableCellLook() },
            new TableCellFormat(0, 1, CellVerticalAlign.Top) { Look = new TableCellLook() },
            new TableCellFormat(1, 0, CellVerticalAlign.Top) { Look = new TableCellLook() },
            new TableCellFormat(1, 1, CellVerticalAlign.Top) { Look = new TableCellLook() },
        ],
    };

    private TableCellLook At(DataTableObject table, int row, int column) =>
        table.CellFormats.Single(format => format.Row == row && format.Column == column).Look!;

    [Fact]
    public void First_column_emphasis_sets_the_flag_and_bolds_only_that_column_and_clearing_it_restores()
    {
        var on = _options.Apply(Table(), new DeckTableEmphasis(true, false, false, false));

        Assert.True(on.Style.FirstColumn);
        Assert.True(At(on, 0, 0).Bold);
        Assert.False(At(on, 0, 1).Bold);

        var off = _options.Apply(on, new DeckTableEmphasis(false, false, false, false));
        Assert.False(off.Style.FirstColumn);
        Assert.False(At(off, 1, 0).Bold);
    }

    [Fact]
    public void Banded_columns_fill_the_odd_columns()
    {
        var banded = _options.Apply(Table(), new DeckTableEmphasis(false, false, false, true));

        Assert.True(banded.Style.BandedColumns);
        Assert.Null(At(banded, 0, 0).FillHex);
        Assert.NotNull(At(banded, 0, 1).FillHex);
    }
}

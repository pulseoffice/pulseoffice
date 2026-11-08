using PulseOffice.Document.Borders;
using PulseOffice.Document.Embedding;
using PulseOffice.Document.Tables;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class DeckTableBordersTests
{
    private readonly DeckTableBorders _borders = new(new BorderFrameResolver(), new DeckTableLookSeed());
    private readonly BorderStroke _red = new(BorderDash.Solid, 2, "#DC2626");

    private DataTableObject Table(bool ownLook) => new()
    {
        Id = "t1",
        Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
        Snapshot = new DataSnapshot
        {
            Columns = [new DataColumn("A", DataValueKind.Text), new DataColumn("B", DataValueKind.Text)],
            Rows = [["1", "2"], ["3", "4"], ["5", "6"]],
        },
        Style = new DataTableStyle { HeaderRow = true, OwnLook = ownLook },
    };

    [Fact]
    public void SenzaSelezioneSiPrendeTuttaLaTabellaConLIntestazione()
    {
        var target = _borders.Target(Table(true), null);

        Assert.Equal(new DeckTableBorderTarget(TableCellFormat.HeaderRow, 0, 2, 1), target);
        Assert.Equal([BorderDash.Solid], _borders.Scope(target).Dashes);
        Assert.False(_borders.Scope(target).Diagonals);
    }

    [Fact]
    public void IlContornoSiScriveSulleCelleDelBordo()
    {
        var table = Table(true);
        var target = _borders.Target(table, new TableCellRange(0, 0, 1, 1));
        var after = BorderFrame.Empty.With(BorderPosition.Bottom, _red).With(BorderPosition.InsideVertical, _red);

        var edited = _borders.Apply(table, target, BorderFrame.Empty, after);

        var line = new TableBorderLine("#DC2626", 2);
        TableCellLook? Look(int row, int column) => edited.CellFormats.Single(format => format.Row == row && format.Column == column).Look;
        Assert.Equal(line, Look(1, 0)!.Bottom);
        Assert.Equal(line, Look(1, 1)!.Bottom);
        Assert.Null(Look(0, 0)!.Bottom);
        Assert.Equal(line, Look(0, 0)!.Right);
        Assert.Equal(line, Look(0, 1)!.Left);
        Assert.Equal(after, _borders.FrameOf(edited, target));
    }

    [Fact]
    public void LaTabellaConLoStileGenericoPrendeUnAspettoProprio()
    {
        var table = Table(false);
        var target = _borders.Target(table, null);

        var edited = _borders.Apply(table, target, _borders.FrameOf(table, target), BorderFrame.Empty);

        Assert.True(edited.Style.OwnLook);
        Assert.Equal(8, edited.CellFormats.Count);
        Assert.True(edited.CellFormats.Single(format => format.Row == TableCellFormat.HeaderRow && format.Column == 0).Look!.Bold);
        Assert.All(edited.CellFormats, format => Assert.Null(format.Look!.Top));
    }
}

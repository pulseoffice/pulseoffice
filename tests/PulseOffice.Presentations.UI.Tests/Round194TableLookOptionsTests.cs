using PulseOffice.Document.Embedding;
using PulseOffice.Document.Tables;
using PulseOffice.UI.Controls.Tables;
using PulseOffice.UI.Controls.Tables.Insert;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round194TableLookOptionsTests
{
    private readonly DeckTableStyleOptions _options = new();

    [Fact]
    public void The_insert_choice_carries_all_five_banding_options()
    {
        var designer = new TableInsertDesigner(new TableStylePresets().All, new TableInsertRequest("command.deck.insert.table.title"));
        var banding = new TableBanding { BandedRows = false, BandedColumns = true, FirstColumn = true, LastColumn = true, LastRow = true };

        designer.Banding = banding;

        Assert.Equal(banding, designer.Choice().Banding);
        Assert.False(designer.Choice().BandedRows);
    }

    [Fact]
    public void Deck_style_options_set_heading_and_banded_rows_too()
    {
        var table = new DataTableObject
        {
            Id = "t",
            Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
            Snapshot = new DataSnapshot { Columns = [new DataColumn("a", DataValueKind.Text)], Rows = [["1"], ["2"]] },
            Style = new DataTableStyle { HeaderRow = true, BandedRows = false, OwnLook = true },
            CellFormats =
            [
                new TableCellFormat(0, 0, CellVerticalAlign.Top) { Look = new TableCellLook() },
                new TableCellFormat(1, 0, CellVerticalAlign.Top) { Look = new TableCellLook() },
            ],
        };

        var changed = _options.Apply(table, new TableLookOptionsChoice(false, TableBanding.Standard));

        Assert.False(changed.Style.HeaderRow);
        Assert.True(changed.Style.BandedRows);
        Assert.Null(changed.CellFormats[0].Look!.FillHex);
        Assert.NotNull(changed.CellFormats[1].Look!.FillHex);
        Assert.Equal(new TableLookOptionsChoice(false, TableBanding.Standard), _options.Of(changed));
    }
}

using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round297PptxTableCellLayoutTests
{
    [Fact]
    public void Cell_margins_and_text_direction_survive_the_pptx_round_trip()
    {
        var down = new TableCellFormat(0, 0, CellVerticalAlign.Top) { Margins = new TableCellMargins(2, 3, 4, 5), Direction = CellTextDirection.Down };
        var stacked = new TableCellFormat(0, 1, CellVerticalAlign.Center) { Direction = CellTextDirection.Stacked };
        var table = new DataTableObject
        {
            Id = "t",
            Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
            Snapshot = new DataSnapshot { Columns = [new DataColumn("a", DataValueKind.Text), new DataColumn("b", DataValueKind.Text)], Rows = [["1", "2"]] },
            Style = new DataTableStyle { HeaderRow = false },
            CellFormats = [down, stacked],
        };
        var element = new ObjectElement { Id = "t", Bounds = new DocumentRect(new Twip(0), new Twip(0), new Twip(4000), new Twip(1000)), Content = table };
        var mapper = new PptxDeckTableMapper();

        var read = (DataTableObject)mapper.Read(mapper.Build(element, table, 2), "t")!.Content;

        Assert.Contains(down, read.CellFormats);
        Assert.Contains(stacked, read.CellFormats);
    }
}

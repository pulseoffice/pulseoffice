using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round170PptxTableHeaderAlignTests
{
    [Fact]
    public void The_vertical_alignment_of_a_header_cell_survives_the_pptx_round_trip()
    {
        var table = new DataTableObject
        {
            Id = "t",
            Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
            Snapshot = new DataSnapshot
            {
                Columns = [new DataColumn("a", DataValueKind.Text), new DataColumn("b", DataValueKind.Text)],
                Rows = [["1", "2"]],
            },
            Style = new DataTableStyle { HeaderRow = true },
            CellFormats = [new TableCellFormat(TableCellFormat.HeaderRow, 1, CellVerticalAlign.Center), new TableCellFormat(0, 0, CellVerticalAlign.Bottom)],
        };
        var element = new ObjectElement { Id = "t", Bounds = new DocumentRect(new Twip(0), new Twip(0), new Twip(4000), new Twip(1000)), Content = table };
        var mapper = new PptxDeckTableMapper();

        var read = (DataTableObject)mapper.Read(mapper.Build(element, table, 2), "t")!.Content;

        Assert.Contains(new TableCellFormat(TableCellFormat.HeaderRow, 1, CellVerticalAlign.Center), read.CellFormats);
        Assert.Contains(new TableCellFormat(0, 0, CellVerticalAlign.Bottom), read.CellFormats);
    }
}

using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round320PptxTableJustifyTests
{
    [Fact]
    public void A_justified_cell_paragraph_survives_the_round_trip()
    {
        var rich = new TableCellText(0, 0, [new TableCellParagraph([new TableTextRun("wide")]) { Alignment = CellHorizontalAlign.Justify }]);
        var element = new ObjectElement
        {
            Id = "t",
            Bounds = new DocumentRect(new Twip(0), new Twip(0), new Twip(4000), new Twip(1000)),
            Content = new DataTableObject
            {
                Id = "t",
                Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
                Snapshot = new DataSnapshot { Columns = [new DataColumn("a", DataValueKind.Text)], Rows = [["wide"]] },
                CellTexts = [rich],
            },
        };
        var mapper = new PptxDeckTableMapper();

        var back = (DataTableObject)mapper.Read(mapper.Build(element, (DataTableObject)element.Content, 2), "t")!.Content;

        var text = Assert.Single(back.CellTexts, cellText => cellText.Row == 0);
        var look = back.CellFormats.FirstOrDefault(format => format.Row == 0 && format.Column == 0)?.Look ?? TableCellLook.Plain;
        Assert.Equal(CellHorizontalAlign.Justify, Assert.Single(text.Paragraphs).AlignedOver(look));
    }
}

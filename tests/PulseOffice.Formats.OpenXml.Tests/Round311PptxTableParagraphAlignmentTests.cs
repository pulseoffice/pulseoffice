using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round311PptxTableParagraphAlignmentTests
{
    private static readonly TableCellText Rich = new(0, 0,
    [
        new TableCellParagraph([new TableTextRun("left")]),
        new TableCellParagraph([new TableTextRun("centre")]) { Alignment = CellHorizontalAlign.Center },
        new TableCellParagraph([new TableTextRun("right")]) { Alignment = CellHorizontalAlign.Right },
    ]);

    private static ObjectElement Element(bool ownLook) => new()
    {
        Id = "t",
        Bounds = new DocumentRect(new Twip(0), new Twip(0), new Twip(4000), new Twip(1000)),
        Content = new DataTableObject
        {
            Id = "t",
            Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
            Snapshot = new DataSnapshot { Columns = [new DataColumn("a", DataValueKind.Text)], Rows = [["left\ncentre\nright"]] },
            Style = new DataTableStyle { OwnLook = ownLook, Grid = !ownLook },
            CellTexts = [Rich],
        },
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Paragraph_alignment_inside_a_cell_survives_the_round_trip(bool ownLook)
    {
        var mapper = new PptxDeckTableMapper();
        var element = Element(ownLook);

        var frame = mapper.Build(element, (DataTableObject)element.Content, 2);
        var back = (DataTableObject)mapper.Read(frame, "t")!.Content;

        var text = Assert.Single(back.CellTexts, cellText => cellText.Row == 0);
        var look = back.CellFormats.FirstOrDefault(format => format.Row == 0 && format.Column == 0)?.Look ?? TableCellLook.Plain;
        Assert.Equal(
            [CellHorizontalAlign.Left, CellHorizontalAlign.Center, CellHorizontalAlign.Right],
            text.Paragraphs.Select(paragraph => paragraph.AlignedOver(look)));
    }

    [Fact]
    public void Each_paragraph_writes_its_own_algn()
    {
        var element = Element(false);

        var frame = new PptxDeckTableMapper().Build(element, (DataTableObject)element.Content, 2);

        var aligns = frame.Descendants<D.TableRow>().Last().Descendants<D.Paragraph>()
            .Select(paragraph => paragraph.ParagraphProperties?.Alignment?.Value).ToList();
        Assert.Null(aligns[0]);
        Assert.Equal(D.TextAlignmentTypeValues.Center, aligns[1]);
        Assert.Equal(D.TextAlignmentTypeValues.Right, aligns[2]);
    }
}

using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round298PptxGridTableStyleTests
{
    private static ObjectElement Element(DataTableStyle style) => new()
    {
        Id = "t",
        Bounds = new DocumentRect(new Twip(0), new Twip(0), new Twip(4000), new Twip(1000)),
        Content = new DataTableObject
        {
            Id = "t",
            Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
            Snapshot = new DataSnapshot { Columns = [new DataColumn("a", DataValueKind.Text)], Rows = [["1"]] },
            Style = style,
        },
    };

    [Fact]
    public void A_grid_table_names_the_built_in_grid_style_and_reads_back_as_a_grid()
    {
        var mapper = new PptxDeckTableMapper();
        var element = Element(new DataTableStyle());

        var frame = mapper.Build(element, (DataTableObject)element.Content, 2);

        Assert.Equal("{5940675A-B579-460E-94D1-54222C63F5DA}", frame.Descendants<D.TableStyleId>().Single().Text);
        var read = (DataTableObject)mapper.Read(frame, "t")!.Content;
        Assert.True(read.Style.Grid);
        Assert.False(read.Style.OwnLook);
    }

    [Fact]
    public void A_table_with_its_own_look_names_no_style()
    {
        var element = Element(new DataTableStyle { OwnLook = true, Grid = false });

        Assert.Empty(new PptxDeckTableMapper().Build(element, (DataTableObject)element.Content, 2).Descendants<D.TableStyleId>());
    }
}

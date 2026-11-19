using PulseOffice.Document.Embedding;
using PulseOffice.Presentations;
using PulseOffice.Presentations.UI.Rendering;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round247SlideTableLocatorTests
{
    private static readonly DataTableObject Table = new()
    {
        Id = "t",
        Query = new DataQuerySource { ConnectionId = "c", Sql = "s" },
        Snapshot = new DataSnapshot { Columns = [new DataColumn("Zona", DataValueKind.Text), new DataColumn("Totale", DataValueKind.Number)], Rows = [["Nord", 1234.5]] },
        Merges = [new TableMerge(0, 0, 1, 2)],
        Style = new DataTableStyle { FirstColumn = true },
    };

    private static readonly PrintTag Container = new("t", PrintRole.Table);

    [Fact]
    public void Drawn_text_finds_its_cell_by_position_and_a_merged_cell_by_its_origin()
    {
        var segments = new SlideTextSegments().For(Container, new ObjectElement { Id = "t", Content = Table }, new SlideElementPlacement(100, 100, 200, 60, 0.05, 0));

        var locator = segments?.Locator;
        Assert.NotNull(locator);
        var corner = locator.At(110, 105);
        Assert.Equal(PrintRole.TableHeader, corner?.Role);
        Assert.Equal(PrintHeaderScope.Both, corner?.Scope);
        var merged = locator.At(290, 125);
        Assert.Equal(2, merged?.ColumnSpan);
        Assert.Same(segments!.Segments[^1].Tag, merged);
    }

    [Fact]
    public void A_rotated_table_is_read_in_its_own_frame()
    {
        var segments = new SlideTextSegments().For(Container, new ObjectElement { Id = "t", Content = Table }, new SlideElementPlacement(100, 100, 200, 60, 0.05, 180));

        var header = segments!.Locator!.At(290, 155);

        Assert.Equal(PrintHeaderScope.Both, header?.Scope);
    }
}

using PulseOffice.Document.Commands;
using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round297TableFrameTests
{
    [Fact]
    public void Adding_a_row_grows_the_table_frame_by_the_row_height_and_undo_restores_it()
    {
        var deck = new Presentation();
        var slide = new Slide("s1", SlideLayout.Blank);
        deck.AddExisting(slide);
        var table = new DataTableObject
        {
            Id = "t",
            Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
            Snapshot = new DataSnapshot { Columns = [new DataColumn("a", DataValueKind.Text)], Rows = [["1"]] },
            Style = new DataTableStyle { HeaderRow = false, FontSize = 10 },
        };
        slide.Add(new ObjectElement { Id = "e", Content = table, Bounds = new DocumentRect(new Twip(0), new Twip(0), new Twip(2000), new Twip(400)) });
        var history = new UndoStack();
        var actions = new DeckActions(deck, history, new SlideFactory(), new FakeTextCatalog());

        actions.EditTable(slide, "e", current => current with { Snapshot = current.Snapshot with { Rows = [.. current.Snapshot.Rows, ["2"]] } });

        Assert.Equal(400 + (int)Math.Round(TableMetrics.RowHeight(table, 1) * 20), slide.Find("e")!.Bounds.Height.Value);
        history.Undo();
        Assert.Equal(400, slide.Find("e")!.Bounds.Height.Value);
    }
}

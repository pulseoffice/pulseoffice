using PulseOffice.Document.Tables;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Editing.Tables;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round186TableSplitHeaderTests
{
    private readonly Fixture _fixture = new();

    [Fact]
    public void SplittingAHeaderCellIntoRowsKeepsTheNewRowsInTheHeader()
    {
        _fixture.Editor.InsertTable(_fixture.Document, _fixture.History, new DocumentPosition(_fixture.First.Id, 0), 3, 2);
        var table = _fixture.Document.Body.Blocks.OfType<TableBlock>().Single();
        table.HeaderRows = 1;
        var grid = new TableGrid(_fixture.Tree);
        var selections = new TableSelections(new TableEditing(_fixture.Stories, _fixture.Deletion), grid, new TableSpans());
        var paragraph = table.Cell(0, 0).Content.Blocks.OfType<ParagraphBlock>().First();
        var selection = selections.Select(_fixture.Document, new DocumentPosition(paragraph.Id, 0), new DocumentPosition(paragraph.Id, 0))!;

        new TableCellEditing(grid, new TableCellFactory()).Split(_fixture.Document, _fixture.History, selection, 3, true);

        Assert.Equal(5, table.Rows.Count);
        Assert.Equal(3, table.HeaderRows);
        Assert.Equal(3, table.Cell(0, 1).RowSpan);
    }
}

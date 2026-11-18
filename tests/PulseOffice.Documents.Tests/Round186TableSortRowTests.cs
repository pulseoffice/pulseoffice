using System.Globalization;
using PulseOffice.Document.Tables;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Editing.Tables;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round186TableSortRowTests
{
    private readonly Fixture _fixture = new();

    [Fact]
    public void SortingWholeRowsCarriesTheirHeightAndBreakSetting()
    {
        _fixture.Editor.InsertTable(_fixture.Document, _fixture.History, new DocumentPosition(_fixture.First.Id, 0), 2, 1);
        var table = _fixture.Document.Body.Blocks.OfType<TableBlock>().Single();
        var grid = new TableGrid(_fixture.Tree);
        ParagraphBlock Paragraph(int row) => table.Cell(row, 0).Content.Blocks.OfType<ParagraphBlock>().First();
        _fixture.Editor.InsertText(_fixture.Document, _fixture.History, new DocumentPosition(Paragraph(0).Id, 0), "b");
        _fixture.Editor.InsertText(_fixture.Document, _fixture.History, new DocumentPosition(Paragraph(1).Id, 0), "a");
        table.Rows[0].Height = Twip.FromPoints(40);
        table.Rows[0].AllowBreakAcrossPages = false;
        var selections = new TableSelections(new TableEditing(_fixture.Stories, _fixture.Deletion), grid, new TableSpans());
        var selection = selections.Select(_fixture.Document, new DocumentPosition(Paragraph(0).Id, 0), new DocumentPosition(Paragraph(0).Id, 0))!;

        new TableSorting(grid, new TableSorter()).Sort(_fixture.Document, _fixture.History, selection, [new TableSortKey(0)], CultureInfo.InvariantCulture);

        Assert.Equal("a", grid.PlainText(table.Cell(0, 0)));
        Assert.Equal(Twip.FromPoints(40), table.Rows[1].Height);
        Assert.False(table.Rows[1].AllowBreakAcrossPages);
        Assert.True(table.Rows[0].AllowBreakAcrossPages);
    }
}

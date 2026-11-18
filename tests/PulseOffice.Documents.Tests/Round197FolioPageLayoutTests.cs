using PulseOffice.Documents.Blocks;
using PulseOffice.Foundation.Pages;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round197FolioPageLayoutTests
{
    private readonly Fixture _f = new();

    [Fact]
    public void A_table_inserted_on_a_landscape_page_takes_the_landscape_text_width()
    {
        var page = _f.Document.Styles.Page.Find(_f.Document.InitialPageStyle)!;
        page.Orientation = PageOrientation.Landscape;
        _f.Type("Before", "After");
        _f.Editor.InsertTable(_f.Document, _f.History, _f.At(1, 0), 1, 2);

        var table = _f.Document.Body.Blocks.OfType<TableBlock>().Single();
        var text = page.Width.Value - page.Margins.Left.Value - page.Margins.Right.Value;
        Assert.InRange(table.Columns.Sum(column => column.Value), text - 2, text);
    }
}

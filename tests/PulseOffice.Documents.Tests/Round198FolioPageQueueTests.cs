using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class Round198FolioPageQueueTests
{
    private readonly Fixture _f = new();

    [Fact]
    public void A_table_takes_the_column_width_of_the_page_style_where_the_caret_is()
    {
        var narrow = new PageStyle("Narrow")
        {
            Margins = Margins.Uniform(new Twip(1000)),
            Columns = new SectionColumns(2, new Twip(500)),
        };
        _f.Document.Styles.Page.Add(narrow);
        _f.Type("Before", "After");
        var second = (ParagraphBlock)_f.Document.Body.Blocks[1];
        second.Content = second.Content with { PageStyleName = "Narrow" };

        _f.Editor.InsertTable(_f.Document, _f.History, _f.At(1, 0), 1, 2);

        var table = _f.Document.Body.Blocks.OfType<TableBlock>().Single();
        var column = (narrow.Width.Value - 2000 - 500) / 2;
        Assert.InRange(table.Columns.Sum(width => width.Value), column - 2, column);
    }
}

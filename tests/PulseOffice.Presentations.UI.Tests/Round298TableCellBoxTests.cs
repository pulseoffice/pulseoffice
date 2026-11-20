using Avalonia;
using PulseOffice.Data.UI;
using PulseOffice.Document.Embedding;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round298TableCellBoxTests
{
    private readonly ITableCellBox _box = new TableCellBox();

    [Fact]
    public void Cell_margins_set_the_text_box_instead_of_the_fixed_padding()
    {
        var area = new Rect(10, 20, 100, 40);

        Assert.Equal(new Rect(12, 25, 84, 30), _box.Inner(area, new TableCellMargins(2, 14, 5, 5)));
        Assert.Equal(new Rect(16, 23, 88, 34), _box.Inner(area, null));
    }
}

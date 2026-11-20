using PulseOffice.UI.Controls.TextFormat;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round315ListSampleAlignmentTests
{
    [Theory]
    [InlineData(ListLabelAlignment.Left, 40)]
    [InlineData(ListLabelAlignment.Center, 35)]
    [InlineData(ListLabelAlignment.Right, 30)]
    public void The_sample_places_the_number_by_its_alignment(ListLabelAlignment alignment, double left)
    {
        var row = new ListSampleRows(new ListLabeler()).Rows([new ListLevelFormat(ListNumbering.Arabic) { Alignment = alignment }], [0]).Single();

        Assert.Equal(alignment, row.Alignment);
        Assert.Equal(left, row.LabelLeft(40, 10), 3);
    }
}

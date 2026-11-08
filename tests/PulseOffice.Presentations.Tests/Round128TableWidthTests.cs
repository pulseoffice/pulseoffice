using PulseOffice.Document.Embedding;
using PulseOffice.Document.Tables;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class Round128TableWidthTests
{
    private DataTableObject Table(string first, string second) => new()
    {
        Id = "t",
        Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
        Snapshot = new DataSnapshot
        {
            Columns = [new DataColumn("A", DataValueKind.Text), new DataColumn("B", DataValueKind.Text)],
            Rows = [new object?[] { first, second }],
        },
    };

    [Fact]
    public void A_very_long_text_does_not_squeeze_the_other_columns_to_nothing()
    {
        var fractions = new OptimalColumnSizing(new ColumnWidths()).Apply(Table(new string('x', 5000), "short text")).ColumnFractions;

        Assert.True(fractions[1] > 0.15, $"second column fraction {fractions[1]}");
    }

    [Fact]
    public void The_widest_line_of_a_multiline_text_sets_the_width()
    {
        var multiline = new OptimalColumnSizing(new ColumnWidths()).Apply(Table("abcd\nabcd\nabcd\nabcd", "abcd")).ColumnFractions;

        Assert.Equal(multiline[0], multiline[1], 6);
    }
}

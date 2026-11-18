using PulseOffice.Documents.Styles;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round204FolioMinorQueueTests
{
    [Fact]
    public void A_paragraph_line_number_restart_starts_the_count_again()
    {
        var f = new LayoutFixture(width: 200, height: 400);
        f.Add("uno");
        var second = f.Add("due");

        var numbers = new PulseOffice.Documents.Layout.Queries.LineNumbers().Number(f.Layout(), new LineNumbering(1) { Start = 3 }, new Dictionary<string, int> { [second.Id] = 7 });

        Assert.Equal([3, 7], numbers.Values.Order());
    }
}

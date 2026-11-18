using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;
using PulseOffice.Data.UI;
using PulseOffice.Document.Embedding;
using PulseOffice.Testing;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round133TableEditorTimingTests(ITestOutputHelper output)
{
    private static readonly UiThreadSession Session = UiThreadSession.StartNew(typeof(DeckHeadlessApp));

    private static DataTableObject Table(int rows, int columns) => new()
    {
        Id = "big",
        Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
        Snapshot = new DataSnapshot
        {
            Columns = [.. Enumerable.Range(0, columns).Select(c => new DataColumn($"C{c}", DataValueKind.Text))],
            Rows = [.. Enumerable.Range(0, rows).Select(r => (IReadOnlyList<object?>)[.. Enumerable.Range(0, columns).Select(c => (object?)$"r{r}c{c}")])],
        },
    };

    [Fact]
    public Task A_table_of_one_hundred_thousand_cells_opens_quickly() => Session.Dispatch(() =>
    {
        _ = Open(100);
        var small = Open(1_000);
        var large = Open(10_000);
        output.WriteLine($"Opening 10,000 cells: {small.TotalMilliseconds:0} ms, 100,000 cells: {large.TotalMilliseconds:0} ms");
        new PulseOffice.Testing.GrowthCheck(TimeSpan.FromMilliseconds(20), 4).Bounded(small, large, 10, $"10,000 cells {small}, 100,000 cells {large}");
    }, CancellationToken.None);

    private static TimeSpan Open(int rows)
    {
        var table = Table(rows, 10);
        var clock = Stopwatch.StartNew();
        var view = new TableEditorView(table, new FakeTextCatalog());
        var window = new Window { Width = 800, Height = 600, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        clock.Stop();
        window.Close();
        return clock.Elapsed;
    }
}

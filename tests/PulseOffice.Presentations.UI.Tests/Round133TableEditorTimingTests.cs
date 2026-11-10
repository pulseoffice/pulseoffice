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
        var table = Table(10_000, 10);
        var clock = Stopwatch.StartNew();
        var view = new TableEditorView(table, new FakeTextCatalog());
        var window = new Window { Width = 800, Height = 600, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        clock.Stop();
        output.WriteLine($"Opening 100,000 cells: {clock.ElapsedMilliseconds} ms");
        Console.WriteLine($"TIMING Opening 100,000 cells: {clock.ElapsedMilliseconds} ms");
        window.Close();
        Assert.True(clock.ElapsedMilliseconds < 3_000, $"{clock.ElapsedMilliseconds} ms");
    }, CancellationToken.None);
}

using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PulseOffice.Data.UI;
using PulseOffice.Document.Embedding;
using PulseOffice.Testing;
using PulseOffice.UI.Theming;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round128TableEditorTests
{
    private static readonly UiThreadSession Session = UiThreadSession.StartNew(typeof(DeckHeadlessApp));

    private DataTableObject Table(int rows) => new()
    {
        Id = "t",
        Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
        Snapshot = new DataSnapshot
        {
            Columns = [new DataColumn("A", DataValueKind.Text), new DataColumn("B", DataValueKind.Text)],
            Rows = [.. Enumerable.Range(0, rows).Select(row => (IReadOnlyList<object?>)new object?[] { $"a{row}", $"b{row}" })],
        },
    };

    [Fact]
    public Task Cells_take_the_theme_surface_of_the_window_they_open_in() => Session.Dispatch(() =>
    {
        var surface = new SolidColorBrush(Color.FromRgb(0x20, 0x22, 0x26));
        var view = new TableEditorView(Table(1), new FakeTextCatalog());
        var window = new Window { Width = 800, Height = 600, Content = view };
        window.Resources[Palette.Keys.Surface] = surface;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var cell = view.GetVisualDescendants().OfType<TextBox>().Single(box => box.Text == "b0").GetVisualAncestors().OfType<Border>().First();
        Assert.Same(surface, cell.Background);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task A_header_merge_spans_the_header_fields() => Session.Dispatch(() =>
    {
        var table = Table(1) with { Merges = [new TableMerge(TableCellFormat.HeaderRow, 0, 1, 2)] };
        var view = new TableEditorView(table, new FakeTextCatalog());
        var window = new Window { Width = 800, Height = 600, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var boxes = view.GetVisualDescendants().OfType<TextBox>().ToList();
        Assert.DoesNotContain(boxes, box => box.Text == "B");
        var header = boxes.Single(box => box.Text == "A").GetVisualAncestors().OfType<Border>().First();
        Assert.Equal(2, Grid.GetColumnSpan(header));
        window.Close();
    }, CancellationToken.None);
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PulseOffice.Data.UI;
using PulseOffice.Document.Embedding;
using PulseOffice.Testing;
using PulseOffice.UI.Theming;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round133TableEditorTests
{
    private static readonly UiThreadSession Session = UiThreadSession.StartNew(typeof(DeckHeadlessApp));

    private static DataTableObject Table(params string[][] rows) => new()
    {
        Id = "t",
        Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
        Snapshot = new DataSnapshot
        {
            Columns = [new DataColumn("First", DataValueKind.Text), new DataColumn("Second", DataValueKind.Text)],
            Rows = [.. rows.Select(row => (IReadOnlyList<object?>)[.. row])],
        },
    };

    private static (TableEditorView View, Window Window) Open(DataTableObject table, FakeTextCatalog? texts = null)
    {
        var view = new TableEditorView(table, texts ?? new FakeTextCatalog());
        var window = new Window { Width = 900, Height = 700, Content = view };
        window.Resources[Palette.Keys.Surface] = new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xFA));
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (view, window);
    }

    private static Control Shown(TableEditorView view, string text) =>
        view.GetVisualDescendants().OfType<Control>().Where(control => control.IsEffectivelyVisible).First(control =>
            control is TextBlock { Text: var shown } && shown == text || control is TextBox { Text: var typed } && typed == text);

    private static Border Host(TableEditorView view, string text) =>
        Shown(view, text).GetVisualAncestors().OfType<Border>().First();

    private static void Click(Window window, Control target)
    {
        var center = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public Task A_cell_with_two_paragraphs_shows_them_on_two_lines_and_its_row_grows() => Session.Dispatch(() =>
    {
        var (view, window) = Open(Table(["one\ntwo", "x"], ["y", "z"]));

        var tall = Host(view, "x").Bounds.Height;
        var plain = Host(view, "y").Bounds.Height;
        Assert.True(tall > plain * 1.5, $"{tall} vs {plain}");
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task A_header_merge_read_from_a_file_can_be_selected_and_split() => Session.Dispatch(() =>
    {
        var texts = new FakeTextCatalog();
        texts.Add("command.deck.table.split.title", "Split cells");
        var table = Table(["a", "b"]) with { Merges = [new TableMerge(TableCellFormat.HeaderRow, 0, 1, 2)] };
        var (view, window) = Open(table, texts);

        Click(window, Host(view, "First"));
        var split = view.GetVisualDescendants().OfType<Button>().Single(button =>
            button.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Text == "Split cells"));
        Assert.True(split.IsEnabled);
        Click(window, split);

        Assert.Equal("Second", Shown(view, "Second") is TextBlock block ? block.Text : ((TextBox)Shown(view, "Second")).Text);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Rows_are_not_banded_when_the_table_turns_banding_off() => Session.Dispatch(() =>
    {
        var table = Table(["a0", "b0"], ["a1", "b1"], ["a2", "b2"]) with { Style = new DataTableStyle { BandedRows = false } };
        var (view, window) = Open(table);

        Assert.Equal(Host(view, "b0").Background, Host(view, "b1").Background);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Rows_are_banded_when_the_table_asks_for_it() => Session.Dispatch(() =>
    {
        var table = Table(["a0", "b0"], ["a1", "b1"], ["a2", "b2"]) with { Style = new DataTableStyle { BandedRows = true } };
        var (view, window) = Open(table);

        Assert.NotEqual(Host(view, "b0").Background, Host(view, "b1").Background);
        Assert.Equal(Host(view, "b0").Background, Host(view, "b2").Background);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Only_the_visible_cells_of_a_large_table_are_realised() => Session.Dispatch(() =>
    {
        var rows = Enumerable.Range(0, 2_000).Select(r => new[] { $"a{r}", $"b{r}" }).ToArray();
        var (view, window) = Open(Table(rows));

        var realised = view.GetVisualDescendants().Count(control => control is TextBlock or TextBox);
        Assert.True(realised < 400, $"{realised} realised");
        window.Close();
    }, CancellationToken.None);
}

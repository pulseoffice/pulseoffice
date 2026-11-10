using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PulseOffice.Data.UI;
using PulseOffice.Document.Embedding;
using PulseOffice.Testing;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class Round133TableEditorSheetTests
{
    private static readonly UiThreadSession Session = UiThreadSession.StartNew(typeof(DeckHeadlessApp));

    private static DataTableObject Table(int rows, int columns = 3) => new()
    {
        Id = "t",
        Query = new DataQuerySource { ConnectionId = string.Empty, Sql = string.Empty },
        Snapshot = new DataSnapshot
        {
            Columns = [.. Enumerable.Range(0, columns).Select(c => new DataColumn($"H{c}", DataValueKind.Text))],
            Rows = [.. Enumerable.Range(0, rows).Select(r => (IReadOnlyList<object?>)[.. Enumerable.Range(0, columns).Select(c => (object?)$"r{r}c{c}")])],
        },
    };

    private static TableEditorSheet Sheet(DataTableObject table) => new(table, new FakeTextCatalog(), new TableEditModel());

    [Fact]
    public void Header_cells_merge_into_a_one_row_header_merge_and_split_back()
    {
        var sheet = Sheet(Table(2));
        sheet.SelectRange(new TableCellRef(TableCellFormat.HeaderRow, 0), new TableCellRef(TableCellFormat.HeaderRow, 1));
        Assert.True(sheet.CanMerge);
        sheet.Merge();
        Assert.Equal(new TableMerge(TableCellFormat.HeaderRow, 0, 1, 2), Assert.Single(sheet.Merges));

        sheet.Select(new TableCellRef(TableCellFormat.HeaderRow, 1));
        Assert.Equal(new TableCellRef(TableCellFormat.HeaderRow, 0), sheet.Active);
        Assert.True(sheet.CanSplit);
        sheet.Split();
        Assert.Empty(sheet.Merges);
    }

    [Fact]
    public void A_selection_across_header_and_body_cannot_be_merged()
    {
        var sheet = Sheet(Table(2));
        sheet.SelectRange(new TableCellRef(TableCellFormat.HeaderRow, 0), new TableCellRef(1, 0));
        Assert.False(sheet.CanMerge);
        sheet.Merge();
        Assert.Empty(sheet.Merges);
    }

    [Fact]
    public void Arrow_keys_step_over_a_merged_cell()
    {
        var sheet = Sheet(Table(3) with { Merges = [new TableMerge(0, 0, 2, 2)] });
        sheet.Select(new TableCellRef(0, 0));
        sheet.Move(0, 1);
        Assert.Equal(new TableCellRef(0, 2), sheet.Active);
        sheet.Select(new TableCellRef(0, 0));
        sheet.Move(1, 0);
        Assert.Equal(new TableCellRef(2, 0), sheet.Active);
        sheet.Move(-1, 0);
        Assert.Equal(new TableCellRef(0, 0), sheet.Active);
        sheet.Move(-1, 0);
        Assert.Equal(new TableCellRef(TableCellFormat.HeaderRow, 0), sheet.Active);
    }

    [Fact]
    public void Text_and_structure_changes_undo_and_redo()
    {
        var table = Table(2);
        var sheet = Sheet(table);
        sheet.SetText(new TableCellRef(0, 0), "one\ntwo");
        sheet.AppendRow();
        Assert.Equal(3, sheet.RowCount);

        sheet.Undo();
        Assert.Equal(2, sheet.RowCount);
        sheet.Undo();
        Assert.Equal("r0c0", sheet.Text(0, 0));
        sheet.Redo();
        Assert.Equal("one\ntwo", sheet.Text(0, 0));
        Assert.Equal("one\ntwo", sheet.Build(table.Snapshot).Snapshot.Rows[0][0]);
    }

    [Fact]
    public void The_line_estimate_counts_paragraphs_and_wrapping()
    {
        var lines = new CellLineEstimator();
        Assert.Equal(1, lines.Lines(string.Empty, 100, 10));
        Assert.Equal(3, lines.Lines("a\r\nb\nc", 100, 10));
        Assert.True(lines.Lines(new string('x', 200), 100, 10) >= 10);
    }

    [Fact]
    public Task Editing_a_cell_keeps_its_line_breaks_and_grows_the_row() => Session.Dispatch(() =>
    {
        var view = new TableEditorView(Table(3), new FakeTextCatalog());
        var window = new Window { Width = 900, Height = 700, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        view.Sheet.Select(new TableCellRef(1, 1));
        view.Cells.BeginEdit("first\r\nsecond\nthird");
        Dispatcher.UIThread.RunJobs();
        Assert.True(view.Cells.IsEditing);
        view.Cells.Commit();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("first\nsecond\nthird", view.Sheet.Text(1, 1));
        var grown = view.GetVisualDescendants().OfType<TableCellPresenter>().Single(p => p.IsVisible && p.Text == "first\nsecond\nthird");
        var plain = view.GetVisualDescendants().OfType<TableCellPresenter>().Single(p => p.IsVisible && p.Text == "r0c1");
        Assert.True(grown.Bounds.Height > plain.Bounds.Height * 2);
        window.Close();
    }, CancellationToken.None);

    [Fact]
    public Task Scrolling_recycles_presenters_onto_far_rows() => Session.Dispatch(() =>
    {
        var view = new TableEditorView(Table(5_000), new FakeTextCatalog());
        var window = new Window { Width = 900, Height = 700, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var before = view.GetVisualDescendants().OfType<TableCellPresenter>().Count();

        view.Cells.Offset = new Vector(0, double.MaxValue);
        Dispatcher.UIThread.RunJobs();

        var presenters = view.GetVisualDescendants().OfType<TableCellPresenter>().ToList();
        Assert.Contains(presenters, p => p.IsVisible && p.Text == "r4999c0");
        Assert.DoesNotContain(presenters, p => p.IsVisible && p.Text == "r0c0");
        Assert.True(presenters.Count <= before + 4, $"{presenters.Count} vs {before}");
        window.Close();
    }, CancellationToken.None);
}

using PulseOffice.Document.Tables;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Editing.Tables;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Localization;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Tables;

namespace PulseOffice.Documents.UI.Commands;

public sealed record FolioTableEditing(
    ITableEditing Tables,
    ITableCellEditing Cells,
    ITableGeometryEditing Geometry,
    ITableFormatting Formatting,
    ITableSorting Sorting,
    ITableFormulaEditing Formulas,
    ITableConversion Conversion,
    ITableMeasure Measure,
    ITableGrid Grid);

public sealed class FolioTableCommands(
    IActiveFolioContext context,
    FolioTableEditing editing,
    IFolioTableDialogs dialogs,
    ITableStyleGallery gallery,
    IFolioTableLooks looks,
    ITableStylePresets presets,
    IStatusBarService status,
    ITextCatalog texts,
    ITableBorderFraming framing) : FolioCommandSet(context)
{
    private readonly CellBorderPreset[] _borders = [CellBorderPreset.All, CellBorderPreset.Outer, CellBorderPreset.Inner, CellBorderPreset.OuterThickInnerThin, CellBorderPreset.None];

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Table("folio.table.rowAbove", (editor, at) => editing.Tables.InsertRow(editor.Document, editor.History, at.At with { Row = at.Range.Top }, false)),
        Table("folio.table.rowBelow", (editor, at) => editing.Tables.InsertRow(editor.Document, editor.History, at.At with { Row = at.Range.Bottom }, true)),
        Table("folio.table.columnLeft", (editor, at) => editing.Tables.InsertColumn(editor.Document, editor.History, at.At with { Column = at.Range.Left }, false)),
        Table("folio.table.columnRight", (editor, at) => editing.Tables.InsertColumn(editor.Document, editor.History, at.At with { Column = at.Range.Right }, true)),
        Table("folio.table.deleteRow", (editor, at) => editing.Tables.DeleteRow(editor.Document, editor.History, at.At)),
        Table("folio.table.deleteColumn", (editor, at) => editing.Tables.DeleteColumn(editor.Document, editor.History, at.At)),
        Table("folio.table.delete", (editor, at) => editing.Tables.DeleteTable(editor.Document, editor.History, at.At)),
        Dialog("folio.table.insertRows", (editor, at, ct) => InsertAsync(editor, at, true, ct)),
        Dialog("folio.table.insertColumns", (editor, at, ct) => InsertAsync(editor, at, false, ct)),
        Select("folio.table.selectCell", at => new TableCellRange(at.At.Row, at.At.Column, at.At.Row, at.At.Column)),
        Select("folio.table.selectRow", at => new TableCellRange(at.Range.Top, 0, at.Range.Bottom, at.Table.ColumnCount - 1)),
        Select("folio.table.selectColumn", at => new TableCellRange(0, at.Range.Left, at.Table.Rows.Count - 1, at.Range.Right)),
        Select("folio.table.selectTable", at => editing.Grid.Whole(at.Table)),
        Table("folio.table.merge", (editor, at) => editing.Cells.Merge(editor.Document, editor.History, at), at => editing.Cells.CanMerge(at)),
        Dialog("folio.table.split", SplitAsync),
        Dialog("folio.table.splitTable", SplitTableAsync, at => at.At.Row > 0),
        Table("folio.table.mergeTable", (editor, at) => editing.Conversion.MergeWithNext(editor.Document, editor.History, at.At), at => editing.Conversion.CanMergeWithNext(at.At)),
        Dialog("folio.table.rowHeight", RowHeightAsync),
        Table("folio.table.optimalRowHeight", (editor, at) => editing.Geometry.SetRowHeight(editor.Document, editor.History, at, null, true)),
        Table("folio.table.minimalRowHeight", (editor, at) => editing.Geometry.SetRowHeight(editor.Document, editor.History, at, null, true)),
        Table("folio.table.distributeRows", (editor, at) => editing.Geometry.DistributeRows(editor.Document, editor.History, Rows(at), RowHeights(editor, at.Table))),
        Dialog("folio.table.columnWidth", ColumnWidthAsync),
        Table("folio.table.optimalColumnWidth", (editor, at) => Fit(editor, at, TableMeasureKind.Optimal, "optimal-table-columns")),
        Table("folio.table.minimalColumnWidth", (editor, at) => Fit(editor, at, TableMeasureKind.Minimal, "minimal-table-columns")),
        Table("folio.table.distributeColumns", (editor, at) => editing.Geometry.DistributeColumns(editor.Document, editor.History, at)),
        Dialog("folio.table.style", StyleAsync),
        .. _borders.Select(preset => Table($"folio.table.borders.{Key(preset)}", (editor, at) => editing.Formatting.SetBorders(editor.Document, editor.History, at, preset, Line(editor, at)))),
        Table("folio.table.alignTop", (editor, at) => editing.Formatting.SetVerticalAlign(editor.Document, editor.History, at, CellVerticalAlign.Top), checkedWhen: at => at.Table.Cell(at.At.Row, at.At.Column).VerticalAlign == CellVerticalAlign.Top),
        Table("folio.table.alignMiddle", (editor, at) => editing.Formatting.SetVerticalAlign(editor.Document, editor.History, at, CellVerticalAlign.Middle), checkedWhen: at => at.Table.Cell(at.At.Row, at.At.Column).VerticalAlign == CellVerticalAlign.Middle),
        Table("folio.table.alignBottom", (editor, at) => editing.Formatting.SetVerticalAlign(editor.Document, editor.History, at, CellVerticalAlign.Bottom), checkedWhen: at => at.Table.Cell(at.At.Row, at.At.Column).VerticalAlign == CellVerticalAlign.Bottom),
        Table("folio.table.headingRepeat", (editor, at) => editing.Formatting.SetHeaderRows(editor.Document, editor.History, at.Table, Math.Max(1, at.Table.HeaderRows),
            !(at.Table.HeaderRows > 0 && at.Table.RepeatHeaderRows)), checkedWhen: at => at.Table.HeaderRows > 0 && at.Table.RepeatHeaderRows),
        Table("folio.table.rowSplit", (editor, at) => editing.Formatting.SetRowBreak(editor.Document, editor.History, at, !at.Table.Rows[at.At.Row].AllowBreakAcrossPages),
            checkedWhen: at => at.Table.Rows[at.At.Row].AllowBreakAcrossPages),
        Dialog("folio.table.sort", SortAsync, at => editing.Sorting.CanSort(at)),
        Dialog("folio.table.formula", FormulaAsync),
        Dialog("folio.table.toText", ToTextAsync),
        Dialog("folio.table.properties", PropertiesAsync),
        TextToTable(),
    ];

    public bool InTable(FolioEditor editor) => editor.InTable;

    private string Key(CellBorderPreset preset) => char.ToLowerInvariant(preset.ToString()[0]) + preset.ToString()[1..];

    private CommandDefinition Contextual(string id, Func<TableSelection, bool>? when) =>
        Editing(id) with
        {
            Surface = CommandSurface.Common,
            RequiredSelection = SelectionKind.Table,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && editor.TableAt() is { } at && (when?.Invoke(at) ?? true),
        };

    private CommandDefinition Table(string id, Func<FolioEditor, TableSelection, DocumentEdit> change, Func<TableSelection, bool>? when = null, Func<TableSelection, bool>? checkedWhen = null) =>
        Contextual(id, when) with
        {
            CheckedWhen = checkedWhen is null ? null : _ => Editor?.TableAt() is { } at && checkedWhen(at),
            Execute = (_, _) =>
            {
                if (Editor is { } editor && !editor.ReadOnly() && editor.TableAt() is { } at)
                {
                    change(editor, at);
                    Settle(editor);
                }
                return Task.CompletedTask;
            },
        };

    private CommandDefinition Dialog(string id, Func<FolioEditor, TableSelection, CancellationToken, Task> run, Func<TableSelection, bool>? when = null) =>
        Contextual(id, when) with
        {
            Execute = async (_, ct) =>
            {
                if (Editor is { } editor && !editor.ReadOnly() && editor.TableAt() is { } at)
                {
                    await run(editor, at, ct).ConfigureAwait(true);
                    Settle(editor);
                }
            },
        };

    private CommandDefinition Select(string id, Func<TableSelection, TableCellRange> range) =>
        Prototype(id) with
        {
            Surface = CommandSurface.Common,
            RequiredSelection = SelectionKind.Table,
            EnabledWhen = _ => Editor?.TableAt() is not null,
            Execute = (_, _) =>
            {
                if (Editor is { } editor && editor.TableAt() is { } at) editor.SelectCells(at.Table, range(at));
                return Task.CompletedTask;
            },
        };

    private CommandDefinition TextToTable() =>
        Editing("folio.table.textToTable") with
        {
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && !editor.InTable
                && editing.Conversion.CanConvertToTable(editor.Document, editor.ParagraphRange()),
            Execute = async (_, ct) =>
            {
                if (Editor is not { } editor || editor.ReadOnly()) return;
                var sample = editor.SelectedText().Split('\n').Where(line => line.Length > 0).Take(5).ToList();
                if (sample.Count == 0) sample = [editor.Services.Stories.Find(editor.Document, editor.Caret.ParagraphId).Paragraph.Text];
                if (await dialogs.TextToTableAsync(sample, ct).ConfigureAwait(true) is not { } options) return;
                var width = editor.Layout.Pages.Count > 0 ? TextWidth(editor) : 450;
                var result = editing.Conversion.ToTable(editor.Document, editor.History, editor.ParagraphRange(), options, width);
                Place(editor, result.FirstParagraphId);
            },
        };

    private void Settle(FolioEditor editor)
    {
        editor.Normalize();
        editor.Select(editor.Anchor, editor.Caret);
    }

    private void Place(FolioEditor editor, string? paragraphId)
    {
        editor.Normalize();
        if (paragraphId is not null) editor.Select(new DocumentPosition(paragraphId, 0), new DocumentPosition(paragraphId, 0));
        else editor.Select(editor.Caret, editor.Caret);
    }

    private double TextWidth(FolioEditor editor)
    {
        var page = editor.Document.Styles.Page.Find(editor.Document.InitialPageStyle);
        return page is null ? 450 : (page.Width - page.Margins.Left - page.Margins.Right).ToPoints();
    }

    private TableSelection Rows(TableSelection at) =>
        at.Range.IsSingleCell ? at with { Range = editing.Grid.Whole(at.Table) } : at;

    private IReadOnlyList<double> RowHeights(FolioEditor editor, TableBlock table)
    {
        var heights = new double[table.Rows.Count];
        foreach (var box in editor.Layout.Pages.SelectMany(page => page.Tables).Where(box => box.TableId == table.Id))
            foreach (var cell in box.Cells.Where(cell => cell.Row < heights.Length && table.Cell(cell.Row, cell.Column).RowSpan <= 1))
                heights[cell.Row] = Math.Max(heights[cell.Row], cell.Bounds.Height);
        return heights;
    }

    private DocumentEdit Fit(FolioEditor editor, TableSelection at, TableMeasureKind kind, string description)
    {
        var wanted = editing.Measure.Widths(editor.Document, at.Table, kind);
        var range = at.Range.IsSingleCell ? at with { Range = editing.Grid.Whole(at.Table) } : at;
        return editing.Geometry.FitColumns(editor.Document, editor.History, range, wanted, TextWidth(editor), description);
    }

    private BorderLine Line(FolioEditor editor, TableSelection at)
    {
        var style = editor.Document.Styles.Table.Find(at.Table.StyleName)?.Table;
        return new BorderLine(style?.BorderWidth is > 0 ? style.BorderWidth.Value : 0.5, style?.BorderColor ?? 0xFF000000);
    }

    private async Task SplitAsync(FolioEditor editor, TableSelection at, CancellationToken ct)
    {
        if (editing.Cells.IsMerged(at))
        {
            editing.Cells.Unmerge(editor.Document, editor.History, at);
            return;
        }
        if (await dialogs.SplitAsync(ct).ConfigureAwait(true) is { } split)
            editing.Cells.Split(editor.Document, editor.History, at, split.Parts, split.Horizontally);
    }

    private async Task InsertAsync(FolioEditor editor, TableSelection at, bool rows, CancellationToken ct)
    {
        if (await dialogs.InsertAsync(rows, ct).ConfigureAwait(true) is not { } choice) return;
        using var transaction = editor.History.BeginTransaction(rows ? "insert-table-rows" : "insert-table-columns");
        for (var index = 0; index < choice.Count; index++)
        {
            if (rows) editing.Tables.InsertRow(editor.Document, editor.History, at.At with { Row = choice.After ? at.Range.Bottom : at.Range.Top }, choice.After);
            else editing.Tables.InsertColumn(editor.Document, editor.History, at.At with { Column = choice.After ? at.Range.Right : at.Range.Left }, choice.After);
        }
    }

    private async Task SplitTableAsync(FolioEditor editor, TableSelection at, CancellationToken ct)
    {
        if (await dialogs.SplitTableAsync(at.Table.HeaderRows > 0, ct).ConfigureAwait(true) is not { } heading) return;
        var result = editing.Conversion.SplitTable(editor.Document, editor.History, at.At, heading);
        Place(editor, result.FirstParagraphId);
    }

    private async Task RowHeightAsync(FolioEditor editor, TableSelection at, CancellationToken ct)
    {
        if (await dialogs.RowHeightAsync(at.Table.Rows[at.At.Row], ct).ConfigureAwait(true) is { } choice)
            editing.Geometry.SetRowHeight(editor.Document, editor.History, at, choice.Points, choice.Minimum);
    }

    private async Task ColumnWidthAsync(FolioEditor editor, TableSelection at, CancellationToken ct)
    {
        var column = at.Range.Left;
        var current = column < at.Table.Columns.Count ? at.Table.Columns[column].ToPoints() : 72;
        if (await dialogs.ColumnWidthAsync(current, ct).ConfigureAwait(true) is { } width)
            editor.ResizeColumn(at.Table, column, width);
    }

    private async Task StyleAsync(FolioEditor editor, TableSelection at, CancellationToken ct)
    {
        var current = presets.Named(at.Table.StyleName)?.Key;
        if (await gallery.AskAsync("command.folio.table.style.title", current, at.Table.HeaderRows > 0, true, ct).ConfigureAwait(true) is not { } choice) return;
        looks.Apply(editor, at.Table, choice.Look, choice.HeaderRow, choice.BandedRows);
    }

    private async Task SortAsync(FolioEditor editor, TableSelection at, CancellationToken ct)
    {
        var range = editing.Sorting.SortRange(at);
        if (await dialogs.SortAsync(at.Table, range, ct).ConfigureAwait(true) is { Count: > 0 } keys)
            editing.Sorting.Sort(editor.Document, editor.History, at, keys, editor.Culture());
    }

    private async Task FormulaAsync(FolioEditor editor, TableSelection at, CancellationToken ct)
    {
        var cell = at.Table.Cell(at.At.Row, at.At.Column);
        if (await dialogs.FormulaAsync(cell.Formula, ct).ConfigureAwait(true) is not { } formula) return;
        if (formula.Trim() is "" or "=")
        {
            editing.Formulas.SetFormula(editor.Document, editor.History, at.At, null, editor.Culture());
            return;
        }
        if (!editing.Formulas.IsValid(formula))
        {
            status.Report(texts["folio.table.formula.invalid"], NotificationSeverity.Warning);
            return;
        }
        editing.Formulas.SetFormula(editor.Document, editor.History, at.At, formula, editor.Culture());
    }

    private async Task ToTextAsync(FolioEditor editor, TableSelection at, CancellationToken ct)
    {
        if (await dialogs.TableToTextAsync(ct).ConfigureAwait(true) is not { } options) return;
        var result = editing.Conversion.ToText(editor.Document, editor.History, at.At, options);
        Place(editor, result.FirstParagraphId);
    }

    private async Task PropertiesAsync(FolioEditor editor, TableSelection at, CancellationToken ct)
    {
        var cell = at.Table.Cell(at.At.Row, at.At.Column);
        if (await dialogs.PropertiesAsync(at, cell, editor.Layout.Pages.Count > 0 ? TextWidth(editor) : 450, ct).ConfigureAwait(true) is not { } choice) return;
        using var transaction = editor.History.BeginTransaction("table-properties");
        editing.Formatting.SetName(editor.Document, editor.History, at.Table, choice.Name);
        editing.Formatting.SetHeaderRows(editor.Document, editor.History, at.Table, choice.HeaderRows, choice.Repeat);
        if (choice.ResetBorders) editing.Formatting.ClearBorders(editor.Document, editor.History, at);
        else if (choice.BordersAfter != choice.BordersBefore) framing.Apply(editor.Document, editor.History, at, choice.BordersBefore, choice.BordersAfter);
        if (choice.ChangeBackground) editing.Formatting.SetBackground(editor.Document, editor.History, at, choice.Background);
        editing.Formatting.SetVerticalAlign(editor.Document, editor.History, at, choice.Align);
        if (choice.Direction != cell.TextDirection) editing.Formatting.SetTextDirection(editor.Document, editor.History, at, choice.Direction);
        if (choice.Placement != at.Table.Alignment || choice.Indent != at.Table.Indent)
            editing.Formatting.SetPlacement(editor.Document, editor.History, at.Table, choice.Placement, choice.Indent);
    }
}

using PulseOffice.Localization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Sheets.UI.LedgerViewParts;

internal sealed class LedgerCellOperations(
    Workbook workbook,
    CalculationEngine engine,
    SheetGridView grid,
    SheetSelection selection,
    PulseOffice.Document.Commands.UndoStack history,
    IStatusBarService status,
    ITextCatalog texts,
    LedgerCellWriter writer,
    Outline.IOutlineGutter gutter)
{
    public event EventHandler? SelectionStale;

    public ISheetFill Fill { get; set; } = new SheetFill();

    public event EventHandler<SheetGridView.FillRequest>? Filled;

    private (SheetGridView.FillRequest Request, object? State)? _lastFill;

    public bool RefillAs(FillMode mode)
    {
        if (_lastFill is not { } last || !ReferenceEquals(history.StatePoint, last.State) || !history.CanUndo) return false;
        history.Undo();
        ApplyFill(last.Request with { Mode = mode, Toggled = false });
        return true;
    }

    public void ClearContents(CellRange range)
    {
        if (!writer.CanEdit()) return;

        var index = grid.SheetIndex;
        var sheet = workbook.Sheets[index];
        var area = sheet.Merges.Expand(range);

        var filled = sheet.NonEmptyCellsIn(area)
            .Where(pair => pair.Value.HasFormula || !pair.Value.Value.IsEmpty)
            .Select(pair => pair.Key)
            .ToList();

        if (filled.Count == 0) return;

        if (filled.Any(address => writer.IsLocked(sheet, address)))
        {
            status.Report(texts["status.protection.locked"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
            return;
        }

        using (var transaction = history.BeginTransaction(texts["history.cell.clear"]))
        {
            foreach (var address in filled)
            {
                var target = new SheetCell(index, address);
                var previous = sheet[address];

                engine.SetValue(target, CellValue.Empty);
                writer.RecordWrite(target, previous, sheet[address]);
            }
        }

        SelectionStale?.Invoke(this, EventArgs.Empty);
        grid.Refresh();
    }

    public void ApplyResize(SheetGridView.ResizeRequest request)
    {
        var sheet = workbook.Sheets[grid.SheetIndex];

        if (!writer.CanEdit()) return;

        if (sheet.Protection is { IsEnabled: true, AllowFormatCells: false })
        {
            status.Report(texts["status.protection.format"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
            grid.Refresh();
            return;
        }

        var layout = sheet.Layout;

        void Set(Foundation.Units.Twip? size)
        {
            if (request.IsColumn)
            {
                if (size is { } width) layout.SetColumnWidth(request.Index, width);
                else layout.ResetColumnWidth(request.Index);
            }
            else
            {
                if (size is { } height) layout.SetRowHeight(request.Index, height);
                else layout.ResetRowHeight(request.Index);
            }
        }

        Set(request.After);

        history.Push(new PulseOffice.Document.Commands.DelegateAction(
            texts[request.IsColumn ? "history.column.width" : "history.row.height"],
            redo: () => Set(request.After),
            undo: () => Set(request.Before)));

        grid.Refresh();
    }

    public void ApplyNoteBox(SheetGridView.NoteBoxChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var sheet = workbook.Sheets[grid.SheetIndex];
        if (!writer.CanEdit() || !sheet.Comments.ContainsKey(change.Address))
        {
            grid.Refresh();
            return;
        }

        if (sheet.Protection.BlocksObjects)
        {
            status.Report(texts["status.protection.objects"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
            grid.Refresh();
            return;
        }

        void Set(CommentBox? box)
        {
            if (sheet.Comments.TryGetValue(change.Address, out var comment)) sheet.Comments[change.Address] = comment with { Box = box };
        }

        Set(change.After);
        history.Push(new PulseOffice.Document.Commands.DelegateAction(
            texts["history.comment.box"],
            redo: () => Set(change.After),
            undo: () => Set(change.Before)));

        grid.Refresh();
    }

    public void ApplyOutline(Outline.OutlineButton button)
    {
        ArgumentNullException.ThrowIfNull(button);
        if (!writer.CanEdit()) return;

        var sheet = workbook.Sheets[grid.SheetIndex];
        if (sheet.Protection.IsEnabled)
        {
            status.Report(texts["status.protection.format"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
            return;
        }

        var before = new OutlineState(sheet);
        gutter.Press(sheet, button);
        var after = new OutlineState(sheet);

        history.Push(new PulseOffice.Document.Commands.DelegateAction(
            texts["history.outline.details"],
            redo: () => after.Restore(sheet),
            undo: () => before.Restore(sheet)));

        SelectionStale?.Invoke(this, EventArgs.Empty);
        grid.Refresh();
    }

    public void ApplyRequestedStyles()
    {
        if (engine.RequestedStyles.Count == 0) return;

        foreach (var (cell, name) in engine.RequestedStyles.ToList())
        {
            if (cell.SheetIndex < 0 || cell.SheetIndex >= workbook.Sheets.Count) continue;
            if (workbook.NamedStyles.Find(name) is not { } named) continue;

            var sheet = workbook.Sheets[cell.SheetIndex];

            sheet.SetStyle(cell.Address, workbook.Styles.Intern(workbook.NamedStyles.StyleOf(named)));
        }

        engine.ClearRequestedStyles();
    }

    public void ApplyFill(SheetGridView.FillRequest request)
    {
        if (ApplyFillCore(request) is not { } mode) return;
        _lastFill = (request with { Mode = mode, Toggled = false }, history.StatePoint);
        Filled?.Invoke(this, request);
    }

    private FillMode? ApplyFillCore(SheetGridView.FillRequest request)
    {
        var sheet = workbook.Sheets[grid.SheetIndex];

        var direction =
            request.Target.End.Row > request.Source.End.Row ? FillDirection.Down :
            request.Target.Start.Row < request.Source.Start.Row ? FillDirection.Up :
            request.Target.End.Column > request.Source.End.Column ? FillDirection.Right :
            FillDirection.Left;

        if (!writer.CanEdit()) return null;
        if (LanesInUse(sheet, request, direction) is not { } lanes) return null;

        var mode = request.Toggled ? Fill.Toggled(sheet, lanes.Source, direction) : request.Mode;
        if (sheet.Protection.IsEnabled && mode == FillMode.FormattingOnly) return null;
        var produced = Fill.Extend(sheet, lanes.Source, lanes.Target, direction, mode);
        if (produced.Count == 0) return null;
        if (produced.Any(pair => writer.IsLocked(sheet, pair.Address)))
        {
            status.Report(texts["status.protection.locked"], NotificationSeverity.Error, LedgerRemedies.UnprotectSheet);
            return null;
        }
        var merges = Fill.Merges(sheet, lanes.Source, lanes.Target, direction);

        using var transaction = history.BeginTransaction(texts["history.fill"]);

        if (!merges.IsEmpty)
        {
            void Apply(IReadOnlyList<CellRange> remove, IReadOnlyList<CellRange> add)
            {
                foreach (var region in remove) sheet.Merges.Remove(region);
                sheet.Merges.AddRange(add);
            }

            Apply(merges.Removed, merges.Added);
            history.Push(new PulseOffice.Document.Commands.DelegateAction(
                texts["history.fill"],
                redo: () => Apply(merges.Removed, merges.Added),
                undo: () => Apply(merges.Added, merges.Removed)));
        }

        foreach (var (address, cell) in produced)
        {
            var target = new SheetCell(grid.SheetIndex, address);
            var previous = sheet[address];

            writer.WriteValidated(target, sheet.Protection.IsEnabled
                ? previous with { Formula = cell.Formula, Value = cell.Value }
                : previous with { Formula = cell.Formula, Value = cell.Value, StyleId = cell.StyleId, ExplicitStyle = cell.ExplicitStyle });
        }

        if (!sheet.Protection.IsEnabled) CopyComments(sheet, Fill.Comments(sheet, lanes.Source, lanes.Target, direction));
        if (!sheet.Protection.IsEnabled && mode != FillMode.WithoutFormatting
            && Fill.Validations(sheet, lanes.Source, lanes.Target, direction) is { } validations)
            ReplaceValidations(sheet, validations);

        selection.MoveTo(request.Target.Start);
        selection.ExtendTo(request.Target.End);

        grid.Refresh();
        status.Report(texts.Format("status.fill.done", produced.Count));
        return mode;
    }

    private void ReplaceValidations(Worksheet sheet, IReadOnlyList<DataValidation> validations)
    {
        List<DataValidation> before = [.. sheet.Validations];
        List<DataValidation> after = [.. validations];

        void Set(List<DataValidation> rules)
        {
            sheet.Validations.Clear();
            sheet.Validations.AddRange(rules);
        }

        Set(after);
        history.Push(new PulseOffice.Document.Commands.DelegateAction(
            texts["history.fill"],
            redo: () => Set(after),
            undo: () => Set(before)));
    }

    private void CopyComments(Worksheet sheet, IReadOnlyList<(CellAddress Address, CellComment? Comment)> comments)
    {
        if (comments.Count == 0) return;
        var previous = comments.Select(pair => (pair.Address, Comment: sheet.Comments.GetValueOrDefault(pair.Address))).ToList();

        void Set(IEnumerable<(CellAddress Address, CellComment? Comment)> values)
        {
            foreach (var (address, comment) in values)
                if (comment is null) sheet.Comments.Remove(address);
                else sheet.Comments[address] = comment;
        }

        Set(comments);
        history.Push(new PulseOffice.Document.Commands.DelegateAction(
            texts["history.fill"],
            redo: () => Set(comments),
            undo: () => Set(previous)));
    }

    private (CellRange Source, CellRange Target)? LanesInUse(Worksheet sheet, SheetGridView.FillRequest request, FillDirection direction)
    {
        var vertical = direction is FillDirection.Down or FillDirection.Up;
        var wholeLanes = vertical
            ? request.Source.Start.Column == 0 && request.Source.End.Column >= CellAddress.MaxColumns - 1
            : request.Source.Start.Row == 0 && request.Source.End.Row >= CellAddress.MaxRows - 1;
        if (!wholeLanes) return (request.Source, request.Target);
        if (sheet.UsedRange() is not { } used) return null;

        return vertical
            ? (WithColumns(request.Source, used), WithColumns(request.Target, used))
            : (WithRows(request.Source, used), WithRows(request.Target, used));
    }

    private CellRange WithColumns(CellRange range, CellRange used) =>
        new(new CellAddress(range.Start.Row, used.Start.Column), new CellAddress(range.End.Row, used.End.Column));

    private CellRange WithRows(CellRange range, CellRange used) =>
        new(new CellAddress(used.Start.Row, range.Start.Column), new CellAddress(used.End.Row, range.End.Column));
}

internal sealed record OutlineState(SheetLayout Layout, IReadOnlyList<OutlineGroup> Rows, IReadOnlyList<OutlineGroup> Columns)
{
    public OutlineState(Worksheet sheet) : this(new SheetLayout(), [.. sheet.RowGroups], [.. sheet.ColumnGroups])
    {
        sheet.Layout.CopyTo(Layout);
    }

    public void Restore(Worksheet sheet)
    {
        Layout.CopyTo(sheet.Layout);
        sheet.RowGroups.Clear();
        sheet.RowGroups.AddRange(Rows);
        sheet.ColumnGroups.Clear();
        sheet.ColumnGroups.AddRange(Columns);
    }
}

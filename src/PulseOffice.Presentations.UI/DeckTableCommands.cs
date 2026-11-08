using PulseOffice.Document.Tables;
using PulseOffice.Document.Embedding;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;

namespace PulseOffice.Presentations.UI;

public sealed class DeckTableCommands(
    IActiveDeckContext context,
    PulseOffice.UI.Controls.Services.IDialogService dialogs,
    IEnumerable<ITableSizing> sizings,
    ITextCatalog texts,
    ITableStructure structure,
    ITableSorter sorter,
    PulseOffice.UI.Controls.Tables.ITableStyleGallery gallery,
    IDeckTableLooks looks,
    PulseOffice.UI.Controls.Borders.IBorderDialog borderDialog,
    IDeckTableBorders borders,
    PulseOffice.UI.Controls.Sorting.ISortLevelsDialog sortLevels) : DeckCommandSet(context)
{
    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Table("deck.table.addRow", AddRow),
        Table("deck.table.removeRow", RemoveRow, CanRemoveRows),
        Table("deck.table.addColumn", AddColumn),
        Table("deck.table.removeColumn", RemoveColumn, CanRemoveColumns),
        Select("deck.table.selectRow", (table, range) => range with { FirstColumn = 0, LastColumn = table.Snapshot.Columns.Count - 1 }),
        Select("deck.table.selectColumn", (table, range) => range with { FirstRow = 0, LastRow = table.Snapshot.Rows.Count - 1 }),
        Table("deck.table.sortAscending", table => Sort(table, false), table => table.Merges.Count == 0 && table.Snapshot.Rows.Count > 1),
        Table("deck.table.sortDescending", table => Sort(table, true), table => table.Merges.Count == 0 && table.Snapshot.Rows.Count > 1),
        SortCommand(),
        StyleCommand(),
        BordersCommand(),
        EditCommand(),
        DeleteCommand(),
        .. sizings.Select(Sizing),
    ];

    private CommandDefinition Sizing(ITableSizing sizing) =>
        Prototype($"deck.table.{sizing.Key}") with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target?.Selected() is ObjectElement { Content: DataTableObject },
            Execute = (_, _) =>
            {
                if (Context.Target is { } t && t.CurrentSlide() is { } slide
                    && t.Selected() is ObjectElement { Content: DataTableObject table } element)
                    t.Actions.ReplaceElement(slide, Resized(element, table, sizing), "deck.action.editTable");
                return Task.CompletedTask;
            },
        };

    private ObjectElement Resized(ObjectElement element, DataTableObject table, ITableSizing sizing)
    {
        var sized = table with { Layout = sizing.Apply(table) };
        if (sized.Layout.RowHeights.SequenceEqual(table.Layout.RowHeights)) return element with { Content = sized };

        var height = new PulseOffice.Foundation.Units.Twip((int)Math.Round(TableMetrics.TotalHeight(sized) * 20));
        return element with { Content = sized, Bounds = element.Bounds with { Height = height } };
    }

    private DataTableObject Sort(DataTableObject table, bool descending)
    {
        var column = Columns(table)?.First ?? 0;
        return column >= table.Snapshot.Columns.Count ? table : Sort(table, [(column, descending)]);
    }

    private DataTableObject Sort(DataTableObject table, IReadOnlyList<(int Column, bool Descending)> levels)
    {
        var snapshot = table.Snapshot;
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        var texts = snapshot.Rows.Select(row => (IReadOnlyList<string>)[.. row.Select(value => Convert.ToString(value, culture) ?? string.Empty)]).ToList();
        var keys = levels
            .Where(level => level.Column < snapshot.Columns.Count)
            .Select(level => new TableSortKey(level.Column, level.Descending,
                snapshot.Columns[level.Column].Kind == DataValueKind.Number ? TableSortKind.Numeric : TableSortKind.Alphanumeric))
            .ToList();
        var order = sorter.Order(texts, keys, culture);
        var position = order.Select((source, target) => (source, target)).ToDictionary(pair => pair.source, pair => pair.target);
        return table with
        {
            Snapshot = snapshot with { Rows = [.. order.Select(index => snapshot.Rows[index])] },
            CellFormats = [.. table.CellFormats.Select(format => format.Row >= 0 && format.Row < order.Count ? format with { Row = position[format.Row] } : format)],
        };
    }

    private CommandDefinition SortCommand() =>
        Prototype("deck.table.sort") with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target?.Selected() is ObjectElement { Content: DataTableObject table } && table.Merges.Count == 0 && table.Snapshot.Rows.Count > 1,
            Execute = async (_, ct) =>
            {
                if (Context.Target is not { } t || t.Selected() is not ObjectElement { Content: DataTableObject current }) return;
                var headers = current.Snapshot.Columns.Select(column => column.Name).ToList();
                var choice = await sortLevels.AskAsync(new PulseOffice.UI.Controls.Sorting.SortLevelsRequest("command.deck.table.sort.title", headers, headers)
                {
                    OffersHeaderChoice = false,
                    OffersCase = false,
                    Initial = [new PulseOffice.UI.Controls.Sorting.SortLevel(Columns(current)?.First ?? 0)],
                }, ct).ConfigureAwait(true);
                if (choice is null || t.CurrentSlide() is not { } slide || t.Selected() is not ObjectElement { Content: DataTableObject } element) return;
                t.Actions.EditTable(slide, element.Id, table => Sort(table, [.. choice.Levels.Select(level => (level.Column, level.Descending))]));
            },
        };

    private CommandDefinition StyleCommand() =>
        Prototype("deck.table.style") with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target?.Selected() is ObjectElement { Content: DataTableObject },
            Execute = async (_, ct) =>
            {
                if (Context.Target is not { } t || t.Selected() is not ObjectElement { Content: DataTableObject current }) return;
                if (await gallery.AskAsync("command.deck.table.style.title", null, current.Style.HeaderRow, current.Style.BandedRows, ct).ConfigureAwait(true) is not { } choice) return;
                if (t.CurrentSlide() is not { } slide || t.Selected() is not ObjectElement { Content: DataTableObject } element) return;
                t.Actions.EditTable(slide, element.Id, table => looks.Apply(table, choice.Look, choice.HeaderRow, choice.BandedRows));
            },
        };

    private CommandDefinition BordersCommand() =>
        Prototype("deck.table.borders") with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target?.Selected() is ObjectElement { Content: DataTableObject },
            Execute = async (_, ct) =>
            {
                if (Context.Target is not { } t || t.Selected() is not ObjectElement { Content: DataTableObject current }) return;
                var target = borders.Target(current, CellsOf());
                var before = borders.FrameOf(current, target);
                if (await borderDialog.AskAsync("command.deck.table.borders.title", borders.Scope(target), before, ct).ConfigureAwait(true) is not { } after || after == before) return;
                if (t.CurrentSlide() is not { } slide || t.Selected() is not ObjectElement { Content: DataTableObject } element) return;
                t.Actions.EditTable(slide, element.Id, table => borders.Apply(table, borders.Target(table, CellsOf()), before, after));
            },
        };

    private CommandDefinition DeleteCommand() =>
        Prototype("deck.table.delete") with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target?.Selected() is ObjectElement { Content: DataTableObject },
            Execute = (_, _) =>
            {
                if (Context.Target is { } t && t.CurrentSlide() is { } slide
                    && t.Selected() is ObjectElement { Content: DataTableObject } element)
                    t.Actions.DeleteObject(slide, element);
                return Task.CompletedTask;
            },
        };

    private CommandDefinition EditCommand() =>
        Prototype("deck.table.edit") with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target?.Selected() is ObjectElement { Content: DataTableObject },
            Execute = (_, ct) => EditAsync(ct),
        };

    private async Task EditAsync(CancellationToken ct)
    {
        if (Context.Target is not { } t || t.CurrentSlide() is not { } slide) return;
        if (t.Selected() is not ObjectElement { Content: DataTableObject table } element) return;

        var edited = await dialogs.ShowAsync<PulseOffice.Data.UI.TableEdit>(
            new PulseOffice.Data.UI.TableEditorView(table, texts),
            new PulseOffice.UI.Controls.Services.DialogOptions { TitleKey = "command.deck.table.edit.title", PreferredWidth = 700 },
            ct).ConfigureAwait(true);

        if (edited is null || !Holds(t, slide, element)) return;

        t.Actions.ReplaceTable(slide, element.Id, edited.Snapshot, edited.Merges, edited.CellFormats);
    }

    private TableCellRange? CellsOf() =>
        Context.Target is { } t && t.Options.TableCells is { } cells
        && t.Selected() is ObjectElement { Content: DataTableObject } element
        && element.Id == cells.ElementId
            ? cells.Range
            : null;

    private (int First, int Last)? Rows(DataTableObject table) => CellsOf()?.DataRows(table.Snapshot.Rows.Count);

    private (int First, int Last)? Columns(DataTableObject table) => CellsOf()?.Columns(table.Snapshot.Columns.Count);

    private DataTableObject AddRow(DataTableObject table) =>
        structure.InsertRow(table, Rows(table) is { } rows ? rows.Last + 1 : table.Snapshot.Rows.Count);

    private bool CanRemoveRows(DataTableObject table)
    {
        var count = table.Snapshot.Rows.Count;
        var (first, last) = Rows(table) ?? (count - 1, count - 1);
        return count > 1 && last - first + 1 < count;
    }

    private bool CanRemoveColumns(DataTableObject table)
    {
        var count = table.Snapshot.Columns.Count;
        var (first, last) = Columns(table) ?? (count - 1, count - 1);
        return count > 1 && last - first + 1 < count;
    }

    private DataTableObject RemoveRow(DataTableObject table)
    {
        var snapshot = table.Snapshot;
        if (snapshot.Rows.Count <= 1) return table;
        var (first, last) = Rows(table) ?? (snapshot.Rows.Count - 1, snapshot.Rows.Count - 1);
        return last - first + 1 >= snapshot.Rows.Count ? table : structure.DeleteRows(table, first, last);
    }

    private DataTableObject AddColumn(DataTableObject table)
    {
        var snapshot = table.Snapshot;
        var at = Columns(table) is { } columns ? columns.Last + 1 : snapshot.Columns.Count;
        return structure.InsertColumn(table, at,
            new DataColumn($"{texts["deck.table.column"]} {snapshot.Columns.Count + 1}", DataValueKind.Text));
    }

    private DataTableObject RemoveColumn(DataTableObject table)
    {
        var snapshot = table.Snapshot;
        if (snapshot.Columns.Count <= 1) return table;
        var (first, last) = Columns(table) ?? (snapshot.Columns.Count - 1, snapshot.Columns.Count - 1);
        return last - first + 1 >= snapshot.Columns.Count ? table : structure.DeleteColumns(table, first, last);
    }

    private CommandDefinition Select(string id, Func<DataTableObject, TableCellRange, TableCellRange> extend) =>
        Prototype(id) with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            EnabledWhen = _ => Context.Target?.Selected() is ObjectElement { Content: DataTableObject },
            Execute = (_, _) =>
            {
                if (Context.Target is { } t && t.Selected() is ObjectElement { Content: DataTableObject table } element)
                {
                    var range = t.Options.TableCells is { } cells && cells.ElementId == element.Id ? cells.Range : new TableCellRange(0, 0, 0, 0);
                    t.Options.SetTableCells((element.Id, extend(table, range)));
                }
                return Task.CompletedTask;
            },
        };

    private void KeepCellsInside(DeckTarget target, Slide slide, string elementId)
    {
        if (target.Options.TableCells is not { } cells || cells.ElementId != elementId) return;
        if (slide.Find(elementId) is not ObjectElement { Content: DataTableObject table }) return;
        var clamped = cells.Range.ClampedTo(table.Snapshot.Rows.Count, table.Snapshot.Columns.Count);
        target.Options.SetTableCells(clamped is { } range ? (elementId, range) : null);
    }

    private CommandDefinition Table(string id, Func<DataTableObject, DataTableObject> change, Func<DataTableObject, bool>? applicable = null) =>
        Prototype(id) with
        {
            Surface = CommandSurface.Advanced | CommandSurface.ContextMenu,
            RequiredState = DocumentState.DocumentOpen,
            ForbiddenState = DocumentState.ReadOnly,
            EnabledWhen = _ => Context.Target?.Selected() is ObjectElement { Content: DataTableObject table }
                && (applicable?.Invoke(table) ?? true),
            Execute = (_, _) =>
            {
                if (Context.Target is { } t && t.CurrentSlide() is { } slide
                    && t.Selected() is ObjectElement { Content: DataTableObject } element)
                {
                    t.Actions.EditTable(slide, element.Id, change);
                    KeepCellsInside(t, slide, element.Id);
                }
                return Task.CompletedTask;
            },
        };
}

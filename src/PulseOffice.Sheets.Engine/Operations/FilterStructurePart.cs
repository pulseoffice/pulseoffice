using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Engine.Operations;

internal sealed class FiltersPart : StructurePart
{
    public override IEnumerable<CellRange> Blocks(Workbook workbook, int sheetIndex, Worksheet sheet) =>
    [
        .. sheet.AutoFilter.Range is { } automatic ? [automatic] : Array.Empty<CellRange>(),
        .. sheet.RangeFilter is { } own ? [own.Range] : Array.Empty<CellRange>(),
        .. workbook.DatabaseRanges.All.Where(database => database.SheetIndex == sheetIndex).Select(database => database.Range),
    ];

    public override void Apply(StructureEdit edit)
    {
        var sheet = edit.Sheet;
        var workbook = edit.Engine.Workbook;

        MoveAutoFilter(edit, sheet.AutoFilter);

        if (sheet.RangeFilter is { } own)
            sheet.RangeFilter = edit.Shift.Move(own.Range) is { } range
                && own.Filter.Moved(Fields(edit, own.Range, range), criteria => Criteria(edit, criteria)) is { } kept
                    ? own with { Range = range, Filter = kept }
                    : null;

        if (sheet.RangeSort is { } remembered)
            sheet.RangeSort = edit.Shift.Move(remembered.Range) is { } moved
                && remembered.Settings.Moved(Fields(edit, remembered.Range, moved, remembered.Settings.Orientation)) is { } sort
                    ? remembered with { Range = moved, Settings = sort }
                    : null;

        foreach (var other in workbook.Sheets.Where(other => !ReferenceEquals(other, sheet)))
            if (other.RangeFilter is { } filtered)
                other.RangeFilter = filtered.Filter.Moved(field => field, criteria => Criteria(edit, criteria)) is { } kept
                    ? filtered with { Filter = kept }
                    : null;

        foreach (var database in workbook.DatabaseRanges.All.ToList())
            workbook.DatabaseRanges.Define(Moved(edit, database));

        if (workbook.Consolidation is { } consolidation) workbook.Consolidation = Moved(edit, consolidation);
    }

    private ConsolidationSettings Moved(StructureEdit edit, ConsolidationSettings settings) => settings with
    {
        Sources = [.. settings.Sources
            .Select(area => area.SheetIndex != edit.SheetIndex ? area : edit.Shift.Move(area.Range) is { } range ? area with { Range = range } : null)
            .OfType<ConsolidationArea>()],
        Target = settings.TargetSheet == edit.SheetIndex && edit.Shift.Move(new CellRange(settings.Target, settings.Target)) is { } target
            ? target.Start
            : settings.Target,
    };

    private DatabaseRange Moved(StructureEdit edit, DatabaseRange database)
    {
        if (database.SheetIndex != edit.SheetIndex)
            return database.RangeFilter is { } other
                ? database with { RangeFilter = other.Moved(field => field, criteria => Criteria(edit, criteria)) }
                : database;

        if (edit.Shift.Move(database.Range) is not { } range) return database;

        var fields = Fields(edit, database.Range, range);
        return database with
        {
            Filters = Columns(database.Filters, fields),
            RangeFilter = database.RangeFilter?.Moved(fields, criteria => Criteria(edit, criteria)),
            Sort = database.Sort is { } sort ? sort.Moved(Fields(edit, database.Range, range, sort.Orientation)) : null,
        };
    }

    private void MoveAutoFilter(StructureEdit edit, AutoFilterState state)
    {
        if (state.Range is not { } before) return;

        if (edit.Shift.Move(before) is not { } after)
        {
            state.Deactivate();
            return;
        }

        var fields = Fields(edit, before, after);
        state.Restore(new AutoFilterMemento(after, state.HasHeader, Columns(state.Filters, fields)) { Sort = state.Sort?.Moved(fields) });
    }

    private IReadOnlyList<ColumnFilter> Columns(IReadOnlyList<ColumnFilter> filters, Func<int, int?> fields) =>
        [.. filters
            .Select(filter => fields(filter.ColumnOffset) is { } offset ? filter with { ColumnOffset = offset } : null)
            .OfType<ColumnFilter>()];

    private Func<int, int?> Fields(StructureEdit edit, CellRange before, CellRange after, SortOrientation orientation = SortOrientation.TopToBottom) =>
        (edit.Shift.Rows, orientation) switch
        {
            (true, SortOrientation.LeftToRight) => field => edit.Shift.Move(before.Start.Row + field) is { } row ? row - after.Start.Row : null,
            (false, SortOrientation.TopToBottom) => field => edit.Shift.Move(before.Start.Column + field) is { } column ? column - after.Start.Column : null,
            _ => field => field,
        };

    private AdvancedCriteria? Criteria(StructureEdit edit, AdvancedCriteria criteria) =>
        criteria.SheetIndex != edit.SheetIndex ? criteria
        : edit.Shift.Move(criteria.Range) is { } range ? criteria with { Range = range }
        : null;
}

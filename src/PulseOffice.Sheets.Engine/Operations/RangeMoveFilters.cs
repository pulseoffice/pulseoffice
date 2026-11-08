using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Engine.Operations;

internal sealed class MovableFilters
{
    public IReadOnlyList<CellRange> On(Workbook workbook, Worksheet sheet, int sheetIndex) =>
    [
        .. sheet.AutoFilter.Range is { } automatic ? [automatic] : Array.Empty<CellRange>(),
        .. sheet.RangeFilter is { } own ? [own.Range] : Array.Empty<CellRange>(),
        .. workbook.DatabaseRanges.All
            .Where(database => database.SheetIndex == sheetIndex && database.QueryBindingId is null)
            .Select(database => database.Range),
    ];

    public bool LandFree(Workbook workbook, RangeMove move, Worksheet source, int sourceIndex, Worksheet target, int targetIndex)
    {
        var targetArea = move.TargetArea;
        var carriesAutoFilter = source.AutoFilter.Range is { } automatic && move.Inside(automatic, move.Source);
        var carriesRangeFilter = source.RangeFilter is { } own && move.Inside(own.Range, move.Source);
        var carried = workbook.DatabaseRanges.All
            .Where(database => database.SheetIndex == sourceIndex && move.Inside(database.Range, move.Source))
            .Select(database => move.Moved(database.Range))
            .ToList();

        if (!ReferenceEquals(source, target))
        {
            if (carriesAutoFilter && target.AutoFilter.Range is { } existing && !move.Inside(existing, targetArea)) return false;
            if (carriesRangeFilter && target.RangeFilter is { } other && !move.Inside(other.Range, targetArea)) return false;
        }

        return !workbook.DatabaseRanges.All
            .Where(database => database.SheetIndex == targetIndex && !(database.SheetIndex == sourceIndex && move.Inside(database.Range, move.Source)))
            .Any(database => carried.Any(landed => landed.IntersectsWith(database.Range)));
    }
}

internal sealed class MovedFiltersPart : RangeMovePart
{
    public override void Apply(RangeMoveEdit edit)
    {
        var move = edit.Move;
        MoveAutoFilter(edit);
        MoveRangeFilter(edit);
        MoveRangeSort(edit);

        var workbook = edit.Workbook;
        foreach (var other in workbook.Sheets)
            if (other.RangeFilter is { } filtered && Criteria(edit, filtered.Filter) is var moved && moved != filtered.Filter)
                other.RangeFilter = moved is null ? null : filtered with { Filter = moved };

        foreach (var database in workbook.DatabaseRanges.All.ToList())
        {
            var carried = database.SheetIndex == edit.SourceIndex && database.QueryBindingId is null && move.Inside(database.Range, move.Source);
            var filter = database.RangeFilter is { } own ? Criteria(edit, own) : null;
            workbook.DatabaseRanges.Define(database with
            {
                SheetIndex = carried ? edit.TargetIndex : database.SheetIndex,
                Range = carried ? move.Moved(database.Range) : database.Range,
                RangeFilter = filter,
            });
        }

        if (workbook.Consolidation is { } consolidation) workbook.Consolidation = Moved(edit, consolidation);
    }

    private ConsolidationSettings Moved(RangeMoveEdit edit, ConsolidationSettings settings)
    {
        var move = edit.Move;
        var targetMoves = settings.TargetSheet == edit.SourceIndex && move.Source.Contains(settings.Target);
        return settings with
        {
            Sources = [.. settings.Sources.Select(area => area.SheetIndex == edit.SourceIndex && move.Inside(area.Range, move.Source)
                ? new ConsolidationArea(edit.TargetIndex, move.Moved(area.Range))
                : area)],
            TargetSheet = targetMoves ? edit.TargetIndex : settings.TargetSheet,
            Target = targetMoves ? move.Moved(settings.Target) : settings.Target,
        };
    }

    private void MoveAutoFilter(RangeMoveEdit edit)
    {
        var move = edit.Move;
        var state = edit.Source.AutoFilter;

        if (state.Range is { } range && move.Inside(range, move.Source))
        {
            var memento = state.Save() with { Range = move.Moved(range) };
            state.Deactivate();
            edit.Target.AutoFilter.Restore(memento);
        }
        else if (edit.Target.AutoFilter.Range is { } overwritten && move.Inside(overwritten, move.TargetArea))
            edit.Target.AutoFilter.Deactivate();
    }

    private void MoveRangeFilter(RangeMoveEdit edit)
    {
        var move = edit.Move;

        if (edit.Source.RangeFilter is { } own && move.Inside(own.Range, move.Source))
        {
            edit.Source.RangeFilter = null;
            edit.Target.RangeFilter = own with { Range = move.Moved(own.Range) };
        }
        else if (edit.Target.RangeFilter is { } overwritten && move.Inside(overwritten.Range, move.TargetArea))
            edit.Target.RangeFilter = null;
    }

    private void MoveRangeSort(RangeMoveEdit edit)
    {
        var move = edit.Move;

        if (edit.Source.RangeSort is { } sorted && move.Inside(sorted.Range, move.Source))
        {
            edit.Source.RangeSort = null;
            edit.Target.RangeSort = sorted with { Range = move.Moved(sorted.Range) };
        }
        else if (edit.Target.RangeSort is { } overwritten && overwritten.Range.IntersectsWith(move.TargetArea))
            edit.Target.RangeSort = null;
    }

    private RangeFilter? Criteria(RangeMoveEdit edit, RangeFilter filter) =>
        filter.Moved(field => field, criteria => Criteria(edit, criteria));

    private AdvancedCriteria? Criteria(RangeMoveEdit edit, AdvancedCriteria criteria) =>
        criteria.SheetIndex == edit.SourceIndex && edit.Move.Inside(criteria.Range, edit.Move.Source)
            ? new AdvancedCriteria(edit.TargetIndex, edit.Move.Moved(criteria.Range))
            : criteria;
}

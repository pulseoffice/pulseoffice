using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Recalculation;

internal sealed class SpillArea(Workbook workbook, Dictionary<SheetCell, CellValue> values, Evaluation.Functions.ValueCoercion coercion)
{
    private readonly Dictionary<SheetCell, CellRange> _ranges = [];

    private readonly Dictionary<SheetCell, SheetCell> _owners = [];

    private readonly Dictionary<SheetCell, CellRange> _blocked = [];

    private readonly HashSet<SheetCell> _loaded = [];

    public IReadOnlyDictionary<SheetCell, CellRange> Ranges => _ranges;

    public bool IsSpilledInto(SheetCell cell) => _owners.ContainsKey(cell);

    public int Bottom(int sheetIndex) =>
        _ranges.Where(pair => pair.Key.SheetIndex == sheetIndex).Select(pair => pair.Value.End.Row).DefaultIfEmpty(-1).Max();

    public int Right(int sheetIndex) =>
        _ranges.Where(pair => pair.Key.SheetIndex == sheetIndex).Select(pair => pair.Value.End.Column).DefaultIfEmpty(-1).Max();

    public SheetCell? Claim(SheetCell cell)
    {
        if (!_owners.Remove(cell, out var owner)) return null;
        values.Remove(cell);
        return owner;
    }

    public IReadOnlyList<SheetCell> UnblockedBy(IReadOnlyCollection<SheetCell> touched) =>
        [.. _blocked
            .Where(blocked => touched.Any(c => c.SheetIndex == blocked.Key.SheetIndex && blocked.Value.Contains(c.Address)))
            .Select(blocked => blocked.Key)];

    public void Reset()
    {
        var previous = _owners.Keys.ToList();
        _ranges.Clear();
        _owners.Clear();
        _blocked.Clear();
        CollectLoaded();
        _loaded.UnionWith(previous);
    }

    private void CollectLoaded()
    {
        _loaded.Clear();
        for (var index = 0; index < workbook.Sheets.Count; index++)
        {
            var sheet = workbook.Sheets[index];
            foreach (var (address, cell) in sheet.NonEmptyCells().Where(pair => pair.Value.IsSpilled).ToList())
            {
                _loaded.Add(new SheetCell(index, address));
                sheet[address] = cell with { IsSpilled = false };
            }
        }
    }

    public void ClearStale()
    {
        foreach (var cell in _loaded.Where(cell => !_owners.ContainsKey(cell)))
            if (SheetAt(cell.SheetIndex) is { } sheet && !sheet[cell.Address].HasFormula)
                sheet[cell.Address] = sheet[cell.Address] with { Value = CellValue.Empty };
        _loaded.Clear();

        for (var index = 0; index < workbook.Sheets.Count; index++)
        {
            var sheet = workbook.Sheets[index];
            foreach (var (address, cell) in sheet.NonEmptyCells().Where(pair => pair.Value.SpillRange is not null).ToList())
                if (!_ranges.ContainsKey(new SheetCell(index, address)))
                    sheet[address] = cell with { SpillRange = null };
        }
    }

    public CellValue Materialize(SheetCell cell, EvalValue result, IEvaluationContext context, HashSet<SheetCell> spilled)
    {
        var sheet = SheetAt(cell.SheetIndex);
        var declared = sheet?[cell.Address].ArrayFormulaRange;

        if (declared is { } area)
            return FillDeclaredArea(cell, area, result, context, spilled);

        if (!context.ImplicitIntersection && result is RangeValue { Range.IsSingleCell: false } range)
            result = new ArrayValue([.. Enumerable.Range(0, range.Range.RowCount).Select(row =>
                (IReadOnlyList<CellValue>)[.. Enumerable.Range(0, range.Range.ColumnCount).Select(column =>
                    context.GetCellValue(range.SheetName, range.Range.Start.Offset(row, column)))])]);

        if (context.ImplicitIntersection || result is not ArrayValue array || (array.RowCount <= 1 && array.ColumnCount <= 1))
        {
            Release(cell, spilled);
            var scalar = coercion.ToScalar(result, context);
            return scalar.IsEmpty ? CellValue.Number(0) : scalar;
        }

        return Spill(cell, array, spilled);
    }

    private CellValue FillDeclaredArea(
        SheetCell cell, CellRange area, EvalValue result, IEvaluationContext context, HashSet<SheetCell> spilled)
    {
        var sheet = SheetAt(cell.SheetIndex);
        if (sheet is null || cell.Address != area.Start)
            return coercion.ToScalar(result, context);

        var array = result as ArrayValue;

        for (var row = 0; row < area.RowCount; row++)
            for (var column = 0; column < area.ColumnCount; column++)
            {
                var target = new SheetCell(cell.SheetIndex,
                    new CellAddress(area.Start.Row + row, area.Start.Column + column));

                var value = array is null
                    ? coercion.ToScalar(result, context)
                    : ElementAt(array, row, column);

                if (target != cell)
                {
                    sheet[target.Address] = sheet[target.Address] with { Value = value };
                    values[target] = value;
                    spilled.Add(target);
                }
                else
                {
                    values[target] = value;
                }
            }

        return values[cell];
    }

    private CellValue Spill(SheetCell anchor, ArrayValue array, HashSet<SheetCell> spilled)
    {
        var sheet = SheetAt(anchor.SheetIndex);
        if (sheet is null) return CellValue.FromError(CellError.Reference);

        var end = new CellAddress(
            anchor.Address.Row + array.RowCount - 1,
            anchor.Address.Column + array.ColumnCount - 1);

        if (!end.IsValid) return CellValue.FromError(CellError.Reference);

        var target = new CellRange(anchor.Address, end);
        _blocked.Remove(anchor);

        Release(anchor, spilled);

        foreach (var address in target.Cells())
        {
            if (address == anchor.Address) continue;
            var occupant = new SheetCell(anchor.SheetIndex, address);

            var existing = sheet[address];
            var ownedByOthers = _owners.TryGetValue(occupant, out var owner) && owner != anchor;

            if (existing.HasFormula || (!existing.Value.IsEmpty && !_loaded.Contains(occupant)) || ownedByOthers)
            {
                _blocked[anchor] = target;
                return CellValue.FromError(CellError.Spill);
            }
        }

        for (var row = 0; row < array.RowCount; row++)
            for (var column = 0; column < array.ColumnCount; column++)
            {
                var address = new CellAddress(anchor.Address.Row + row, anchor.Address.Column + column);
                var value = ElementAt(array, row, column);
                var occupant = new SheetCell(anchor.SheetIndex, address);

                if (address == anchor.Address) continue;

                sheet[address] = sheet[address] with { Value = value };
                values[occupant] = value;
                _owners[occupant] = anchor;
                spilled.Add(occupant);
            }

        _ranges[anchor] = target;
        sheet[anchor.Address] = sheet[anchor.Address] with { SpillRange = target };
        return ElementAt(array, 0, 0);
    }

    public void Release(SheetCell anchor, HashSet<SheetCell>? spilled = null)
    {
        if (!_ranges.TryGetValue(anchor, out var previous)) return;

        var sheet = SheetAt(anchor.SheetIndex);
        foreach (var address in previous.Cells())
        {
            if (address == anchor.Address) continue;
            var occupant = new SheetCell(anchor.SheetIndex, address);

            if (!_owners.TryGetValue(occupant, out var owner) || owner != anchor) continue;

            _owners.Remove(occupant);
            values.Remove(occupant);
            if (sheet is not null) sheet[address] = sheet[address] with { Value = CellValue.Empty };
            spilled?.Add(occupant);
        }

        _ranges.Remove(anchor);
        if (sheet is not null && sheet[anchor.Address].SpillRange is not null)
            sheet[anchor.Address] = sheet[anchor.Address] with { SpillRange = null };
    }

    private static CellValue ElementAt(ArrayValue array, int row, int column)
    {
        if (row >= array.Rows.Count) return CellValue.FromError(CellError.NotAvailable);
        var line = array.Rows[row];

        var value = line.Count == 1 && column > 0 ? line[0]
            : column < line.Count ? line[column] : CellValue.FromError(CellError.NotAvailable);
        return value.IsEmpty ? CellValue.Number(0) : value;
    }

    private Worksheet? SheetAt(int index) =>
        index >= 0 && index < workbook.Sheets.Count ? workbook.Sheets[index] : null;
}

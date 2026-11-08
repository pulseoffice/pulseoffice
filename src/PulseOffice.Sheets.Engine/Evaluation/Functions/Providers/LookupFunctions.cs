using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using M = System.Math;
using F = PulseOffice.Sheets.Engine.Evaluation.Functions.FunctionBuilder;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class LookupFunctions(IRangeMaterializer ranges) : IFunctionProvider
{
    private readonly SortedVectorSearch _sortedSearch = new(StringComparison.CurrentCultureIgnoreCase);

    public string Category => "Ricerca e riferimento";

    public IEnumerable<FunctionDefinition> GetFunctions() => new FunctionBuilder(Category)
        .Raw("VLOOKUP", 3, 4, r => Lookup(r, byColumn: true))
        .Raw("HLOOKUP", 3, 4, r => Lookup(r, byColumn: false))

        .Raw("LOOKUP", 2, 3, r =>
        {
            var needle = r.Value(0);
            if (r.HasError) return r.Error;
            if (needle.IsEmpty) return CellValue.FromError(CellError.NotAvailable);

            var resultSource = r.Has(2) ? r.Raw(2) : null;
            if (resultSource is RangeValue { Range: { RowCount: > 1, ColumnCount: > 1 } }) return CellValue.FromError(CellError.Value);
            var resultGrid = resultSource is null or RangeValue ? null : ranges.Materialize(resultSource, r.Context);
            if (resultGrid is { RowCount: > 1, ColumnCount: > 1 }) return CellValue.FromError(CellError.Value);

            var searchGrid = ranges.Materialize(r.Raw(1), r.Context);
            if (searchGrid.IsEmpty) return CellValue.FromError(CellError.NotAvailable);

            var vertical = searchGrid.RowCount >= searchGrid.ColumnCount;
            var haystack = (vertical ? searchGrid.Column(0) : searchGrid.Row(0)).ToList();

            var index = _sortedSearch.LastNotGreater(haystack, needle);
            if (index < 0) return CellValue.FromError(CellError.NotAvailable);

            if (resultSource is null)
                return vertical
                    ? searchGrid[index, searchGrid.ColumnCount - 1]
                    : searchGrid[searchGrid.RowCount - 1, index];

            if (resultSource is RangeValue reference)
            {
                var start = reference.Range.Start;
                var target = reference.Range.RowCount > 1
                    ? new CellAddress(start.Row + index, start.Column)
                    : new CellAddress(start.Row, start.Column + index);
                if (target.Row >= CellAddress.MaxRows || target.Column >= CellAddress.MaxColumns) return CellValue.Number(0);
                var cell = ranges.Materialize(reference with { Range = CellRange.Single(target) }, r.Context);
                return cell.IsEmpty ? CellValue.Empty : cell[0, 0];
            }

            var results = (resultGrid!.RowCount >= resultGrid.ColumnCount ? resultGrid.Column(0) : resultGrid.Row(0)).ToList();
            return index < results.Count ? results[index] : CellValue.FromError(CellError.NotAvailable);
        })

        .Raw("MATCH", 2, 3, r =>
        {
            var needle = r.Value(0);
            var mode = M.Sign(r.Number(2, 1));
            if (r.HasError) return r.Error;

            var grid = ranges.Materialize(r.Raw(1), r.Context);
            if (grid.IsEmpty) return CellValue.FromError(CellError.NotAvailable);

            var values = grid.InReadingOrder().ToList();

            var index = mode switch
            {
                0 => FindExactWithWildcards(values, needle, r.Context.TextRules.ForQueries),
                1 => FindApproximate(values, needle, r.Context.TextRules.ForQueries),
                -1 => FindApproximateDescending(values, needle, r.Context.TextRules.ForQueries),
                _ => -1,
            };

            return index < 0 ? CellValue.FromError(CellError.NotAvailable) : CellValue.Number(index + 1);
        })

        .ArrayResult("INDEX", 1, 4, Index)

        .ArrayResult("CHOOSE", 2, int.MaxValue, r =>
        {
            var index = r.Integer(0);
            if (r.HasError) return EvalValue.Scalar(r.Error);
            if (index < 1 || index >= r.Count) return EvalValue.Error(CellError.Number);
            return r.Raw(index);
        }, handlesErrors: true)

        .Raw("ROWS", 1, 1, r => r.Raw(0) switch
        {
            RangeValue range => CellValue.Number(range.Range.RowCount),
            ReferenceListValue => CellValue.FromError(CellError.Value),
            var other => CellValue.Number(ranges.Materialize(other, r.Context).RowCount),
        })

        .Raw("COLUMNS", 1, 1, r => r.Raw(0) switch
        {
            RangeValue range => CellValue.Number(range.Range.ColumnCount),
            ReferenceListValue => CellValue.FromError(CellError.Value),
            var other => CellValue.Number(ranges.Materialize(other, r.Context).ColumnCount),
        })

        .Raw("ADDRESS", 2, 5, r =>
        {
            var row = r.Integer(0);
            var column = r.Integer(1);
            var kind = r.Integer(2, 1);
            var a1 = r.Boolean(3, true);
            var sheet = r.Has(4) ? r.Text(4) : null;
            if (r.HasError) return r.Error;
            if (kind is < 1 or > 4) return F.Invalid();
            if (row < 1 || column < 1) return CellValue.FromError(CellError.Number);

            var absoluteRow = kind is 1 or 2;
            var absoluteColumn = kind is 1 or 3;

            var address = a1
                ? new CellAddress(row - 1, column - 1).ToA1(absoluteColumn, absoluteRow)
                : R1C1(row, column, absoluteRow, absoluteColumn);
            if (string.IsNullOrEmpty(sheet)) return CellValue.Text(address);
            return CellValue.Text($"{QuoteSheet(sheet)}{(a1 ? "." : "!")}{address}");
        })

        .Build();

    private EvalValue Index(ArgumentReader r)
    {
        var source = r.Raw(0);
        var area = r.Integer(3, 1);
        if (r.HasError) return EvalValue.Scalar(r.Error);
        if (source is ReferenceListValue list)
        {
            if (area < 1 || area > list.Areas.Count) return EvalValue.Error(CellError.Reference);
            source = list.Areas[area - 1];
        }
        else if (area != 1) return EvalValue.Error(CellError.Reference);

        return source is RangeValue reference ? IndexReference(r, reference) : IndexValues(r, source);
    }

    private static EvalValue IndexReference(ArgumentReader r, RangeValue reference)
    {
        var row = r.Integer(1, 0);
        var column = r.Integer(2, 0);
        if (r.HasError) return EvalValue.Scalar(r.Error);
        var rows = reference.Range.RowCount;
        var columns = reference.Range.ColumnCount;

        if (!r.Has(2) && rows == 1 && row > 0)
            (row, column) = (1, row);
        else if (!r.Has(2) && columns == 1)
            column = 1;

        if (row < 0 || column < 0) return EvalValue.Error(CellError.Value);
        if (row > rows || column > columns) return EvalValue.Error(CellError.Reference);

        var start = reference.Range.Start;
        var top = row == 0 ? 0 : row - 1;
        var bottom = row == 0 ? rows - 1 : row - 1;
        var left = column == 0 ? 0 : column - 1;
        var right = column == 0 ? columns - 1 : column - 1;
        return reference with
        {
            Range = new CellRange(new CellAddress(start.Row + top, start.Column + left), new CellAddress(start.Row + bottom, start.Column + right)),
        };
    }

    private EvalValue IndexValues(ArgumentReader r, EvalValue source)
    {
        var grid = ranges.Materialize(source, r.Context);
        var row = r.Integer(1, 0);
        var column = r.Integer(2, 0);
        if (r.HasError) return EvalValue.Scalar(r.Error);
        if (grid.IsEmpty) return EvalValue.Error(CellError.Reference);

        if (!r.Has(2) && grid.RowCount == 1 && row > 0)
            (row, column) = (1, row);
        else if (!r.Has(2) && grid.ColumnCount == 1)
            column = 1;

        if (row < 0 || column < 0) return EvalValue.Error(CellError.Value);
        if (row > grid.RowCount || column > grid.ColumnCount) return EvalValue.Error(CellError.Reference);

        if (row > 0 && column > 0) return EvalValue.Scalar(grid[row - 1, column - 1]);

        var firstRow = row == 0 ? 0 : row - 1;
        var lastRow = row == 0 ? grid.RowCount - 1 : row - 1;
        var firstColumn = column == 0 ? 0 : column - 1;
        var lastColumn = column == 0 ? grid.ColumnCount - 1 : column - 1;

        var rows = new List<IReadOnlyList<CellValue>>();
        for (var y = firstRow; y <= lastRow; y++)
        {
            var cells = new List<CellValue>();
            for (var x = firstColumn; x <= lastColumn; x++) cells.Add(grid[y, x]);
            rows.Add(cells);
        }
        return new ArrayValue(rows);
    }

    private static string R1C1(int row, int column, bool absoluteRow, bool absoluteColumn) =>
        $"R{(absoluteRow ? row.ToString(System.Globalization.CultureInfo.InvariantCulture) : $"[{row}]")}" +
        $"C{(absoluteColumn ? column.ToString(System.Globalization.CultureInfo.InvariantCulture) : $"[{column}]")}";

    private static string QuoteSheet(string sheet)
    {
        var plain = !char.IsDigit(sheet[0]) && sheet.All(c => char.IsLetterOrDigit(c) || c == '_');
        return plain ? sheet : $"'{sheet.Replace("'", "''", StringComparison.Ordinal)}'";
    }

    private static int FindExactWithWildcards(IReadOnlyList<CellValue> values, CellValue needle, TextRules rules)
    {
        if (needle.Kind == ValueKind.Text)
        {
            if (rules.Pattern(needle.AsText(), anchored: true) is { } pattern)
            {
                for (var i = 0; i < values.Count; i++)
                    if (values[i].Kind == ValueKind.Text && pattern.IsMatch(values[i].AsText())) return i;
                return -1;
            }
        }
        return FindExact(values, needle, rules);
    }

    private CellValue Lookup(ArgumentReader r, bool byColumn)
    {
        var needle = r.Value(0);
        var index = r.Integer(2);
        var approximate = r.Boolean(3, true);
        if (r.HasError) return r.Error;

        var table = r.Raw(1);
        if (table is RangeValue range) return LookupInRange(range, r.Context, needle, index, approximate, byColumn);

        var grid = ranges.Materialize(table, r.Context);
        if (grid.IsEmpty) return CellValue.FromError(CellError.NotAvailable);

        var limit = byColumn ? grid.ColumnCount : grid.RowCount;
        if (index < 1) return F.Invalid();
        if (index > limit) return CellValue.FromError(CellError.Reference);

        var haystack = (byColumn ? grid.Column(0) : grid.Row(0)).ToList();
        var position = approximate ? FindApproximate(haystack, needle, r.Context.TextRules.ForQueries) : FindExactWithWildcards(haystack, needle, r.Context.TextRules.ForQueries);
        if (position < 0) return CellValue.FromError(CellError.NotAvailable);

        return byColumn ? grid[position, index - 1] : grid[index - 1, position];
    }

    private CellValue LookupInRange(RangeValue table, IEvaluationContext context, CellValue needle, int index, bool approximate, bool byColumn)
    {
        var area = table.Range;
        var limit = byColumn ? area.ColumnCount : area.RowCount;
        if (index < 1) return F.Invalid();
        if (index > limit) return CellValue.FromError(CellError.Reference);

        var keys = byColumn
            ? new CellRange(area.Start, new CellAddress(area.End.Row, area.Start.Column))
            : new CellRange(area.Start, new CellAddress(area.Start.Row, area.End.Column));
        var content = context.ContentWithin(table.SheetName, keys);
        var haystack = content is { } stored
            ? ranges.Materialize(table with { Range = stored }, context).InReadingOrder().ToList()
            : [];
        var position = approximate ? FindApproximate(haystack, needle, context.TextRules.ForQueries) : FindExactWithWildcards(haystack, needle, context.TextRules.ForQueries);
        if (position < 0) return CellValue.FromError(CellError.NotAvailable);

        var target = byColumn
            ? new CellAddress(area.Start.Row + position, area.Start.Column + index - 1)
            : new CellAddress(area.Start.Row + index - 1, area.Start.Column + position);
        return context.GetCellValue(table.SheetName, target);
    }

    private static int FindExact(IReadOnlyList<CellValue> values, CellValue needle, TextRules rules)
    {
        for (var i = 0; i < values.Count; i++)
            if (AreEqual(values[i], needle, rules)) return i;
        return -1;
    }

    private static int FindApproximate(IReadOnlyList<CellValue> values, CellValue needle, TextRules rules) =>
        FindApproximate(values, needle, rules.Comparison);

    private static int FindApproximate(IReadOnlyList<CellValue> values, CellValue needle, StringComparison comparison)
    {
        var best = -1;
        for (var i = 0; i < values.Count; i++)
        {
            var order = CompareForLookup(values[i], needle, comparison);
            if (order is null) continue;
            if (order <= 0) best = i;
            else break;
        }
        return best;
    }

    private static int FindApproximateDescending(IReadOnlyList<CellValue> values, CellValue needle, TextRules rules)
    {
        var best = -1;
        for (var i = 0; i < values.Count; i++)
        {
            var order = CompareForLookup(values[i], needle, rules);
            if (order is null) continue;
            if (order >= 0) best = i;
            else break;
        }
        return best;
    }

    private static bool AreEqual(CellValue a, CellValue b, TextRules rules)
    {
        if (a.IsNumeric && b.IsNumeric) return Numerics.SpreadsheetMath.ApproxCompare(a.AsNumber(), b.AsNumber()) == 0;
        if (a.Kind == ValueKind.Text && b.Kind == ValueKind.Text)
            return string.Equals(a.AsText(), b.AsText(), rules.Comparison);
        return a.IsEmpty && b.IsEmpty;
    }

    private static int? CompareForLookup(CellValue value, CellValue needle, TextRules rules) =>
        CompareForLookup(value, needle, rules.Comparison);

    private static int? CompareForLookup(CellValue value, CellValue needle, StringComparison comparison)
    {
        if (value.IsNumeric && needle.IsNumeric) return value.AsNumber().CompareTo(needle.AsNumber());
        if (value.Kind == ValueKind.Text && needle.Kind == ValueKind.Text)
            return string.Compare(value.AsText(), needle.AsText(), comparison);
        return null;
    }
}

public sealed class SortedVectorSearch(StringComparison comparison)
{
    public int LastNotGreater(IReadOnlyList<CellValue> values, CellValue needle)
    {
        if (values.Count == 0) return -1;
        var delta = -1;
        var found = false;
        int first = 0, last = values.Count - 1;
        for (var length = last - first; length > 0; length = last - first)
        {
            var middle = first + length / 2;
            var order = Compare(values[middle], needle);
            if (order == 0)
            {
                while (middle + 1 < values.Count && Compare(values[middle + 1], needle) == 0) middle++;
                delta = middle;
                found = true;
                break;
            }
            if (length == 1)
            {
                delta = order < 0 ? last - 1 : first - 1;
                found = delta >= 0;
                break;
            }
            if (order < 0) first = middle;
            else last = middle;
        }

        if (delta == values.Count - 2)
        {
            if (Compare(values[delta + 1], needle) <= 0)
            {
                delta++;
                found = true;
            }
        }
        else if (delta > 0)
        {
            found = true;
        }

        if (!found || delta < 0) return -1;
        var hit = values[delta];
        var byText = needle.Kind == ValueKind.Text;
        return byText == (hit.Kind == ValueKind.Text) && !hit.IsEmpty ? delta : -1;
    }

    private int Compare(CellValue value, CellValue needle)
    {
        if (value.IsEmpty) return 1;
        var valueIsText = value.Kind == ValueKind.Text;
        var needleIsText = needle.Kind == ValueKind.Text;
        if (valueIsText != needleIsText) return valueIsText ? 1 : -1;
        if (valueIsText) return M.Sign(string.Compare(value.AsText(), needle.AsText(), comparison));
        if (value.IsError) return 1;
        return value.AsNumber().CompareTo(needle.AsNumber());
    }
}

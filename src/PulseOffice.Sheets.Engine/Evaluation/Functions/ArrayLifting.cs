using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions;

public sealed class ArrayLifting
{
    private readonly ValueCoercion _coercion = new();

    public EvalValue Apply(
        IReadOnlyList<EvalValue> arguments, IEvaluationContext context, Func<int, bool> isScalarPosition,
        Func<IReadOnlyList<EvalValue>, EvalValue> body)
    {
        var lifted = new Dictionary<int, IReadOnlyList<IReadOnlyList<CellValue>>>();
        for (var index = 0; index < arguments.Count; index++)
        {
            if (!isScalarPosition(index) || (context.ImplicitIntersection && arguments[index] is RangeValue)) continue;
            if (arguments[index] is RangeValue { Range: var area } && !ResultLimits.ArrayFits(area.RowCount, area.ColumnCount))
                return EvalValue.Error(CellError.Number);
            if (Rows(arguments[index], context) is { } rows) lifted[index] = rows;
        }
        if (lifted.Count == 0) return body(arguments);
        context.NoteArrayUse();

        var height = lifted.Values.Max(rows => rows.Count);
        var width = lifted.Values.Max(rows => rows.Count == 0 ? 0 : rows[0].Count);
        if (!ResultLimits.ArrayFits(height, width)) return EvalValue.Error(CellError.Number);
        var result = new List<IReadOnlyList<CellValue>>(height);
        var element = arguments.ToArray();
        for (var row = 0; row < height; row++)
        {
            var line = new List<CellValue>(width);
            for (var column = 0; column < width; column++)
            {
                foreach (var (index, rows) in lifted) element[index] = EvalValue.Scalar(At(rows, row, column));
                line.Add(_coercion.ToScalar(body(element), context));
            }
            result.Add(line);
        }
        return new ArrayValue(result);
    }

    private IReadOnlyList<IReadOnlyList<CellValue>>? Rows(EvalValue value, IEvaluationContext context)
    {
        switch (value)
        {
            case ArrayValue array when array.RowCount > 1 || array.ColumnCount > 1:
                return array.Rows;
            case RangeValue range when !range.Range.IsSingleCell:
                var rows = new List<IReadOnlyList<CellValue>>(range.Range.RowCount);
                for (var row = range.Range.Start.Row; row <= range.Range.End.Row; row++)
                {
                    var line = new List<CellValue>(range.Range.ColumnCount);
                    for (var column = range.Range.Start.Column; column <= range.Range.End.Column; column++)
                        line.Add(context.GetCellValue(range.SheetName, new CellAddress(row, column)));
                    rows.Add(line);
                }
                return rows;
            default:
                return null;
        }
    }

    private static CellValue At(IReadOnlyList<IReadOnlyList<CellValue>> rows, int row, int column)
    {
        var y = rows.Count == 1 ? 0 : row;
        if (y >= rows.Count) return CellValue.FromError(CellError.NotAvailable);
        var line = rows[y];
        var x = line.Count == 1 ? 0 : column;
        return x < line.Count ? line[x] : CellValue.FromError(CellError.NotAvailable);
    }
}

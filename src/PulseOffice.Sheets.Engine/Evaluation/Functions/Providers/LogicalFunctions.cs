using PulseOffice.Sheets.Values;
using F = PulseOffice.Sheets.Engine.Evaluation.Functions.FunctionBuilder;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class LogicalFunctions(IRangeMaterializer ranges) : IFunctionProvider
{
    public string Category => "Logica";

    public IEnumerable<FunctionDefinition> GetFunctions() => new FunctionBuilder(Category)
        .Constant("TRUE", () => CellValue.Boolean(true))
        .Constant("FALSE", () => CellValue.Boolean(false))

        .ArrayResult("IF", 1, 3, r =>
        {
            var condition = r.Raw(0);
            if (IsMultiValued(condition) && !r.Context.ImplicitIntersection)
            {
                r.Context.NoteArrayUse();
                return ElementwiseIf(r, ranges.Materialize(condition, r.Context));
            }

            var test = r.Value(0);
            if (test.IsError) return EvalValue.Scalar(test);
            if (!test.TryCoerceToNumber(out var number)) return EvalValue.Scalar(F.Invalid());
            return Branch(r, number != 0);
        }, handlesErrors: true)

        .RawHandlingErrors("IFS", 2, int.MaxValue, r =>
        {
            for (var i = 0; i + 1 < r.Count; i += 2)
            {
                var condition = r.Value(i);
                if (condition.IsError) return condition;
                if (!condition.TryCoerceToNumber(out var test)) return F.Invalid();
                if (test != 0) return r.Value(i + 1);
            }
            return CellValue.FromError(CellError.NotAvailable);
        })

        .Raw("AND", 1, int.MaxValue, r => Combine(r, requireAll: true))
        .Raw("OR", 1, int.MaxValue, r => Combine(r, requireAll: false))

        .Raw("XOR", 1, int.MaxValue, r =>
        {
            var trueCount = 0;
            var any = false;
            foreach (var value in LogicalOperands(r))
            {
                if (value.IsError) return value;
                if (value.IsEmpty || value.Kind == ValueKind.Text) continue;
                if (!value.TryCoerceToNumber(out var n)) return F.Invalid();
                any = true;
                if (n != 0) trueCount++;
            }
            return any ? CellValue.Boolean(trueCount % 2 == 1) : F.Invalid();
        })

        .Raw("NOT", 1, 1, r =>
        {
            var value = r.Value(0);
            if (value.IsError) return value;
            return value.TryCoerceToNumber(out var n) ? CellValue.Boolean(n == 0) : F.Invalid();
        })

        .RawHandlingErrors("IFERROR", 2, 2, r =>
        {
            var value = r.Value(0);
            return value.IsError ? r.Value(1) : value;
        })

        .RawHandlingErrors("IFNA", 2, 2, r =>
        {
            var value = r.Value(0);
            return value.IsError && value.Error == CellError.NotAvailable ? r.Value(1) : value;
        })

        .Build();

    private static CellValue Combine(ArgumentReader r, bool requireAll)
    {
        var any = false;
        var accumulated = requireAll;

        foreach (var value in LogicalOperands(r))
        {
            if (value.IsError) return value;
            if (value.IsEmpty || value.Kind == ValueKind.Text) continue;
            if (!value.TryCoerceToNumber(out var n)) return F.Invalid();

            any = true;
            var flag = n != 0;
            accumulated = requireAll ? accumulated && flag : accumulated || flag;

            if (requireAll && !accumulated) return CellValue.Boolean(false);
            if (!requireAll && accumulated) return CellValue.Boolean(true);
        }

        return any ? CellValue.Boolean(accumulated) : F.Invalid();
    }

    private static IEnumerable<CellValue> LogicalOperands(ArgumentReader r)
    {
        for (var index = 0; index < r.Count; index++)
        {
            if (r.Raw(index) is ScalarValue { Value: { Kind: ValueKind.Text } text })
            {
                yield return bool.TryParse(text.AsText().Trim(), out var flag) ? CellValue.Boolean(flag) : F.Invalid();
                continue;
            }
            foreach (var value in r.Values(index)) yield return value;
        }
    }

    private static bool IsMultiValued(EvalValue value) => value switch
    {
        RangeValue range => !range.Range.IsSingleCell,
        ArrayValue array => array.RowCount > 1 || array.ColumnCount > 1,
        _ => false,
    };

    private static EvalValue Branch(ArgumentReader r, bool chosen) =>
        r.Count == 1 ? EvalValue.Scalar(CellValue.Boolean(chosen))
        : chosen ? r.Raw(1)
        : r.Has(2) ? r.Raw(2)
        : EvalValue.Scalar(CellValue.Boolean(false));

    private EvalValue ElementwiseIf(ArgumentReader r, ValueGrid condition)
    {
        var whenTrue = ranges.Materialize(r.Raw(1), r.Context);
        var whenFalse = r.Has(2) ? ranges.Materialize(r.Raw(2), r.Context) : null;
        var rows = new List<IReadOnlyList<CellValue>>(condition.RowCount);
        for (var row = 0; row < condition.RowCount; row++)
        {
            var line = new List<CellValue>(condition.ColumnCount);
            for (var column = 0; column < condition.ColumnCount; column++)
            {
                var test = condition[row, column];
                line.Add(test.IsError ? test
                    : !test.TryCoerceToNumber(out var number) ? F.Invalid()
                    : number != 0 ? Broadcast(whenTrue, row, column)
                    : whenFalse is null ? CellValue.Boolean(false) : Broadcast(whenFalse, row, column));
            }
            rows.Add(line);
        }
        return new ArrayValue(rows);
    }

    private static CellValue Broadcast(ValueGrid grid, int row, int column)
    {
        if (grid.IsEmpty) return CellValue.Empty;
        var y = grid.RowCount == 1 ? 0 : row;
        var x = grid.ColumnCount == 1 ? 0 : column;
        return y < grid.RowCount && x < grid.ColumnCount ? grid[y, x] : CellValue.FromError(CellError.NotAvailable);
    }
}

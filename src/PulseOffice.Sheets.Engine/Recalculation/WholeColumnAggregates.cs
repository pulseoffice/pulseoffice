using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation;
using PulseOffice.Sheets.Engine.Parsing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Recalculation;

internal sealed class WholeColumnAggregates(Evaluator evaluator, Func<string?, int, int> resolveSheet)
{
    private const int BlockRows = 256;

    private readonly Dictionary<SheetCell, AggregatePlan> _plans = [];

    private readonly IReadOnlyList<AggregateShape> _shapes =
    [
        new PlainAggregateShape("SUM", countsBlanks: false),
        new PlainAggregateShape("COUNT", countsBlanks: false),
        new PlainAggregateShape("COUNTA", countsBlanks: false),
        new ConditionalAggregateShape("SUMIFS", leadingRange: true, countsBlanks: false),
        new ConditionalAggregateShape("COUNTIFS", leadingRange: false, countsBlanks: true),
    ];

    public EvalValue? TryEvaluate(SheetCell cell, FormulaNode formula, IEvaluationContext context)
    {
        if (!_plans.TryGetValue(cell, out var plan) || !ReferenceEquals(plan.Formula, formula)
            || resolveSheet(plan.SheetName, cell.SheetIndex) != plan.SheetIndex)
        {
            _plans.Remove(cell);
            if (formula is not FunctionNode function || Plan(cell, function) is not { } created) return null;
            _plans[cell] = plan = created;
        }

        return plan.Evaluate(evaluator, context);
    }

    public void Forget(SheetCell cell) => _plans.Remove(cell);

    public void Clear() => _plans.Clear();

    public void Invalidate(SheetCell changed)
    {
        foreach (var plan in _plans.Values) plan.Invalidate(changed);
    }

    private AggregatePlan? Plan(SheetCell cell, FunctionNode function)
    {
        var shape = _shapes.FirstOrDefault(candidate => candidate.Matches(function));
        if (shape is null) return null;
        var ranges = function.Arguments.OfType<RangeNode>().ToList();
        var sheets = ranges.Select(range => resolveSheet(range.SheetName, cell.SheetIndex)).Distinct().ToList();
        if (sheets is not [var sheet] || sheet < 0) return null;
        var first = ranges.Min(range => range.Range.Start.Column);
        var last = ranges.Max(range => range.Range.End.Column);
        return new AggregatePlan(function, sheet, first, last, ranges[0].SheetName, shape.CountsBlanks);
    }

    private abstract class AggregateShape(string name, bool countsBlanks)
    {
        public bool CountsBlanks => countsBlanks;

        public bool Matches(FunctionNode function) =>
            string.Equals(function.Name, name, StringComparison.OrdinalIgnoreCase) && Accepts(function.Arguments);

        protected abstract bool Accepts(IReadOnlyList<FormulaNode> arguments);

        protected static bool IsWholeColumn(FormulaNode node) =>
            node is RangeNode { SpansSheets: false, Range.IsWholeColumns: true };

        protected static bool IsConstant(FormulaNode node) => node is LiteralNode { Value.IsError: false };
    }

    private sealed class PlainAggregateShape(string name, bool countsBlanks) : AggregateShape(name, countsBlanks)
    {
        protected override bool Accepts(IReadOnlyList<FormulaNode> arguments) =>
            arguments.Count > 0 && arguments.All(IsWholeColumn);
    }

    private sealed class ConditionalAggregateShape(string name, bool leadingRange, bool countsBlanks) : AggregateShape(name, countsBlanks)
    {
        protected override bool Accepts(IReadOnlyList<FormulaNode> arguments)
        {
            var offset = leadingRange ? 1 : 0;
            if (leadingRange && (arguments.Count == 0 || !IsWholeColumn(arguments[0]))) return false;
            var pairs = arguments.Count - offset;
            if (pairs < 2 || pairs % 2 != 0) return false;
            for (var index = offset; index < arguments.Count; index += 2)
                if (!IsWholeColumn(arguments[index]) || !IsConstant(arguments[index + 1])) return false;
            return true;
        }
    }

    private sealed class AggregatePlan(FunctionNode formula, int sheetIndex, int firstColumn, int lastColumn, string? sheetName, bool countsBlanks)
    {
        private readonly Dictionary<int, CellValue> _blocks = [];

        public FunctionNode Formula => formula;

        public string? SheetName => sheetName;

        public int SheetIndex => sheetIndex;

        public void Invalidate(SheetCell changed)
        {
            if (changed.SheetIndex != sheetIndex) return;
            var column = changed.Address.Column;
            if (column < firstColumn || column > lastColumn) return;
            _blocks.Remove(changed.Address.Row / BlockRows);
        }

        public EvalValue Evaluate(Evaluator evaluator, IEvaluationContext context)
        {
            var probe = new CellRange(new CellAddress(0, firstColumn), new CellAddress(CellAddress.MaxRows - 1, lastColumn));
            var used = context.ContentWithin(sheetName, probe);
            var blocks = used is { } extent ? extent.End.Row / BlockRows + 1 : 0;
            var total = 0.0;
            for (var block = 0; block < blocks; block++)
            {
                if (!_blocks.TryGetValue(block, out var partial))
                    _blocks[block] = partial = Partial(evaluator, context, block * BlockRows, block * BlockRows + BlockRows - 1);
                if (partial.IsError) return EvalValue.Scalar(partial);
                if (!partial.IsNumeric) return EvalValue.Error(CellError.Value);
                total += partial.AsNumber();
            }

            var tailStart = blocks * BlockRows;
            if (countsBlanks && tailStart < CellAddress.MaxRows)
            {
                var tail = Partial(evaluator, context, tailStart, CellAddress.MaxRows - 1);
                if (tail.IsError) return EvalValue.Scalar(tail);
                total += tail.AsNumber();
            }

            return double.IsFinite(total) ? EvalValue.Scalar(CellValue.Number(total)) : EvalValue.Error(CellError.Number);
        }

        private CellValue Partial(Evaluator evaluator, IEvaluationContext context, int firstRow, int lastRow)
        {
            var restricted = formula with
            {
                Arguments = [.. formula.Arguments.Select(argument => argument is RangeNode range
                    ? range with { Range = new CellRange(new CellAddress(firstRow, range.Range.Start.Column), new CellAddress(lastRow, range.Range.End.Column)) }
                    : argument)],
            };
            return evaluator.Evaluate(restricted, context);
        }
    }
}

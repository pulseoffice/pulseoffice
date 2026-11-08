using System.Globalization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Parsing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Evaluation;

public sealed class Evaluator(IFunctionRegistry functions, ISpecialFormRegistry specialForms, NumericText numbers, ArrayParameterClasses arrayParameters, INameExpansion names)
    : IEvaluationServices
{
    private const int MaxDepth = 64;
    private readonly Functions.ValueCoercion _coercion = new();

    private readonly INameExpansion _names = names;

    public INameExpansion Names => _names;

    public NumberToText NumberText { get; } = new(numbers.Culture);

    public NumericText TextNumbers => numbers;

    private readonly ThreadLocal<int> _callDepth = new();

    private readonly ThreadLocal<CellValue> _lastResult = new(() => CellValue.Number(0));

    public CellValue LastResult => _lastResult.Value;

    public CellValue Evaluate(FormulaNode node, IEvaluationContext context) =>
        _coercion.ToScalar(EvaluateRaw(node, context), context);

    public EvalValue EvaluateRaw(FormulaNode node, IEvaluationContext context)
    {
        var enclosingCallDepth = _callDepth.Value;
        var enclosingResult = _lastResult.Value;
        _lastResult.Value = CellValue.Number(0);
        try
        {
            return EvaluateNode(node, EvaluationScope.Empty, context, 0);
        }
        finally
        {
            _callDepth.Value = enclosingCallDepth;
            if (enclosingCallDepth > 0) _lastResult.Value = enclosingResult;
        }
    }

    EvalValue IEvaluationServices.Evaluate(FormulaNode node, EvaluationScope scope, IEvaluationContext context) =>
        EvaluateNode(node, scope, context, scope.Depth);

    public EvalValue Invoke(LambdaValue lambda, IReadOnlyList<EvalValue> arguments, IEvaluationContext context)
    {
        if (arguments.Count > lambda.Arity || arguments.Count < lambda.MinimumArguments) return EvalValue.Error(CellError.Value);
        if (_callDepth.Value >= MaxDepth) return EvalValue.Error(CellError.Circular);

        var scope = lambda.Captured.Extend(lambda.Arity);
        for (var i = 0; i < lambda.Arity; i++) scope.Bind(lambda.Parameters[i], i < arguments.Count ? arguments[i] : MissingValue.Instance);

        _callDepth.Value++;
        try
        {
            return EvaluateNode(lambda.Body, scope, context, scope.Depth);
        }
        finally
        {
            _callDepth.Value--;
        }
    }

    private EvalValue EvaluateNode(FormulaNode node, EvaluationScope scope, IEvaluationContext ctx, int depth)
    {
        if (depth > MaxDepth) return EvalValue.Error(CellError.Circular);

        var result = node.Accept(new NodeEvaluation(this, scope, ctx, depth));
        if (result is ScalarValue { Value: var scalar }) _lastResult.Value = scalar;
        return result;
    }

    private EvalValue EvaluateReference(ReferenceNode node, IEvaluationContext ctx)
    {
        if (node.SheetName is not null && !ctx.SheetExists(node.SheetName))
            return EvalValue.Error(CellError.Reference);
        if (!node.Address.IsValid)
            return EvalValue.Error(CellError.Reference);

        return new RangeValue(node.SheetName, CellRange.Single(node.Address));
    }

    private EvalValue EvaluateRange(RangeNode node, IEvaluationContext ctx)
    {
        if (node.SheetName is not null && !ctx.SheetExists(node.SheetName)) return EvalValue.Error(CellError.Reference);
        if (!node.SpansSheets) return new RangeValue(node.SheetName, node.Range);
        var sheets = ctx.SheetsBetween(node.SheetName!, node.LastSheetName!);
        return sheets.Count == 0
            ? EvalValue.Error(CellError.Reference)
            : new ReferenceListValue([.. sheets.Select(sheet => new RangeValue(sheet, node.Range))]);
    }

    private EvalValue EvaluateReferenceConcat(BinaryNode node, EvaluationScope scope, IEvaluationContext ctx, int depth)
    {
        var areas = new List<RangeValue>();
        foreach (var operand in (FormulaNode[])[node.Left, node.Right])
        {
            switch (EvaluateNode(operand, scope, ctx, depth))
            {
                case RangeValue range: areas.Add(range); break;
                case ReferenceListValue list: areas.AddRange(list.Areas); break;
                case ScalarValue { Value.IsError: true } error: return error;
                default: return EvalValue.Error(CellError.Value);
            }
        }
        return new ReferenceListValue(areas);
    }

    private EvalValue EvaluateSpillReference(SpillReferenceNode node, IEvaluationContext ctx)
    {
        if (node.SheetName is not null && !ctx.SheetExists(node.SheetName))
            return EvalValue.Error(CellError.Reference);

        var range = ctx.GetSpillRange(node.SheetName, node.Anchor);

        return range is null ? EvalValue.Error(CellError.Reference) : new RangeValue(node.SheetName, range.Value);
    }

    private EvalValue EvaluateName(NameNode node, EvaluationScope scope, IEvaluationContext ctx, int depth)
    {
        if (scope.TryResolve(node.Name, out var bound)) return bound;

        var expression = ctx.ResolveName(node.Name);
        if (expression is null) return EvalValue.Error(CellError.Name);

        try
        {
            return EvaluateNode(_names.Expand(expression, ctx.CurrentCell.Address), scope, ctx, depth + 1);
        }
        catch (FormulaSyntaxException)
        {
            return EvalValue.Error(CellError.Name);
        }
    }

    private EvalValue EvaluateInvocation(InvocationNode node, EvaluationScope scope, IEvaluationContext ctx, int depth)
    {
        if (EvaluateNode(node.Callee, scope, ctx, depth + 1) is not LambdaValue lambda) return EvalValue.Error(CellError.Value);
        var values = new List<EvalValue>(node.Arguments.Count);
        foreach (var argument in node.Arguments) values.Add(EvaluateNode(argument, scope, ctx, depth + 1));
        return Invoke(lambda, values, ctx);
    }

    private EvalValue EvaluateUnary(UnaryNode node, EvaluationScope scope, IEvaluationContext ctx, int depth)
    {
        if (node.Operator == UnaryOperator.Plus) return EvaluateNode(node.Operand, scope, ctx, depth);
        if (node.Operator == UnaryOperator.Single) return EvalValue.Scalar(_coercion.Single(EvaluateNode(node.Operand, scope, ctx, depth), ctx));
        var operand = Intersected(EvaluateNode(node.Operand, scope, ctx, depth), ctx);
        if (!IsMultiValued(operand)) return EvalValue.Scalar(ApplyUnary(node.Operator, _coercion.ToScalar(operand, ctx), ctx));
        if (ExceedsArrayLimit(operand)) return EvalValue.Error(CellError.Number);

        return new ArrayValue([.. ToGrid(operand, ctx)
            .Select(line => (IReadOnlyList<CellValue>)[.. line.Select(value => ApplyUnary(node.Operator, value, ctx))])]);
    }

    private CellValue ApplyUnary(UnaryOperator op, CellValue operand, IEvaluationContext ctx)
    {
        if (operand.IsError) return operand;
        if (!_coercion.TryNumber(operand, ctx, out var value)) return CellValue.FromError(CellError.Value);

        return op switch
        {
            UnaryOperator.Plus => CellValue.Number(value),
            UnaryOperator.Minus => CellValue.Number(-value),
            UnaryOperator.Percent => CellValue.Number(value / 100.0),
            _ => CellValue.FromError(CellError.Value),
        };
    }

    private EvalValue EvaluateBinary(BinaryNode node, EvaluationScope scope, IEvaluationContext ctx, int depth)
    {
        if (node.Operator == BinaryOperator.ReferenceConcat) return EvaluateReferenceConcat(node, scope, ctx, depth);
        var left = Intersected(EvaluateNode(node.Left, scope, ctx, depth), ctx);
        var right = Intersected(EvaluateNode(node.Right, scope, ctx, depth), ctx);

        return IsMultiValued(left) || IsMultiValued(right)
            ? ApplyOverArrays(node.Operator, left, right, ctx)
            : ApplyToScalars(node.Operator, left, right, ctx);
    }

    private EvalValue Intersected(EvalValue value, IEvaluationContext ctx)
    {
        if (!IsMultiValued(value)) return value;
        if (ctx.ImplicitIntersection && value is RangeValue) return EvalValue.Scalar(_coercion.ToScalar(value, ctx));
        ctx.NoteArrayUse();
        return value;
    }

    private bool IsMultiValued(EvalValue value) => value switch
    {
        RangeValue range => !range.Range.IsSingleCell,
        ArrayValue array => array.RowCount > 1 || array.ColumnCount > 1,
        _ => false,
    };

    private EvalValue ApplyOverArrays(
    BinaryOperator op, EvalValue left, EvalValue right, IEvaluationContext ctx)
    {
        if (ExceedsArrayLimit(left) || ExceedsArrayLimit(right)) return EvalValue.Error(CellError.Number);
        var leftGrid = ToGrid(left, ctx);
        var rightGrid = ToGrid(right, ctx);

        var rows = Extent(leftGrid.Count, rightGrid.Count);
        var columns = Extent(Width(leftGrid), Width(rightGrid));
        if (!ResultLimits.ArrayFits(rows, columns)) return EvalValue.Error(CellError.Number);

        var result = new List<IReadOnlyList<CellValue>>(rows);
        for (var row = 0; row < rows; row++)
        {
            var line = new List<CellValue>(columns);
            for (var column = 0; column < columns; column++)
            {
                var a = EvalValue.Scalar(Element(leftGrid, row, column));
                var b = EvalValue.Scalar(Element(rightGrid, row, column));
                line.Add(_coercion.ToScalar(ApplyToScalars(op, a, b, ctx), ctx));
            }
            result.Add(line);
        }
        return new ArrayValue(result);

        static int Width(List<IReadOnlyList<CellValue>> grid) =>
            grid.Count == 0 ? 0 : grid.Max(line => line.Count);

        static int Extent(int left, int right) =>
            left == 1 ? right : right == 1 ? left : Math.Min(left, right);
    }

    private bool ExceedsArrayLimit(EvalValue value) =>
        value is RangeValue range && !ResultLimits.ArrayFits(range.Range.RowCount, range.Range.ColumnCount);

    private List<IReadOnlyList<CellValue>> ToGrid(EvalValue value, IEvaluationContext ctx)
    {
        switch (value)
        {
            case ArrayValue array:
                return [.. array.Rows];

            case RangeValue range:
                {
                    var lines = new List<IReadOnlyList<CellValue>>(range.Range.RowCount);
                    for (var row = 0; row < range.Range.RowCount; row++)
                    {
                        var line = new List<CellValue>(range.Range.ColumnCount);
                        for (var column = 0; column < range.Range.ColumnCount; column++)
                            line.Add(ctx.GetCellValue(range.SheetName, new CellAddress(
                                range.Range.Start.Row + row, range.Range.Start.Column + column)));
                        lines.Add(line);
                    }
                    return lines;
                }

            default:
                return [[_coercion.ToScalar(value, ctx)]];
        }
    }

    private CellValue Element(List<IReadOnlyList<CellValue>> grid, int row, int column)
    {
        if (grid.Count == 0) return CellValue.Empty;

        var line = grid[grid.Count == 1 ? 0 : Math.Min(row, grid.Count - 1)];
        if (line.Count == 0) return CellValue.Empty;

        return line[line.Count == 1 ? 0 : Math.Min(column, line.Count - 1)];
    }

    private EvalValue ApplyToScalars(
        BinaryOperator op, EvalValue leftValue, EvalValue rightValue, IEvaluationContext ctx)
    {
        var left = _coercion.ToScalar(leftValue, ctx);
        if (left.IsError) return EvalValue.Scalar(left);

        var right = _coercion.ToScalar(rightValue, ctx);
        if (right.IsError) return EvalValue.Scalar(right);

        if (op == BinaryOperator.Concat)
        {
            var leftText = ctx.NumberText.Of(left);
            var rightText = ctx.NumberText.Of(right);
            return ResultLimits.TextFits((long)leftText.Length + rightText.Length)
                ? EvalValue.Scalar(CellValue.Text(leftText + rightText))
                : EvalValue.Error(CellError.Value);
        }

        if (IsComparison(op)) return EvalValue.Scalar(Compare(op, left, right, ctx));

        if (!_coercion.TryNumber(left, ctx, out var a) || !_coercion.TryNumber(right, ctx, out var b))
            return EvalValue.Error(CellError.Value);

        if (op == BinaryOperator.Divide && b == 0) return EvalValue.Error(CellError.DivideByZero);

        var result = op switch
        {
            BinaryOperator.Add => Functions.Numerics.SpreadsheetMath.ApproxAdd(a, b),
            BinaryOperator.Subtract => Functions.Numerics.SpreadsheetMath.ApproxAdd(a, -b),
            BinaryOperator.Multiply => a * b,
            BinaryOperator.Divide => a / b,
            _ => Functions.Numerics.SpreadsheetMath.Power(a, b),
        };
        return double.IsFinite(result)
            ? EvalValue.Scalar(CellValue.Number(result))
            : EvalValue.Error(CellError.Number);
    }

    private bool IsComparison(BinaryOperator op) =>
        op is BinaryOperator.Equal or BinaryOperator.NotEqual or BinaryOperator.Less
            or BinaryOperator.LessOrEqual or BinaryOperator.Greater or BinaryOperator.GreaterOrEqual;

    private CellValue Compare(BinaryOperator op, CellValue left, CellValue right, IEvaluationContext ctx)
    {
        int comparison;

        if (left.IsNumeric && right.IsNumeric)
            comparison = Functions.Numerics.SpreadsheetMath.ApproxCompare(left.AsNumber(), right.AsNumber());
        else if (left.Kind == ValueKind.Text && right.Kind == ValueKind.Text)
            comparison = string.Compare(left.AsText(), right.AsText(), ctx.TextRules.Comparison);
        else if (left.IsEmpty && right.IsEmpty)
            comparison = 0;
        else if (left.IsEmpty)
            comparison = right.Kind == ValueKind.Text
                ? (right.AsText().Length == 0 ? 0 : -1)
                : (0.0).CompareTo(right.AsNumber());
        else if (right.IsEmpty)
            comparison = left.Kind == ValueKind.Text
                ? (left.AsText().Length == 0 ? 0 : 1)
                : left.AsNumber().CompareTo(0.0);
        else
            comparison = Rank(left).CompareTo(Rank(right));

        return CellValue.Boolean(op switch
        {
            BinaryOperator.Equal => comparison == 0,
            BinaryOperator.NotEqual => comparison != 0,
            BinaryOperator.Less => comparison < 0,
            BinaryOperator.LessOrEqual => comparison <= 0,
            BinaryOperator.Greater => comparison > 0,
            BinaryOperator.GreaterOrEqual => comparison >= 0,
            _ => false,
        });

        static int Rank(CellValue v) => v.Kind switch
        {
            ValueKind.Number => 0,
            ValueKind.Text => 1,
            ValueKind.Boolean => 2,
            _ => -1,
        };
    }

    private EvalValue EvaluateFunction(
        FunctionNode node, EvaluationScope scope, IEvaluationContext ctx, int depth)
    {
        if (specialForms.Find(node.Name) is { } form)
        {
            if (node.Arguments.Count < form.MinArguments || node.Arguments.Count > form.MaxArguments)
                return EvalValue.Error(CellError.Value);

            return form.Evaluate(node.Arguments, scope, ctx, this);
        }

        if (scope.TryResolve(node.Name, out var bound) && bound is LambdaValue lambda)
        {
            var values = new List<EvalValue>(node.Arguments.Count);
            foreach (var argument in node.Arguments)
                values.Add(EvaluateNode(argument, scope, ctx, depth + 1));

            return Invoke(lambda, values, ctx);
        }

        var definition = functions.Find(node.Name);
        if (definition is null)
        {
            if (ctx.ResolveName(node.Name) is not { } expression) return EvalValue.Error(CellError.Name);
            try
            {
                if (EvaluateNode(_names.Expand(expression, ctx.CurrentCell.Address), scope, ctx, depth + 1) is not LambdaValue named) return EvalValue.Error(CellError.Name);
                var values = new List<EvalValue>(node.Arguments.Count);
                foreach (var argument in node.Arguments) values.Add(EvaluateNode(argument, scope, ctx, depth + 1));
                return Invoke(named, values, ctx);
            }
            catch (FormulaSyntaxException)
            {
                return EvalValue.Error(CellError.Name);
            }
        }
        if (!definition.AcceptsArgumentCount(node.Arguments.Count)) return EvalValue.Error(CellError.Value);

        ArrayModeContext? arrayMode = null;
        EvalValue EvaluateArgument(int index)
        {
            var argumentContext = ctx.ImplicitIntersection && arrayParameters.IsArrayParameter(node.Name, index)
                ? arrayMode ??= new ArrayModeContext(ctx)
                : ctx;
            return EvaluateNode(node.Arguments[index], scope, argumentContext, depth);
        }

        IReadOnlyList<EvalValue> arguments;
        if (definition.HandlesErrors) arguments = new LazyArgumentList(node.Arguments.Count, EvaluateArgument);
        else
        {
            var evaluated = new List<EvalValue>(node.Arguments.Count);
            for (var index = 0; index < node.Arguments.Count; index++)
            {
                var value = EvaluateArgument(index);
                if (value is ScalarValue { Value.IsError: true } error) return error;
                evaluated.Add(value);
            }
            arguments = evaluated;
        }

        try
        {
            return definition.Implementation(arguments, ctx);
        }
        catch (Exception exception) when (exception is ArgumentOutOfRangeException or OverflowException)
        {
            return EvalValue.Error(CellError.Number);
        }
    }

    private EvalValue EvaluateArray(
        ArrayNode node, EvaluationScope scope, IEvaluationContext ctx, int depth)
    {
        var rows = new List<IReadOnlyList<CellValue>>(node.Rows.Count);
        foreach (var row in node.Rows)
        {
            var values = new List<CellValue>(row.Count);
            foreach (var cell in row)
                values.Add(_coercion.ToScalar(EvaluateNode(cell, scope, ctx, depth), ctx));
            rows.Add(values);
        }
        return new ArrayValue(rows);
    }

    public static string Format(CellValue value) => value.Kind switch
    {
        ValueKind.Number => value.AsNumber().ToString("R", CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };

    private sealed class NodeEvaluation(Evaluator evaluator, EvaluationScope scope, IEvaluationContext ctx, int depth) : FormulaNodeVisitor<EvalValue>
    {
        public override EvalValue VisitLiteral(LiteralNode node) => EvalValue.Scalar(node.Value);

        public override EvalValue VisitMissingArgument(MissingArgumentNode node) => MissingValue.Instance;

        public override EvalValue VisitReference(ReferenceNode node) => evaluator.EvaluateReference(node, ctx);

        public override EvalValue VisitRange(RangeNode node) => evaluator.EvaluateRange(node, ctx);

        public override EvalValue VisitSpillReference(SpillReferenceNode node) => evaluator.EvaluateSpillReference(node, ctx);

        public override EvalValue VisitName(NameNode node) => evaluator.EvaluateName(node, scope, ctx, depth);

        public override EvalValue VisitUnary(UnaryNode node) => evaluator.EvaluateUnary(node, scope, ctx, depth);

        public override EvalValue VisitBinary(BinaryNode node) => evaluator.EvaluateBinary(node, scope, ctx, depth);

        public override EvalValue VisitFunction(FunctionNode node) => evaluator.EvaluateFunction(node, scope, ctx, depth);

        public override EvalValue VisitArray(ArrayNode node) => evaluator.EvaluateArray(node, scope, ctx, depth);

        public override EvalValue VisitInvocation(InvocationNode node) => evaluator.EvaluateInvocation(node, scope, ctx, depth);

        protected override EvalValue Otherwise(FormulaNode node) => EvalValue.Error(CellError.Value);
    }
}

using PulseOffice.Sheets.Engine.Parsing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.SpecialForms;

public abstract class HigherOrderForm(IRangeMaterializer ranges) : ISpecialForm
{
    private readonly ValueCoercion _coercion = new();

    public abstract string Name { get; }
    public abstract int MinArguments { get; }
    public abstract int MaxArguments { get; }
    public string Category => "Matrice";

    public abstract EvalValue Evaluate(
        IReadOnlyList<FormulaNode> arguments, EvaluationScope scope,
        IEvaluationContext context, IEvaluationServices services);

    protected ValueGrid Grid(
    FormulaNode node, EvaluationScope scope, IEvaluationContext context, IEvaluationServices services) =>
    ranges.Materialize(services.Evaluate(node, scope, context), context);

    protected LambdaValue? Lambda(
    FormulaNode node, EvaluationScope scope, IEvaluationContext context, IEvaluationServices services) =>
    services.Evaluate(node, scope, context) as LambdaValue;

    protected CellValue Scalar(EvalValue value, IEvaluationContext context) =>
        _coercion.ToScalar(value, context);

    protected CellValue Element(EvalValue value, IEvaluationContext context) => value switch
    {
        ArrayValue { RowCount: > 1 } or ArrayValue { ColumnCount: > 1 } => CellValue.FromError(CellError.Calc),
        RangeValue { Range.IsSingleCell: false } => CellValue.FromError(CellError.Calc),
        ReferenceListValue { Areas.Count: > 1 } => CellValue.FromError(CellError.Calc),
        _ => Scalar(value, context),
    };
}

public sealed class MapForm(IRangeMaterializer ranges) : HigherOrderForm(ranges)
{
    public override string Name => "MAP";
    public override int MinArguments => 2;
    public override int MaxArguments => int.MaxValue;

    public override EvalValue Evaluate(
        IReadOnlyList<FormulaNode> arguments, EvaluationScope scope,
        IEvaluationContext context, IEvaluationServices services)
    {
        var lambda = Lambda(arguments[^1], scope, context, services);
        if (lambda is null) return EvalValue.Error(CellError.Value);

        var grids = new List<ValueGrid>(arguments.Count - 1);
        for (var i = 0; i < arguments.Count - 1; i++)
            grids.Add(Grid(arguments[i], scope, context, services));

        if (lambda.Arity != grids.Count) return EvalValue.Error(CellError.Value);
        if (grids[0].IsEmpty) return EvalValue.Error(CellError.Value);

        if (grids.Any(g => g.RowCount != grids[0].RowCount || g.ColumnCount != grids[0].ColumnCount))
            return EvalValue.Error(CellError.Value);

        var result = new List<IReadOnlyList<CellValue>>(grids[0].RowCount);

        for (var row = 0; row < grids[0].RowCount; row++)
        {
            var line = new List<CellValue>(grids[0].ColumnCount);
            for (var column = 0; column < grids[0].ColumnCount; column++)
            {
                var parameters = grids.Select(g => (EvalValue)EvalValue.Scalar(g[row, column])).ToList();
                line.Add(Element(services.Invoke(lambda, parameters, context), context));
            }
            result.Add(line);
        }

        return new ArrayValue(result);
    }
}

public sealed class ReduceForm(IRangeMaterializer ranges) : HigherOrderForm(ranges)
{
    public override string Name => "REDUCE";
    public override int MinArguments => 3;
    public override int MaxArguments => 3;

    public override EvalValue Evaluate(
        IReadOnlyList<FormulaNode> arguments, EvaluationScope scope,
        IEvaluationContext context, IEvaluationServices services)
    {
        var lambda = Lambda(arguments[2], scope, context, services);
        if (lambda is null || lambda.Arity != 2) return EvalValue.Error(CellError.Value);

        var accumulator = services.Evaluate(arguments[0], scope, context);
        var grid = Grid(arguments[1], scope, context, services);

        foreach (var value in grid.InReadingOrder())
        {
            accumulator = services.Invoke(lambda, [accumulator, EvalValue.Scalar(value)], context);
            if (accumulator is ScalarValue { Value.IsError: true }) return accumulator;
        }

        return accumulator;
    }
}

public sealed class ScanForm(IRangeMaterializer ranges) : HigherOrderForm(ranges)
{
    public override string Name => "SCAN";
    public override int MinArguments => 3;
    public override int MaxArguments => 3;

    public override EvalValue Evaluate(
        IReadOnlyList<FormulaNode> arguments, EvaluationScope scope,
        IEvaluationContext context, IEvaluationServices services)
    {
        var lambda = Lambda(arguments[2], scope, context, services);
        if (lambda is null || lambda.Arity != 2) return EvalValue.Error(CellError.Value);

        var accumulator = services.Evaluate(arguments[0], scope, context);
        var grid = Grid(arguments[1], scope, context, services);
        var result = new List<IReadOnlyList<CellValue>>(grid.RowCount);

        for (var row = 0; row < grid.RowCount; row++)
        {
            var line = new List<CellValue>(grid.ColumnCount);
            for (var column = 0; column < grid.ColumnCount; column++)
            {
                accumulator = services.Invoke(lambda, [accumulator, EvalValue.Scalar(grid[row, column])], context);
                line.Add(Element(accumulator, context));
            }
            result.Add(line);
        }

        return new ArrayValue(result);
    }
}

public abstract class ByLineForm(IRangeMaterializer ranges) : HigherOrderForm(ranges)
{
    public override int MinArguments => 2;
    public override int MaxArguments => 2;

    protected abstract bool ByRow { get; }

    public override EvalValue Evaluate(
        IReadOnlyList<FormulaNode> arguments, EvaluationScope scope,
        IEvaluationContext context, IEvaluationServices services)
    {
        var lambda = Lambda(arguments[1], scope, context, services);
        if (lambda is null || lambda.Arity != 1) return EvalValue.Error(CellError.Value);

        var grid = Grid(arguments[0], scope, context, services);
        if (grid.IsEmpty) return EvalValue.Error(CellError.Value);

        var count = ByRow ? grid.RowCount : grid.ColumnCount;
        var result = new List<IReadOnlyList<CellValue>>(count);

        for (var index = 0; index < count; index++)
        {
            var line = (ByRow ? grid.Row(index) : grid.Column(index)).ToList();

            var slice = ByRow
                ? new ArrayValue([line])
                : new ArrayValue([.. line.Select(v => (IReadOnlyList<CellValue>)[v])]);

            var value = Element(services.Invoke(lambda, [slice], context), context);

            if (ByRow) result.Add([value]);
            else
            {
                if (result.Count == 0) result.Add(new List<CellValue>());
                ((List<CellValue>)result[0]).Add(value);
            }
        }

        return new ArrayValue(result);
    }
}

public sealed class ByRowForm(IRangeMaterializer ranges) : ByLineForm(ranges)
{
    public override string Name => "BYROW";
    protected override bool ByRow => true;
}

public sealed class ByColForm(IRangeMaterializer ranges) : ByLineForm(ranges)
{
    public override string Name => "BYCOL";
    protected override bool ByRow => false;
}

public sealed class MakeArrayForm(IRangeMaterializer ranges) : HigherOrderForm(ranges)
{
    public override string Name => "MAKEARRAY";
    public override int MinArguments => 3;
    public override int MaxArguments => 3;

    private const int MaxCells = 1_000_000;

    public override EvalValue Evaluate(
        IReadOnlyList<FormulaNode> arguments, EvaluationScope scope,
        IEvaluationContext context, IEvaluationServices services)
    {
        var lambda = Lambda(arguments[2], scope, context, services);
        if (lambda is null || lambda.Arity != 2) return EvalValue.Error(CellError.Value);

        var rowValue = Scalar(services.Evaluate(arguments[0], scope, context), context);
        var columnValue = Scalar(services.Evaluate(arguments[1], scope, context), context);

        if (!rowValue.TryCoerceToNumber(out var rowCount) ||
            !columnValue.TryCoerceToNumber(out var columnCount))
            return EvalValue.Error(CellError.Value);

        var rows = (int)rowCount;
        var columns = (int)columnCount;

        if (rows < 1 || columns < 1) return EvalValue.Error(CellError.Value);
        if ((long)rows * columns > MaxCells) return EvalValue.Error(CellError.Number);

        var result = new List<IReadOnlyList<CellValue>>(rows);

        for (var row = 1; row <= rows; row++)
        {
            var line = new List<CellValue>(columns);
            for (var column = 1; column <= columns; column++)
                line.Add(Element(services.Invoke(lambda,
                    [EvalValue.Scalar(CellValue.Number(row)), EvalValue.Scalar(CellValue.Number(column))],
                    context), context));
            result.Add(line);
        }

        return new ArrayValue(result);
    }
}

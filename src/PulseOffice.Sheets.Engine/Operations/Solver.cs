using PulseOffice.Sheets;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

public enum SolverGoal { Maximum, Minimum, Value }

public enum ConstraintOperator { LessOrEqual, Equal, GreaterOrEqual, Integer, Binary }

public sealed record SolverConstraint(SheetCell Cell, ConstraintOperator Operator, double Value = 0);

public sealed record SolverRequest
{
    public required SheetCell Objective { get; init; }

    public required IReadOnlyList<SheetCell> Variables { get; init; }

    public SolverGoal Goal { get; init; } = SolverGoal.Maximum;

    public double TargetValue { get; init; }

    public IReadOnlyList<SolverConstraint> Constraints { get; init; } = [];

    public bool AssumeNonNegative { get; init; } = true;

    public int MaximumIterations { get; init; } = 2000;

    public double Tolerance { get; init; } = 1e-9;
}

public sealed record SolverOutcome(
    bool Succeeded, double Objective, IReadOnlyList<double> Variables, int Iterations, string? Reason = null);

public interface ISolver
{
    SolverOutcome Solve(CalculationEngine engine, SolverRequest request);
}

public sealed class Solver : ISolver
{
    private const double Penalty = 1e6;

    public SolverOutcome Solve(CalculationEngine engine, SolverRequest request)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Variables.Count == 0)
            return new SolverOutcome(false, double.NaN, [], 0, "no-variables");

        var originals = request.Variables
            .Select(cell => engine.Workbook.Sheets[cell.SheetIndex][cell.Address])
            .ToList();

        try
        {
            return Search(engine, request);
        }
        finally
        {
            for (var index = 0; index < request.Variables.Count; index++)
                Restore(engine, request.Variables[index], originals[index]);
        }
    }

    private SolverOutcome Search(CalculationEngine engine, SolverRequest request)
    {
        var dimension = request.Variables.Count;
        var start = request.Variables.Select(cell => Read(engine, cell)).ToArray();

        var simplex = new List<double[]> { start };

        for (var index = 0; index < dimension; index++)
        {
            var point = (double[])start.Clone();
            point[index] += Math.Abs(point[index]) > 1e-9 ? point[index] * 0.05 : 1;
            simplex.Add(point);
        }

        var scores = simplex.Select(point => Score(engine, request, point)).ToList();
        var iterations = 0;

        while (iterations < request.MaximumIterations)
        {
            iterations++;

            var order = Enumerable.Range(0, simplex.Count).OrderBy(index => scores[index]).ToList();
            simplex = [.. order.Select(index => simplex[index])];
            scores = [.. order.Select(index => scores[index])];

            if (Math.Abs(scores[^1] - scores[0]) <= request.Tolerance * (Math.Abs(scores[0]) + 1)) break;

            var centroid = new double[dimension];

            for (var index = 0; index < simplex.Count - 1; index++)
                for (var axis = 0; axis < dimension; axis++)
                    centroid[axis] += simplex[index][axis] / (simplex.Count - 1);

            var worst = simplex[^1];

            var reflected = Toward(centroid, worst, 1);
            var reflectedScore = Score(engine, request, reflected);

            if (reflectedScore < scores[0])
            {
                var expanded = Toward(centroid, worst, 2);
                var expandedScore = Score(engine, request, expanded);

                simplex[^1] = expandedScore < reflectedScore ? expanded : reflected;
                scores[^1] = Math.Min(expandedScore, reflectedScore);
                continue;
            }

            if (reflectedScore < scores[^2])
            {
                simplex[^1] = reflected;
                scores[^1] = reflectedScore;
                continue;
            }

            var contracted = Toward(centroid, worst, -0.5);
            var contractedScore = Score(engine, request, contracted);

            if (contractedScore < scores[^1])
            {
                simplex[^1] = contracted;
                scores[^1] = contractedScore;
                continue;
            }

            for (var index = 1; index < simplex.Count; index++)
            {
                for (var axis = 0; axis < dimension; axis++)
                    simplex[index][axis] = simplex[0][axis] + (simplex[index][axis] - simplex[0][axis]) * 0.5;

                scores[index] = Score(engine, request, simplex[index]);
            }
        }

        var best = simplex[0];
        var rounded = Round(request, best);

        Write(engine, request, rounded);

        var objective = engine.GetValue(request.Objective);
        var feasible = Violation(engine, request) <= 1e-6;

        return new SolverOutcome(
            feasible && objective.IsNumeric,
            objective.TryCoerceToNumber(out var number) ? number : double.NaN,
            rounded,
            iterations,
            feasible ? null : "constraints");
    }

    private double[] Round(SolverRequest request, double[] point)
    {
        var result = (double[])point.Clone();

        for (var index = 0; index < request.Variables.Count; index++)
        {
            if (request.AssumeNonNegative) result[index] = Math.Max(result[index], 0);

            var constraint = request.Constraints.FirstOrDefault(
                c => c.Cell.Equals(request.Variables[index])
                     && c.Operator is ConstraintOperator.Integer or ConstraintOperator.Binary);

            if (constraint is null) continue;

            result[index] = Math.Round(result[index], MidpointRounding.AwayFromZero);

            if (constraint.Operator == ConstraintOperator.Binary)
                result[index] = Math.Clamp(result[index], 0, 1);
        }

        return result;
    }

    private double Score(CalculationEngine engine, SolverRequest request, double[] point)
    {
        Write(engine, request, Round(request, point));

        var value = engine.GetValue(request.Objective);
        if (!value.TryCoerceToNumber(out var objective)) return double.MaxValue;

        var score = request.Goal switch
        {
            SolverGoal.Maximum => -objective,
            SolverGoal.Minimum => objective,
            _ => Math.Abs(objective - request.TargetValue),
        };

        return score + Penalty * Violation(engine, request);
    }

    private double Violation(CalculationEngine engine, SolverRequest request)
    {
        var total = 0.0;

        foreach (var constraint in request.Constraints)
        {
            if (constraint.Operator is ConstraintOperator.Integer or ConstraintOperator.Binary) continue;

            if (!engine.GetValue(constraint.Cell).TryCoerceToNumber(out var value)) return double.MaxValue;

            total += constraint.Operator switch
            {
                ConstraintOperator.LessOrEqual => Math.Max(value - constraint.Value, 0),
                ConstraintOperator.GreaterOrEqual => Math.Max(constraint.Value - value, 0),
                _ => Math.Abs(value - constraint.Value),
            };
        }

        if (!request.AssumeNonNegative) return total;

        foreach (var variable in request.Variables)
            if (engine.GetValue(variable).TryCoerceToNumber(out var value))
                total += Math.Max(-value, 0);

        return total;
    }

    private double[] Toward(double[] centroid, double[] worst, double factor)
    {
        var result = new double[centroid.Length];

        for (var index = 0; index < centroid.Length; index++)
            result[index] = centroid[index] + factor * (centroid[index] - worst[index]);

        return result;
    }

    private void Write(CalculationEngine engine, SolverRequest request, double[] point)
    {
        for (var index = 0; index < request.Variables.Count; index++)
            engine.SetValue(request.Variables[index], CellValue.Number(point[index]));
    }

    private double Read(CalculationEngine engine, SheetCell cell) =>
        engine.GetValue(cell).TryCoerceToNumber(out var number) ? number : 0;

    private void Restore(CalculationEngine engine, SheetCell cell, Cell original)
    {
        if (original.HasFormula) engine.SetFormula(cell, original.Formula!);
        else engine.SetValue(cell, original.Value);
    }
}

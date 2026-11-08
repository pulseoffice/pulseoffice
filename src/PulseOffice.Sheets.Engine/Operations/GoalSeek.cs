using PulseOffice.Sheets;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

public sealed record GoalSeekRequest(SheetCell Formula, SheetCell Variable, double Target)
{
    public double Tolerance { get; init; } = 1e-9;

    public int MaximumIterations { get; init; } = 100;

    public double InitialStep { get; init; } = 1;
}

public sealed record GoalSeekOutcome(bool Found, double Value, double Reached, int Iterations)
{
    public double Distance(double target) => Math.Abs(Reached - target);
}

public interface IGoalSeek
{
    GoalSeekOutcome Solve(CalculationEngine engine, GoalSeekRequest request);
}

public sealed class GoalSeek : IGoalSeek
{
    public GoalSeekOutcome Solve(CalculationEngine engine, GoalSeekRequest request)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(request);

        var original = engine.Workbook.Sheets[request.Variable.SheetIndex][request.Variable.Address];

        try
        {
            return Search(engine, request);
        }
        finally
        {
            Restore(engine, request.Variable, original);
        }
    }

    private GoalSeekOutcome Search(CalculationEngine engine, GoalSeekRequest request)
    {
        var start = Read(engine, request.Variable);

        var x0 = start;
        var x1 = Math.Abs(start) > 1e-12 ? start * 1.1 : request.InitialStep;

        var first = Evaluate(engine, request, x0);

        if (first is null)
        {
            x0 = x1;
            x1 = x0 + request.InitialStep;
            first = Evaluate(engine, request, x0);
        }

        if (first is not { } f0)
            return new GoalSeekOutcome(false, start, double.NaN, 0);

        if (Math.Abs(f0) <= request.Tolerance)
            return new GoalSeekOutcome(true, x0, f0 + request.Target, 0);

        for (var iteration = 1; iteration <= request.MaximumIterations; iteration++)
        {
            if (Evaluate(engine, request, x1) is not { } f1)
                return new GoalSeekOutcome(false, x1, double.NaN, iteration);

            if (Math.Abs(f1) <= request.Tolerance)
                return new GoalSeekOutcome(true, x1, f1 + request.Target, iteration);

            var slope = f1 - f0;

            if (Math.Abs(slope) < 1e-15)
            {
                x0 = x1;
                f0 = f1;
                x1 += request.InitialStep;
                continue;
            }

            var next = x1 - f1 * (x1 - x0) / slope;

            if (double.IsNaN(next) || double.IsInfinity(next))
                return new GoalSeekOutcome(false, x1, f1 + request.Target, iteration);

            x0 = x1;
            f0 = f1;
            x1 = next;
        }

        var reached = Evaluate(engine, request, x1);

        return new GoalSeekOutcome(
            reached is { } last && Math.Abs(last) <= request.Tolerance,
            x1,
            reached is { } value ? value + request.Target : double.NaN,
            request.MaximumIterations);
    }

    private double? Evaluate(CalculationEngine engine, GoalSeekRequest request, double candidate)
    {
        engine.SetValue(request.Variable, CellValue.Number(candidate));

        var result = engine.GetValue(request.Formula);
        return result.TryCoerceToNumber(out var number) ? number - request.Target : null;
    }

    private double Read(CalculationEngine engine, SheetCell cell) =>
        engine.GetValue(cell).TryCoerceToNumber(out var number) ? number : 0;

    private void Restore(CalculationEngine engine, SheetCell cell, Cell original)
    {
        if (original.HasFormula) engine.SetFormula(cell, original.Formula!);
        else engine.SetValue(cell, original.Value);
    }
}

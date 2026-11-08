using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Operations;

public interface IDependencyTracer
{
    IReadOnlyList<TraceArrow> Precedents(CalculationEngine engine, SheetCell cell);

    IReadOnlyList<TraceArrow> Dependents(CalculationEngine engine, SheetCell cell);

    IReadOnlyList<TraceArrow> ErrorSources(CalculationEngine engine, SheetCell cell);
}

public sealed class DependencyTracer : IDependencyTracer
{
    public IReadOnlyList<TraceArrow> Precedents(CalculationEngine engine, SheetCell cell)
    {
        ArgumentNullException.ThrowIfNull(engine);

        return [.. engine.Graph
            .DirectPrecedents(cell)
            .Where(precedent => precedent != cell)
            .Select(precedent => new TraceArrow(precedent.Address, cell.Address, TraceKind.Precedent)
            {
                IsExternal = precedent.SheetIndex != cell.SheetIndex,
            })
            .OrderBy(arrow => arrow.From.Row)
            .ThenBy(arrow => arrow.From.Column)];
    }

    public IReadOnlyList<TraceArrow> Dependents(CalculationEngine engine, SheetCell cell)
    {
        ArgumentNullException.ThrowIfNull(engine);

        return [.. engine.Graph
            .DirectDependents(cell)
            .Where(dependent => dependent != cell)
            .Select(dependent => new TraceArrow(cell.Address, dependent.Address, TraceKind.Dependent)
            {
                IsExternal = dependent.SheetIndex != cell.SheetIndex,
            })
            .OrderBy(arrow => arrow.To.Row)
            .ThenBy(arrow => arrow.To.Column)];
    }

    public IReadOnlyList<TraceArrow> ErrorSources(CalculationEngine engine, SheetCell cell)
    {
        ArgumentNullException.ThrowIfNull(engine);

        if (!engine.GetValue(cell).IsError) return [];

        var arrows = new List<TraceArrow>();
        var seen = new HashSet<SheetCell> { cell };
        var queue = new Queue<SheetCell>([cell]);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var precedent in engine.Graph.DirectPrecedents(current))
            {
                if (!seen.Add(precedent)) continue;
                if (!engine.GetValue(precedent).IsError) continue;

                arrows.Add(new TraceArrow(precedent.Address, current.Address, TraceKind.Error)
                {
                    IsExternal = precedent.SheetIndex != current.SheetIndex,
                });

                queue.Enqueue(precedent);
            }
        }

        return arrows;
    }
}

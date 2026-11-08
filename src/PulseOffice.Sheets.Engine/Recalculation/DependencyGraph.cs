using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Engine.Recalculation;

public sealed class DependencyGraph
{
    private readonly Dictionary<SheetCell, CompactCellSet> _dependents = [];
    private readonly Dictionary<SheetCell, SheetCell[]> _precedents = [];

    private readonly Dictionary<SheetCell, List<(int SheetIndex, CellRange Range)>> _rangePrecedents = [];

    private readonly RangeDependencyIndex _rangeIndex = new();

    public int CellCount => _precedents.Count;

    public void SetPrecedents(SheetCell cell, IEnumerable<SheetCell> precedents, IEnumerable<(int, CellRange)> ranges)
    {
        ClearPrecedents(cell);

        SheetCell[] set = [.. new HashSet<SheetCell>(precedents)];
        if (set.Length > 0)
        {
            _precedents[cell] = set;
            foreach (var p in set)
            {
                if (!_dependents.TryGetValue(p, out var d))
                    _dependents[p] = d = new CompactCellSet();
                d.Add(cell);
            }
        }

        var rangeList = ranges.ToList();
        if (rangeList.Count == 0) return;
        _rangePrecedents[cell] = rangeList;
        foreach (var (sheetIndex, range) in rangeList) _rangeIndex.Add(cell, sheetIndex, range);
    }

    public void ClearPrecedents(SheetCell cell)
    {
        if (_precedents.TryGetValue(cell, out var existing))
        {
            foreach (var p in existing)
                if (_dependents.TryGetValue(p, out var d))
                {
                    d.Remove(cell);
                    if (d.Count == 0) _dependents.Remove(p);
                }
            _precedents.Remove(cell);
        }
        if (_rangePrecedents.Remove(cell, out var ranges))
            foreach (var (sheetIndex, range) in ranges) _rangeIndex.Remove(cell, sheetIndex, range);
    }

    public IReadOnlySet<SheetCell> DirectPrecedents(SheetCell cell)
    {
        var result = new HashSet<SheetCell>();

        if (_precedents.TryGetValue(cell, out var direct)) result.UnionWith(direct);

        if (_rangePrecedents.TryGetValue(cell, out var ranges))
            foreach (var (sheetIndex, range) in ranges)
                foreach (var address in range.Cells())
                    result.Add(new SheetCell(sheetIndex, address));

        return result;
    }

    public IReadOnlyList<SheetCell> DirectCellPrecedents(SheetCell cell) =>
        _precedents.TryGetValue(cell, out var direct) ? direct : [];

    public IReadOnlyList<(int SheetIndex, CellRange Range)> PrecedentRanges(SheetCell cell) =>
        _rangePrecedents.TryGetValue(cell, out var ranges) ? ranges : [];

    public IReadOnlySet<SheetCell> DirectDependents(SheetCell cell)
    {
        var result = new HashSet<SheetCell>();
        if (_dependents.TryGetValue(cell, out var direct)) result.UnionWith(direct);

        _rangeIndex.CollectDependents(cell, result);

        return result;
    }

    public IReadOnlySet<SheetCell> TransitiveDependents(IEnumerable<SheetCell> changed)
    {
        var visited = new HashSet<SheetCell>();
        var queue = new Queue<SheetCell>(changed);

        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            if (!visited.Add(cell)) continue;
            foreach (var d in DirectDependents(cell))
                if (!visited.Contains(d)) queue.Enqueue(d);
        }
        return visited;
    }

    public IReadOnlySet<SheetCell> FindCircular(IEnumerable<SheetCell> scope, bool cellsOnly = false)
    {
        IReadOnlySet<SheetCell> Next(SheetCell cell) =>
            !cellsOnly ? DirectDependents(cell)
            : _dependents.TryGetValue(cell, out var direct) ? direct.ToHashSet() : new HashSet<SheetCell>();

        var circular = new HashSet<SheetCell>();
        var index = new Dictionary<SheetCell, int>();
        var lowLink = new Dictionary<SheetCell, int>();
        var onStack = new HashSet<SheetCell>();
        var stack = new Stack<SheetCell>();
        var next = 0;

        foreach (var cell in scope)
            if (!index.ContainsKey(cell))
                StrongConnect(cell);

        return circular;

        void Open(SheetCell v, Stack<(SheetCell Cell, IEnumerator<SheetCell> Next)> frames)
        {
            index[v] = lowLink[v] = next++;
            stack.Push(v);
            onStack.Add(v);
            frames.Push((v, Next(v).GetEnumerator()));
        }

        void StrongConnect(SheetCell root)
        {
            var frames = new Stack<(SheetCell Cell, IEnumerator<SheetCell> Next)>();
            Open(root, frames);

            while (frames.Count > 0)
            {
                var (v, successors) = frames.Peek();
                if (successors.MoveNext())
                {
                    var w = successors.Current;
                    if (!index.ContainsKey(w)) Open(w, frames);
                    else if (onStack.Contains(w)) lowLink[v] = System.Math.Min(lowLink[v], index[w]);
                    continue;
                }

                frames.Pop();
                if (frames.Count > 0)
                {
                    var parent = frames.Peek().Cell;
                    lowLink[parent] = System.Math.Min(lowLink[parent], lowLink[v]);
                }

                if (lowLink[v] != index[v]) continue;

                var component = new List<SheetCell>();
                SheetCell popped;
                do
                {
                    popped = stack.Pop();
                    onStack.Remove(popped);
                    component.Add(popped);
                } while (!popped.Equals(v));

                if (component.Count > 1 || Next(v).Contains(v))
                    circular.UnionWith(component);
            }
        }
    }

    public (IReadOnlyList<SheetCell> Order, IReadOnlySet<SheetCell> Circular) TopologicalOrder(
    IReadOnlySet<SheetCell> scope)
    {
        var circular = FindCircular(scope);
        var pending = scope.Where(c => !circular.Contains(c)).ToHashSet();

        var inDegree = pending.ToDictionary(c => c, _ => 0);
        foreach (var cell in pending)
            foreach (var dependent in DirectDependents(cell))
                if (inDegree.TryGetValue(dependent, out var degree)) inDegree[dependent] = degree + 1;

        var ready = new Queue<SheetCell>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var order = new List<SheetCell>(pending.Count);

        while (ready.Count > 0)
        {
            var cell = ready.Dequeue();
            order.Add(cell);

            foreach (var dependent in DirectDependents(cell))
            {
                if (!inDegree.TryGetValue(dependent, out var degree)) continue;
                inDegree[dependent] = --degree;
                if (degree == 0) ready.Enqueue(dependent);
            }
        }

        var leftover = pending.Except(order).ToHashSet();
        if (leftover.Count > 0)
        {
            var all = new HashSet<SheetCell>(circular);
            all.UnionWith(leftover);
            return (order, all);
        }

        return (order, circular);
    }

    public void Clear()
    {
        _dependents.Clear();
        _precedents.Clear();
        _rangePrecedents.Clear();
        _rangeIndex.Clear();
    }
}

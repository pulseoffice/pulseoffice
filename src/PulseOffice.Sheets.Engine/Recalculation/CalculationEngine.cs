using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Evaluation;
using PulseOffice.Sheets.Engine.Evaluation.Formats;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Parsing;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Recalculation;

public sealed class CalculationEngine
{
    private readonly FormulaParser _parser = new();
    private readonly Evaluation.Functions.ValueCoercion _coercion = new();

    private readonly Workbook _workbook;
    private readonly PulseOffice.Foundation.Identity.IUserIdentity _identity;
    private readonly IFunctionRegistry _functions;
    private readonly Evaluator _evaluator;
    private readonly DependencyGraph _graph = new();
    private readonly Dictionary<SheetCell, FormulaNode> _formulas = [];
    private readonly IFormulaFormatInference _formats;
    private readonly IFormatHints _hints;

    private static readonly IFormatHints DefaultHints = new FormatHints(new FormatKindClassifier());
    private readonly Dictionary<SheetCell, CellValue> _values = [];
    private readonly HashSet<SheetCell> _circular = [];

    private readonly Dictionary<SheetCell, CellRange> _spillRanges = [];

    private readonly Dictionary<SheetCell, SheetCell> _spillOwners = [];

    private readonly Dictionary<SheetCell, CellRange> _blockedSpills = [];

    private readonly Dictionary<SheetCell, string> _requestedStyles = [];

    private readonly WholeColumnAggregates _aggregates;

    internal Evaluation.Functions.NumberToText NumberText => _evaluator.NumberText;

    internal Evaluation.Functions.NumericText TextNumbers => _evaluator.TextNumbers;

    private Evaluation.Functions.TextRules? _textRules;

    internal Evaluation.Functions.TextRules TextRules =>
        _textRules is { } rules && ReferenceEquals(rules.Settings, _workbook.Calculation)
            ? rules
            : _textRules = new Evaluation.Functions.TextRules(_workbook.Calculation);

    public CalculationEngine(Workbook workbook, IFunctionRegistry functions, Evaluator evaluator)
        : this(workbook, functions, evaluator, DefaultHints, new FormulaFormatInference(DefaultHints, new FunctionFormatRules()))
    {
    }

    public CalculationEngine(Workbook workbook, IFunctionRegistry functions, Evaluator evaluator, IFormatHints hints, IFormulaFormatInference formats)
        : this(workbook, functions, evaluator, hints, formats, new PulseOffice.Foundation.Identity.SystemUserIdentity())
    {
    }

    public CalculationEngine(Workbook workbook, IFunctionRegistry functions, Evaluator evaluator, IFormatHints hints, IFormulaFormatInference formats,
        PulseOffice.Foundation.Identity.IUserIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        _identity = identity;
        ArgumentNullException.ThrowIfNull(hints);
        ArgumentNullException.ThrowIfNull(formats);
        _hints = hints;
        _formats = formats;
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(functions);
        ArgumentNullException.ThrowIfNull(evaluator);

        _workbook = workbook;
        _functions = functions;
        _evaluator = evaluator;
        _aggregates = new WholeColumnAggregates(evaluator, ResolveSheet);
        _workbook.CellContentChanged += OnExternalChange;
        _workbook.SheetCleared += _ => _aggregates.Clear();
        _workbook.NamesChanged += (_, _) => { if (_writing == 0 && _formulas.Count > 0) RecalculateAll(); };
        _workbook.RowVisibilityChanged += OnRowVisibilityChanged;
    }

    private void OnRowVisibilityChanged(Worksheet sheet)
    {
        if (_writing > 0 || _workbook.IndexOf(sheet) < 0) return;

        _visibilitySensitive.RemoveWhere(cell => !_formulas.ContainsKey(cell));
        if (_visibilitySensitive.Count == 0 && _volatile.Count == 0) return;

        RecalculateFrom([.. _visibilitySensitive]);
    }

    private const int MaxSpillPasses = 8;

    private int _writing;

    private void OnExternalChange(Worksheet sheet, CellAddress address)
    {
        var index = _workbook.IndexOf(sheet);
        if (index < 0) return;
        _aggregates.Invalidate(new SheetCell(index, address));
        if (_writing > 0) return;

        var cell = new SheetCell(index, address);
        SheetCell? owner;
        _writing++;
        try
        {
            owner = Claim(cell);
            Synchronize(cell, sheet[address]);
        }
        finally
        {
            _writing--;
        }

        RecalculateFrom(owner is { } anchor ? [cell, anchor] : [cell]);
    }

    private SheetCell? Claim(SheetCell cell)
    {
        if (!_spillOwners.Remove(cell, out var owner)) return null;
        _values.Remove(cell);
        return owner;
    }

    private void Synchronize(SheetCell cell, Cell content)
    {
        _circular.Remove(cell);

        if (!content.HasFormula)
        {
            if (content.IsPartOfArrayFormula) return;
            _formulas.Remove(cell);
            _values.Remove(cell);
            _graph.ClearPrecedents(cell);
            return;
        }

        try
        {
            var ast = _parser.Parse(content.Formula!);
            _formulas[cell] = ast;
            RegisterDependencies(cell, ast);
        }
        catch (FormulaSyntaxException)
        {
            _formulas.Remove(cell);
            _graph.ClearPrecedents(cell);
            _values[cell] = CellValue.FromError(CellError.Name);
        }
    }

    public DependencyGraph Graph => _graph;

    public IPivotDataSource? Pivots { get; set; }

    public PulseOffice.Sheets.Engine.Evaluation.ILabelReferences Labels { get; set; } = new PulseOffice.Sheets.Engine.Evaluation.LabelReferences();

    public IWebResourceAccess? Web { get; set; }

    private const int MaximumOffsetNesting = 64;

    private int _offsetNesting;

    public IReadOnlyDictionary<SheetCell, string> RequestedStyles => _requestedStyles;

    public void ClearRequestedStyles() => _requestedStyles.Clear();

    internal void RequestStyle(SheetCell cell, string styleName) => _requestedStyles[cell] = styleName;

    internal CellValue EvaluateOffset(SheetCell formulaCell, int rowDelta, int columnDelta)
    {
        var formula = SheetAt(formulaCell.SheetIndex)?[formulaCell.Address].Formula;
        if (string.IsNullOrEmpty(formula)) return GetValue(formulaCell);
        if (_offsetNesting >= MaximumOffsetNesting || !System.Runtime.CompilerServices.RuntimeHelpers.TryEnsureSufficientExecutionStack())
            return CellValue.FromError(CellError.Value);

        var moved = rowDelta == 0 && columnDelta == 0 ? formula : new Operations.OffsetRewriter(rowDelta, columnDelta).Rewrite(formula);
        if (moved is null) return CellValue.FromError(CellError.Value);

        _offsetNesting++;
        try
        {
            return Evaluate(moved, new WorkbookEvaluationContext(this, formulaCell));
        }
        finally
        {
            _offsetNesting--;
        }
    }

    internal CellValue EvaluateSubstituted(
        SheetCell formulaCell, IReadOnlyList<(SheetCell Cell, CellValue Value)> inputs)
    {
        var formula = SheetAt(formulaCell.SheetIndex)?[formulaCell.Address].Formula;

        var substitutions = new Dictionary<SheetCell, CellValue>();
        foreach (var (cell, value) in inputs) substitutions[cell] = value;

        if (string.IsNullOrEmpty(formula))
            return substitutions.TryGetValue(formulaCell, out var direct)
                ? direct
                : GetValue(formulaCell);

        var context = new SubstitutingContext(
            new WorkbookEvaluationContext(this, formulaCell),
            new SubstitutionSources(
                substitutions,
                cell => SheetAt(cell.SheetIndex)?[cell.Address].Formula,
                (text, inner) => Evaluate(text, inner),
                cell => new WorkbookEvaluationContext(this, cell),
                name => Workbook.FindSheet(name) is { } sheet ? Workbook.IndexOf(sheet) : -1));

        foreach (var cell in SubstitutionOrder(formulaCell, substitutions.Keys)) context.Prepare(cell);

        return Evaluate(formula, context);
    }

    private IReadOnlyList<SheetCell> SubstitutionOrder(SheetCell target, IEnumerable<SheetCell> inputs)
    {
        var affected = _graph.TransitiveDependents(inputs);
        if (affected.Count == 0) return [];

        var bySheet = affected.Where(_formulas.ContainsKey).GroupBy(cell => cell.SheetIndex)
            .ToDictionary(group => group.Key, group => group.ToList());
        var needed = new HashSet<SheetCell>();
        var queue = new Queue<SheetCell>();
        queue.Enqueue(target);

        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var precedent in _graph.DirectCellPrecedents(cell))
                if (affected.Contains(precedent) && _formulas.ContainsKey(precedent) && needed.Add(precedent)) queue.Enqueue(precedent);

            foreach (var (sheetIndex, range) in _graph.PrecedentRanges(cell))
                if (bySheet.TryGetValue(sheetIndex, out var candidates))
                    foreach (var candidate in candidates)
                        if (range.Contains(candidate.Address) && needed.Add(candidate)) queue.Enqueue(candidate);
        }

        needed.Remove(target);
        return _graph.TopologicalOrder(needed).Order;
    }

    private CellValue Evaluate(string formula, IEvaluationContext context)
    {
        try
        {
            var ast = _parser.Parse(formula);

            return _evaluator.EvaluateRaw(ast, context) switch
            {
                ScalarValue scalar => scalar.Value,
                ArrayValue array when array.Rows.Count > 0 && array.Rows[0].Count > 0 => array.Rows[0][0],
                RangeValue range => context.GetRangeValues(range.SheetName, range.Range).FirstOrDefault(),
                _ => CellValue.FromError(CellError.Value),
            };
        }
        catch (Parsing.FormulaSyntaxException)
        {
            return CellValue.FromError(CellError.Value);
        }
    }

    public IReadOnlyDictionary<SheetCell, CellRange> SpillRanges => _spillRanges;

    public bool IsSpilledInto(SheetCell cell) => _spillOwners.ContainsKey(cell);
    public IFunctionRegistry Functions => _functions;
    public IReadOnlySet<SheetCell> CircularCells => _circular;

    private const int MaxOnDemandDepth = 256;

    internal int SpillBottom(int sheetIndex) =>
        _spillRanges.Where(pair => pair.Key.SheetIndex == sheetIndex).Select(pair => pair.Value.End.Row).DefaultIfEmpty(-1).Max();

    internal int SpillRight(int sheetIndex) =>
        _spillRanges.Where(pair => pair.Key.SheetIndex == sheetIndex).Select(pair => pair.Value.End.Column).DefaultIfEmpty(-1).Max();

    private RecalculationPass? _pass;

    private sealed class RecalculationPass(IEnumerable<SheetCell> order, HashSet<SheetCell> spilled)
    {
        public HashSet<SheetCell> Pending { get; } = [.. order];
        public HashSet<SheetCell> InProgress { get; } = [];
        public HashSet<SheetCell> SelfReads { get; } = [];
        public HashSet<SheetCell> Evaluating { get; } = [];
        public HashSet<SheetCell> Spilled => spilled;
    }

    private void Compute(SheetCell cell)
    {
        var pass = _pass!;
        pass.Pending.Remove(cell);
        pass.InProgress.Add(cell);
        try
        {
            _circular.Remove(cell);
            var context = new WorkbookEvaluationContext(this, cell);
            pass.Evaluating.Add(cell);
            EvalValue result;
            try
            {
                result = _aggregates.TryEvaluate(cell, _formulas[cell], context) ?? _evaluator.EvaluateRaw(_formulas[cell], context);
            }
            finally
            {
                pass.Evaluating.Remove(cell);
            }

            var produced = Materialize(cell, result, context, pass.Spilled);
            if (pass.SelfReads.Remove(cell))
            {
                _circular.Add(cell);
                produced = CellValue.FromError(CellError.Circular);
            }
            _values[cell] = produced;
            WriteBack(cell, produced);
            RecordArrayUse(cell, context.UsedArrays);
        }
        finally
        {
            pass.InProgress.Remove(cell);
        }
    }

    private void Iterate(IReadOnlySet<SheetCell> circular)
    {
        var cells = circular.OrderBy(cell => cell.SheetIndex).ThenBy(cell => cell.Address).ToList();
        foreach (var cell in cells)
        {
            _circular.Remove(cell);
            if (!_values.TryGetValue(cell, out var previous) || previous.IsError) _values[cell] = CellValue.Number(0);
        }

        var settings = _workbook.Calculation;
        for (var step = 0; step < settings.MaxIterations; step++)
        {
            var largestChange = 0.0;
            foreach (var cell in cells)
            {
                var before = _values[cell];
                var context = new WorkbookEvaluationContext(this, cell);
                var after = _coercion.ToScalar(_evaluator.EvaluateRaw(_formulas[cell], context), context);
                _values[cell] = after;
                largestChange = Math.Max(largestChange, Change(before, after));
            }
            if (largestChange <= settings.MaxChange) break;
        }

        foreach (var cell in cells) WriteBack(cell, _values[cell]);
    }

    private static double Change(CellValue before, CellValue after) =>
        before.IsNumeric && after.IsNumeric ? Math.Abs(after.AsNumber() - before.AsNumber())
        : before.Equals(after) ? 0 : double.PositiveInfinity;

    private void RecordArrayUse(SheetCell cell, bool usedArrays)
    {
        if (SheetAt(cell.SheetIndex) is not { } sheet || sheet[cell.Address].UsesArrays == usedArrays) return;
        sheet[cell.Address] = sheet[cell.Address] with { UsesArrays = usedArrays };
    }

    private void ComputeOnDemand(SheetCell cell)
    {
        if (_pass is not { } pass || !pass.Pending.Contains(cell)) return;
        if (pass.InProgress.Count >= MaxOnDemandDepth) return;
        Compute(cell);
    }

    public CellValue GetValue(SheetCell cell)
    {
        ComputeOnDemand(cell);
        if (_pass is { } pass && pass.Evaluating.Contains(cell))
        {
            pass.SelfReads.Add(cell);
            return CellValue.FromError(CellError.Circular);
        }
        if (_circular.Contains(cell)) return CellValue.FromError(CellError.Circular);
        if (_values.TryGetValue(cell, out var value)) return value;

        var sheet = SheetAt(cell.SheetIndex);
        return sheet is null ? CellValue.Empty : sheet[cell.Address].Value;
    }

    private void Track(SheetCell cell, Cell previous, CellValue after, string? formulaAfter)
    {
        if (!_workbook.Changes.IsRecording) return;

        if (previous.Value.Equals(after) && previous.Formula == formulaAfter) return;

        _workbook.Changes.Record(new TrackedChange
        {
            Id = Guid.NewGuid().ToString("N"),
            SheetIndex = cell.SheetIndex,
            Address = cell.Address,
            Before = previous.Value,
            After = after,
            FormulaBefore = previous.Formula,
            FormulaAfter = formulaAfter,
            Author = _identity.Name,
        });
    }

    public void SetFormula(SheetCell cell, string formula)
    {
        _writing++;
        try
        {
            SetFormulaCore(cell, formula);
        }
        finally
        {
            _writing--;
        }
    }

    private void SetFormulaCore(SheetCell cell, string formula)
    {
        var sheet = SheetAt(cell.SheetIndex) ?? throw new ArgumentOutOfRangeException(nameof(cell));

        Track(cell, sheet[cell.Address], sheet[cell.Address].Value, formula);

        var owner = Claim(cell);
        sheet.SetFormula(cell.Address, formula);

        try
        {
            var ast = _parser.Parse(formula);
            _formulas[cell] = ast;
            RegisterDependencies(cell, ast);
        }
        catch (FormulaSyntaxException)
        {
            _formulas.Remove(cell);
            _graph.ClearPrecedents(cell);
            _values[cell] = CellValue.FromError(CellError.Name);
        }

        RecalculateFrom(owner is { } anchor ? [cell, anchor] : [cell]);
    }

    public void SetArrayFormula(SheetCell anchor, CellRange area, string formula)
    {
        _writing++;
        try
        {
            SetArrayFormulaCore(anchor, area, formula);
        }
        finally
        {
            _writing--;
        }
    }

    private void SetArrayFormulaCore(SheetCell anchor, CellRange area, string formula)
    {
        var sheet = SheetAt(anchor.SheetIndex) ?? throw new ArgumentOutOfRangeException(nameof(anchor));
        if (anchor.Address != area.Start)
            throw new ArgumentException("The anchor must be the top-left corner of the area.", nameof(anchor));

        sheet.SetArrayFormula(area, formula);

        try
        {
            var ast = _parser.Parse(formula);
            _formulas[anchor] = ast;
            RegisterDependencies(anchor, ast);
        }
        catch (FormulaSyntaxException)
        {
            _formulas.Remove(anchor);
            _graph.ClearPrecedents(anchor);
            _values[anchor] = CellValue.FromError(CellError.Name);
        }

        RecalculateFrom([anchor]);
    }

    public void SetValue(SheetCell cell, CellValue value)
    {
        _writing++;
        try
        {
            SetValueCore(cell, value);
        }
        finally
        {
            _writing--;
        }
    }

    private void SetValueCore(SheetCell cell, CellValue value)
    {
        var sheet = SheetAt(cell.SheetIndex) ?? throw new ArgumentOutOfRangeException(nameof(cell));

        Track(cell, sheet[cell.Address], value, null);

        var owner = Claim(cell);
        sheet.SetValue(cell.Address, value);

        ReleaseSpill(cell);
        _formulas.Remove(cell);
        _aggregates.Forget(cell);
        _values.Remove(cell);
        _graph.ClearPrecedents(cell);
        _circular.Remove(cell);

        RecalculateFrom(owner is { } anchor ? [cell, anchor] : [cell]);
    }

    public IReadOnlyList<CellValue> EvaluateExpression(string formula, SheetCell origin)
    {
        ArgumentNullException.ThrowIfNull(formula);

        var ast = _parser.Parse(formula);
        var context = new WorkbookEvaluationContext(this, origin);

        return _evaluator.EvaluateRaw(ast, context) switch
        {
            ScalarValue scalar => [scalar.Value],
            ArrayValue array => [.. array.Rows.SelectMany(row => row)],
            RangeValue range => [.. context.GetRangeValues(range.SheetName, range.Range)],
            _ => [],
        };
    }

    public bool AutomaticCalculation { get; set; } = true;

    public bool HasPendingCalculation { get; private set; }

    public void RecalculateAll()
    {
        _writing++;
        try
        {
            RecalculateAllCore();
        }
        finally
        {
            _writing--;
        }
    }

    private void RecalculateAllCore()
    {
        HasPendingCalculation = false;

        var previousSpills = _spillOwners.Keys.ToList();
        _values.Clear();
        _circular.Clear();
        _graph.Clear();
        _spillRanges.Clear();
        _spillOwners.Clear();
        _blockedSpills.Clear();

        _formulas.Clear();
        _aggregates.Clear();
        _volatile.Clear();
        _visibilitySensitive.Clear();
        CollectLoadedSpills();
        _loadedSpills.UnionWith(previousSpills);
        for (var s = 0; s < _workbook.Sheets.Count; s++)
        {
            foreach (var (address, cell) in _workbook.Sheets[s].NonEmptyCells())
            {
                if (!cell.HasFormula) continue;
                var target = new SheetCell(s, address);
                try
                {
                    var ast = _parser.Parse(cell.Formula!);
                    _formulas[target] = ast;
                    RegisterDependencies(target, ast);
                }
                catch (FormulaSyntaxException)
                {
                    _values[target] = CellValue.FromError(CellError.Name);
                }
            }
        }

        Recalculate(_formulas.Keys.ToHashSet());
        ClearStaleSpills();
    }

    private readonly HashSet<SheetCell> _loadedSpills = [];

    private void CollectLoadedSpills()
    {
        _loadedSpills.Clear();
        for (var index = 0; index < _workbook.Sheets.Count; index++)
        {
            var sheet = _workbook.Sheets[index];
            foreach (var (address, cell) in sheet.NonEmptyCells().Where(pair => pair.Value.IsSpilled).ToList())
            {
                _loadedSpills.Add(new SheetCell(index, address));
                sheet[address] = cell with { IsSpilled = false };
            }
        }
    }

    private void ClearStaleSpills()
    {
        foreach (var cell in _loadedSpills.Where(cell => !_spillOwners.ContainsKey(cell)))
            if (SheetAt(cell.SheetIndex) is { } sheet && !sheet[cell.Address].HasFormula)
                sheet[cell.Address] = sheet[cell.Address] with { Value = CellValue.Empty };
        _loadedSpills.Clear();

        for (var index = 0; index < _workbook.Sheets.Count; index++)
        {
            var sheet = _workbook.Sheets[index];
            foreach (var (address, cell) in sheet.NonEmptyCells().Where(pair => pair.Value.SpillRange is not null).ToList())
                if (!_spillRanges.ContainsKey(new SheetCell(index, address)))
                    sheet[address] = cell with { SpillRange = null };
        }
    }

    public void RecalculateFrom(IEnumerable<SheetCell> changed)
    {
        _writing++;
        try
        {
            RecalculateFromCore(changed);
        }
        finally
        {
            _writing--;
        }
    }

    private readonly List<SheetCell> _batched = [];

    private int _batchDepth;

    public void Batch(Action write)
    {
        ArgumentNullException.ThrowIfNull(write);
        _batchDepth++;
        try
        {
            write();
        }
        finally
        {
            _batchDepth--;
        }

        if (_batchDepth > 0 || _batched.Count == 0) return;
        var changed = _batched.Distinct().ToList();
        _batched.Clear();
        RecalculateFrom(changed);
    }

    private void RecalculateFromCore(IEnumerable<SheetCell> changed)
    {
        if (_batchDepth > 0)
        {
            _batched.AddRange(changed);
            return;
        }

        if (!AutomaticCalculation)
        {
            HasPendingCalculation = true;
            return;
        }

        var touched = changed.ToList();
        _volatile.RemoveWhere(cell => !_formulas.ContainsKey(cell));
        var affected = _graph.TransitiveDependents([.. touched, .. _volatile]).ToHashSet();
        affected.UnionWith(_volatile);

        foreach (var (anchor, area) in _blockedSpills)
            if (touched.Any(c => c.SheetIndex == anchor.SheetIndex && area.Contains(c.Address)))
                affected.Add(anchor);

        Recalculate(affected);
    }

    private void Recalculate(IReadOnlySet<SheetCell> scope, int depth = 0)
    {
        var formulaCells = scope.Where(_formulas.ContainsKey).ToHashSet();

        var (order, circular) = _graph.TopologicalOrder(formulaCells);

        var iterative = _workbook.Calculation.Iterative && circular.Count > 0;
        var certain = iterative ? circular : _graph.FindCircular(circular, cellsOnly: true);
        var possible = iterative ? [] : circular.Where(cell => !certain.Contains(cell)).ToList();
        if (!iterative)
            foreach (var cell in certain)
            {
                _circular.Add(cell);
                _values[cell] = CellValue.FromError(CellError.Circular);
                WriteBack(cell, _values[cell]);
            }

        var spilled = new HashSet<SheetCell>();
        var outerPass = _pass;
        _pass = new RecalculationPass(order.Concat(possible), spilled);
        try
        {
            if (iterative) Iterate(circular);
            foreach (var cell in order.Concat(possible))
                if (_pass.Pending.Contains(cell)) Compute(cell);
        }
        finally
        {
            _pass = outerPass;
        }

        if (spilled.Count > 0)
        {
            var dependents = _graph.TransitiveDependents(spilled)
                                   .Where(_formulas.ContainsKey)
                                   .ToHashSet();
            if (dependents.Count > 0 && depth < MaxSpillPasses) Recalculate(dependents, depth + 1);
        }
    }

    private CellValue Materialize(
    SheetCell cell, EvalValue result, IEvaluationContext context, HashSet<SheetCell> spilled)
    {
        var sheet = SheetAt(cell.SheetIndex);
        var declared = sheet?[cell.Address].ArrayFormulaRange;

        if (declared is { } area)
            return FillDeclaredArea(cell, area, result, context, spilled);

        if (!context.ImplicitIntersection && result is RangeValue { Range.IsSingleCell: false } range)
            result = new ArrayValue([.. Enumerable.Range(0, range.Range.RowCount).Select(row =>
                (IReadOnlyList<CellValue>)[.. Enumerable.Range(0, range.Range.ColumnCount).Select(column =>
                    context.GetCellValue(range.SheetName, range.Range.Start.Offset(row, column)))])]);

        if (context.ImplicitIntersection || result is not ArrayValue array || (array.RowCount <= 1 && array.ColumnCount <= 1))
        {
            ReleaseSpill(cell, spilled);
            var scalar = _coercion.ToScalar(result, context);
            return scalar.IsEmpty ? CellValue.Number(0) : scalar;
        }

        return Spill(cell, array, spilled);
    }

    private CellValue FillDeclaredArea(
    SheetCell cell, CellRange area, EvalValue result, IEvaluationContext context, HashSet<SheetCell> spilled)
    {
        var sheet = SheetAt(cell.SheetIndex);
        if (sheet is null || cell.Address != area.Start)
            return _coercion.ToScalar(result, context);

        var array = result as ArrayValue;

        for (var row = 0; row < area.RowCount; row++)
            for (var column = 0; column < area.ColumnCount; column++)
            {
                var target = new SheetCell(cell.SheetIndex,
                    new CellAddress(area.Start.Row + row, area.Start.Column + column));

                var value = array is null
                    ? _coercion.ToScalar(result, context)
                    : ElementAt(array, row, column);

                if (target != cell)
                {
                    sheet[target.Address] = sheet[target.Address] with { Value = value };
                    _values[target] = value;
                    spilled.Add(target);
                }
                else
                {
                    _values[target] = value;
                }
            }

        return _values[cell];
    }

    private CellValue Spill(SheetCell anchor, ArrayValue array, HashSet<SheetCell> spilled)
    {
        var sheet = SheetAt(anchor.SheetIndex);
        if (sheet is null) return CellValue.FromError(CellError.Reference);

        var end = new CellAddress(
            anchor.Address.Row + array.RowCount - 1,
            anchor.Address.Column + array.ColumnCount - 1);

        if (!end.IsValid) return CellValue.FromError(CellError.Reference);

        var target = new CellRange(anchor.Address, end);
        _blockedSpills.Remove(anchor);

        ReleaseSpill(anchor, spilled);

        foreach (var address in target.Cells())
        {
            if (address == anchor.Address) continue;
            var occupant = new SheetCell(anchor.SheetIndex, address);

            var existing = sheet[address];
            var ownedByOthers = _spillOwners.TryGetValue(occupant, out var owner) && owner != anchor;

            if (existing.HasFormula || (!existing.Value.IsEmpty && !_loadedSpills.Contains(occupant)) || ownedByOthers)
            {
                _blockedSpills[anchor] = target;
                return CellValue.FromError(CellError.Spill);
            }
        }

        for (var row = 0; row < array.RowCount; row++)
            for (var column = 0; column < array.ColumnCount; column++)
            {
                var address = new CellAddress(anchor.Address.Row + row, anchor.Address.Column + column);
                var value = ElementAt(array, row, column);
                var occupant = new SheetCell(anchor.SheetIndex, address);

                if (address == anchor.Address) continue;

                sheet[address] = sheet[address] with { Value = value };
                _values[occupant] = value;
                _spillOwners[occupant] = anchor;
                spilled.Add(occupant);
            }

        _spillRanges[anchor] = target;
        sheet[anchor.Address] = sheet[anchor.Address] with { SpillRange = target };
        return ElementAt(array, 0, 0);
    }

    private void ReleaseSpill(SheetCell anchor, HashSet<SheetCell>? spilled = null)
    {
        if (!_spillRanges.TryGetValue(anchor, out var previous)) return;

        var sheet = SheetAt(anchor.SheetIndex);
        foreach (var address in previous.Cells())
        {
            if (address == anchor.Address) continue;
            var occupant = new SheetCell(anchor.SheetIndex, address);

            if (!_spillOwners.TryGetValue(occupant, out var owner) || owner != anchor) continue;

            _spillOwners.Remove(occupant);
            _values.Remove(occupant);
            if (sheet is not null) sheet[address] = sheet[address] with { Value = CellValue.Empty };
            spilled?.Add(occupant);
        }

        _spillRanges.Remove(anchor);
        if (sheet is not null && sheet[anchor.Address].SpillRange is not null)
            sheet[anchor.Address] = sheet[anchor.Address] with { SpillRange = null };
    }

    private static CellValue ElementAt(ArrayValue array, int row, int column)
    {
        if (row >= array.Rows.Count) return CellValue.FromError(CellError.NotAvailable);
        var line = array.Rows[row];

        var value = line.Count == 1 && column > 0 ? line[0]
            : column < line.Count ? line[column] : CellValue.FromError(CellError.NotAvailable);
        return value.IsEmpty ? CellValue.Number(0) : value;
    }

    private void WriteBack(SheetCell cell, CellValue value)
    {
        var sheet = SheetAt(cell.SheetIndex);
        if (sheet is null) return;
        var inferred = _formulas.TryGetValue(cell, out var ast)
            ? _formats.Infer(ast, new FormatLookup(this, cell))
            : 0;
        sheet[cell.Address] = sheet[cell.Address] with { Value = value, InferredFormatId = inferred };
    }

    private sealed class FormatLookup(CalculationEngine engine, SheetCell origin) : IFormatLookup
    {
        private readonly int _defaultSheet = origin.SheetIndex;

        public NumberFormatCatalog Formats => engine._workbook.NumberFormats;

        public FormatHint LastIn(string? sheetName, CellRange range)
        {
            var index = engine.ResolveSheet(sheetName, _defaultSheet);
            if (engine.SheetAt(index) is not { } sheet) return FormatHint.None;
            for (var row = Math.Min(range.End.Row, sheet.LastUsedRow); row >= range.Start.Row; row--)
                for (var column = Math.Min(range.End.Column, sheet.LastUsedColumn); column >= range.Start.Column; column--)
                {
                    var address = new CellAddress(row, column);
                    if (sheet.TryGetCell(address, out var cell) && !cell.Value.IsEmpty) return At(sheetName, address);
                }
            return FormatHint.None;
        }

        public CellValue? Evaluate(FormulaNode node)
        {
            var context = new WorkbookEvaluationContext(engine, origin);
            return engine._coercion.ToScalar(engine._evaluator.EvaluateRaw(node, context), context);
        }

        public FormatHint At(string? sheetName, CellAddress address)
        {
            var index = engine.ResolveSheet(sheetName, _defaultSheet);
            if (engine.SheetAt(index) is not { } sheet) return FormatHint.None;
            var formats = engine._workbook.NumberFormats;
            var own = engine._workbook.Styles.Get(sheet.StyleIdAt(address)).NumberFormatId;
            if (own != 0) return engine._hints.Of(own, formats);
            return sheet.TryGetCell(address, out var target) && target.HasFormula ? engine._hints.Of(target.InferredFormatId, formats) : FormatHint.None;
        }

        public FormulaNode? Expand(NameNode name)
        {
            if (engine._workbook.ResolveName(name.Name, engine.SheetAt(_defaultSheet)?.Name)?.Expression is not { } expression) return null;
            try { return engine._evaluator.Names.Expand(expression, origin.Address); }
            catch (FormulaSyntaxException) { return null; }
        }
    }

    private void RegisterDependencies(SheetCell cell, FormulaNode ast)
    {
        var cells = new List<SheetCell>();
        var ranges = new List<(int, CellRange)>();
        Collect(ast, cell.SheetIndex, cells, ranges, cell);
        _graph.SetPrecedents(cell, cells, ranges);
    }

    private static readonly HashSet<string> GeometryFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "ROW", "COLUMN", "ROWS", "COLUMNS", "AREAS", "SHEET", "ISREF",
    };

    private static readonly HashSet<string> VolatileFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "NOW", "TODAY", "RAND", "RANDBETWEEN", "INDIRECT", "OFFSET", "INFO", "CELL",
    };

    private static readonly HashSet<string> VisibilitySensitiveFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "SUBTOTAL", "AGGREGATE",
    };

    private readonly HashSet<SheetCell> _volatile = [];

    private readonly HashSet<SheetCell> _visibilitySensitive = [];

    private void Collect(FormulaNode node, int defaultSheet, List<SheetCell> cells, List<(int, CellRange)> ranges, SheetCell owner)
    {
        var collector = new DependencyCollector(this, defaultSheet, cells, ranges, owner);
        node.Accept(collector);
        if (collector.Volatile) _volatile.Add(owner);
        else _volatile.Remove(owner);
        if (collector.VisibilitySensitive) _visibilitySensitive.Add(owner);
        else _visibilitySensitive.Remove(owner);
    }

    private sealed class DependencyCollector(CalculationEngine engine, int defaultSheet, List<SheetCell> cells, List<(int, CellRange)> ranges, SheetCell owner)
        : FormulaNodeVisitor<bool>
    {
        public override bool VisitReference(ReferenceNode node) => Cell(node.SheetName, node.Address);

        public override bool VisitSpillReference(SpillReferenceNode node) => Cell(node.SheetName, node.Anchor);

        public override bool VisitRange(RangeNode node)
        {
            if (node.SpansSheets)
            {
                foreach (var name in engine.SheetsBetween(node.SheetName!, node.LastSheetName!))
                    ranges.Add((engine.ResolveSheet(name, defaultSheet), node.Range));
                return true;
            }
            var sheet = engine.ResolveSheet(node.SheetName, defaultSheet);
            if (sheet >= 0) ranges.Add((sheet, node.Range));
            return true;
        }

        public override bool VisitUnary(UnaryNode node) => node.Operand.Accept(this);

        public override bool VisitBinary(BinaryNode node) => node.Left.Accept(this) & node.Right.Accept(this);

        public bool Volatile { get; private set; }

        public bool VisibilitySensitive { get; private set; }

        public override bool VisitFunction(FunctionNode node)
        {
            if (VolatileFunctions.Contains(node.Name)) Volatile = true;
            if (VisibilitySensitiveFunctions.Contains(node.Name)) VisibilitySensitive = true;
            if (GeometryFunctions.Contains(node.Name))
                return All(node.Arguments.Where(argument => argument is not (ReferenceNode or RangeNode or SpillReferenceNode)));
            return All(node.Arguments);
        }

        public override bool VisitArray(ArrayNode node) => All(node.Rows.SelectMany(row => row));

        public override bool VisitInvocation(InvocationNode node) => node.Callee.Accept(this) & All(node.Arguments);

        private readonly HashSet<string> _expanding = new(StringComparer.OrdinalIgnoreCase);

        public override bool VisitName(NameNode node)
        {
            if (engine._workbook.ResolveName(node.Name, engine.SheetAt(defaultSheet)?.Name)?.Expression is not { } expression) return true;
            if (!_expanding.Add(node.Name)) return true;
            try { return engine._evaluator.Names.Expand(expression, owner.Address).Accept(this); }
            catch (FormulaSyntaxException) { return true; }
            finally { _expanding.Remove(node.Name); }
        }

        protected override bool Otherwise(FormulaNode node) => true;

        private bool Cell(string? sheetName, CellAddress address)
        {
            var sheet = engine.ResolveSheet(sheetName, defaultSheet);
            if (sheet >= 0) cells.Add(new SheetCell(sheet, address));
            return true;
        }

        private bool All(IEnumerable<FormulaNode> nodes)
        {
            foreach (var node in nodes) node.Accept(this);
            return true;
        }
    }

    internal IReadOnlyList<string> SheetsBetween(string firstSheet, string lastSheet)
    {
        var first = ResolveSheet(firstSheet, -1);
        var last = ResolveSheet(lastSheet, -1);
        if (first < 0 || last < 0) return [];
        return [.. Enumerable.Range(Math.Min(first, last), Math.Abs(last - first) + 1).Select(index => _workbook.Sheets[index].Name)];
    }

    private int ResolveSheet(string? name, int current)
    {
        if (name is null) return current;
        var sheet = _workbook.FindSheet(name);
        return sheet is null ? -1 : _workbook.IndexOf(sheet);
    }

    public Worksheet? SheetAtIndex(int index) => SheetAt(index);

    public IEvaluationContext ContextAt(SheetCell cell) => new WorkbookEvaluationContext(this, cell);

    internal Worksheet? SheetAt(int index) =>
        index >= 0 && index < _workbook.Sheets.Count ? _workbook.Sheets[index] : null;

    public Workbook Workbook => _workbook;
}

internal sealed class WorkbookEvaluationContext(CalculationEngine engine, SheetCell current) : IEvaluationContext
{
    public SheetCell CurrentCell => current;

    public Evaluation.Functions.NumberToText NumberText => engine.NumberText;

    public Evaluation.Functions.NumericText TextNumbers => engine.TextNumbers;

    public bool ImplicitIntersection { get; } = engine.SheetAt(current.SheetIndex)?[current.Address] is { ImplicitIntersection: true, IsPartOfArrayFormula: false };

    public bool UsedArrays { get; private set; }

    public Evaluation.Functions.TextRules TextRules => engine.TextRules;

    public void NoteArrayUse()
    {
        if (!ImplicitIntersection) UsedArrays = true;
    }

    public CellValue GetCellValue(string? sheetName, CellAddress address)
    {
        var index = ResolveSheetIndex(sheetName);
        if (index < 0) return CellValue.FromError(CellError.Reference);
        return engine.GetValue(new SheetCell(index, address));
    }

    public CellRange? ContentWithin(string? sheetName, CellRange range)
    {
        var index = ResolveSheetIndex(sheetName);
        if (index < 0 || engine.SheetAt(index) is not { } sheet) return range;
        var lastRow = Math.Min(range.End.Row, Math.Max(sheet.LastUsedRow, engine.SpillBottom(index)));
        var lastColumn = Math.Min(range.End.Column, Math.Max(sheet.LastUsedColumn, engine.SpillRight(index)));
        if (lastRow < range.Start.Row || lastColumn < range.Start.Column) return null;
        return new CellRange(range.Start, new CellAddress(lastRow, lastColumn));
    }

    public IEnumerable<CellValue> GetRangeValues(string? sheetName, CellRange range)
    {
        var index = ResolveSheetIndex(sheetName);
        if (index < 0)
        {
            yield return CellValue.FromError(CellError.Reference);
            yield break;
        }

        var sheet = engine.SheetAt(index);
        if (sheet is null) yield break;

        var lastRow = Math.Max(sheet.LastUsedRow, engine.SpillBottom(index));
        var lastColumn = Math.Max(sheet.LastUsedColumn, engine.SpillRight(index));
        for (var row = range.Start.Row; row <= range.End.Row; row++)
        {
            if (row > lastRow)
            {
                for (long rest = (long)(range.End.Row - row + 1) * range.ColumnCount; rest > 0; rest--) yield return CellValue.Empty;
                yield break;
            }
            for (var column = range.Start.Column; column <= range.End.Column; column++)
                yield return column > lastColumn
                    ? CellValue.Empty
                    : engine.GetValue(new SheetCell(index, new CellAddress(row, column)));
        }
    }

    public string? ResolveName(string name) =>
        engine.Workbook.ResolveName(name, engine.SheetAt(current.SheetIndex)?.Name)?.Expression
        ?? DatabaseRangeExpression(name)
        ?? LabelExpression(name);

    private string? LabelExpression(string name)
    {
        if (engine.SheetAt(current.SheetIndex) is not { } sheet
            || engine.Labels.Find(sheet, name, engine.Workbook.Calculation.AutomaticFindLabels) is not { } range) return null;
        return $"{range.Start.ToA1(true, true)}:{range.End.ToA1(true, true)}";
    }

    private string? DatabaseRangeExpression(string name)
    {
        if (engine.Workbook.DatabaseRanges.Find(name) is not { } database || engine.SheetAt(database.SheetIndex) is not { } sheet) return null;
        var range = database.Range;
        return $"'{sheet.Name.Replace("'", "''", StringComparison.Ordinal)}'.{range.Start.ToA1(true, true)}:{range.End.ToA1(true, true)}";
    }

    public bool SheetExists(string sheetName) => engine.Workbook.FindSheet(sheetName) is not null;

    public string CurrentSheetName => engine.SheetAt(current.SheetIndex)?.Name ?? string.Empty;

    public int SheetCount => engine.Workbook.Sheets.Count;

    public int SheetIndex(string? sheetName)
    {
        if (sheetName is null) return current.SheetIndex + 1;
        var sheet = engine.Workbook.FindSheet(sheetName);
        return sheet is null ? 0 : engine.Workbook.IndexOf(sheet) + 1;
    }

    public IReadOnlyList<string> SheetsBetween(string firstSheet, string lastSheet) => engine.SheetsBetween(firstSheet, lastSheet);

    public CellRange? ArrayArea => engine.SheetAt(current.SheetIndex)?[current.Address].ArrayFormulaRange;

    public string? GetFormulaText(string? sheetName, CellAddress address)
    {
        var index = ResolveSheetIndex(sheetName);
        if (index < 0 || engine.SheetAt(index) is not { } sheet) return null;
        var cell = sheet[address];
        if (cell.ArrayFormulaRange is not { } array) return cell.Formula;
        var anchor = sheet[array.Start].Formula;
        return anchor is null ? null : "{" + (anchor.StartsWith('=') ? anchor : "=" + anchor) + "}";
    }

    public int GetStyle(string? sheetName, CellAddress address)
    {
        var index = ResolveSheetIndex(sheetName);
        return index < 0 ? 0 : engine.SheetAt(index)?.StyleIdAt(address) ?? 0;
    }

    public RowVisibility RowVisibilityAt(string? sheetName, int row)
    {
        var index = ResolveSheetIndex(sheetName);
        if (index < 0 || engine.SheetAt(index) is not { } sheet || !sheet.Layout.IsRowHidden(row)) return RowVisibility.Visible;
        return sheet.Layout.IsRowFiltered(row)
            || sheet.AutoFilter is { Range: { } filtered, Filters.Count: > 0 } && filtered.Contains(new CellAddress(row, filtered.Start.Column))
            ? RowVisibility.Filtered
            : RowVisibility.Hidden;
    }

    public CellRange? GetSpillRange(string? sheetName, CellAddress anchor)
    {
        var index = ResolveSheetIndex(sheetName);
        if (index < 0) return null;

        return engine.SpillRanges.TryGetValue(new SheetCell(index, anchor), out var range)
            ? range : null;
    }

    public CellValue EvaluateWith(
        SheetCell formula, IReadOnlyList<(SheetCell Cell, CellValue Value)> inputs) =>
        engine.EvaluateSubstituted(formula, inputs);

    public void RequestStyle(string styleName) => engine.RequestStyle(current, styleName);

    public CellValue LookupPivot(
        string? sheetName, CellAddress target, string dataField,
        IReadOnlyList<(string Field, string Item)> filters)
    {
        var index = ResolveSheetIndex(sheetName);
        if (index < 0) return CellValue.FromError(CellError.Reference);

        return engine.Pivots?.Lookup(index, target, dataField, filters)
            ?? CellValue.FromError(CellError.Reference);
    }

    public string? FetchResource(string url) => engine.Web?.Fetch(url);

    public CellValue EvaluateOffset(string? sheetName, CellAddress formula, int rowDelta, int columnDelta)
    {
        var index = ResolveSheetIndex(sheetName);
        return index < 0
            ? CellValue.FromError(CellError.Reference)
            : engine.EvaluateOffset(new SheetCell(index, formula), rowDelta, columnDelta);
    }

    private int ResolveSheetIndex(string? sheetName)
    {
        if (sheetName is null) return current.SheetIndex;
        var sheet = engine.Workbook.FindSheet(sheetName);
        return sheet is null ? -1 : engine.Workbook.IndexOf(sheet);
    }
}

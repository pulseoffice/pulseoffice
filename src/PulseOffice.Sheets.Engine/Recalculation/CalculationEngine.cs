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




    private readonly Dictionary<SheetCell, string> _requestedStyles = [];

    private readonly SpillArea _spills;

    private readonly WholeColumnAggregates _aggregates;

    private readonly TableReferenceResolution _tables = new();

    private readonly FormulaDependencies _dependencies;

    private readonly TableColumnFollower _columns;
    private readonly Operations.TableReferenceOwners _tableOwners = new();

    public event EventHandler? Recalculated;

    internal ITableReferenceResolution Tables => _tables;

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
        _spills = new SpillArea(workbook, _values, _coercion);
        _aggregates = new WholeColumnAggregates(evaluator, ResolveSheet);
        _dependencies = new FormulaDependencies(this, evaluator.Names, _tables);
        _columns = new TableColumnFollower(_tables);
        _columns.Reset(workbook);
        _workbook.DatabaseRanges.Changed += OnTablesChanged;
        _workbook.CellContentChanged += OnExternalChange;
        _workbook.SheetCleared += _ => _aggregates.Clear();
        _workbook.NamesChanged += (_, _) => { if (_writing == 0 && _formulas.Count > 0) RecalculateAll(); };
        _workbook.RowVisibilityChanged += OnRowVisibilityChanged;
        _workbook.MergesChanged += OnMergesChanged;
    }

    private void OnMergesChanged(Worksheet sheet)
    {
        if (_writing > 0 || !AutomaticCalculation) return;
        var index = _workbook.IndexOf(sheet);
        if (index < 0) return;
        var anchors = _spills.Anchors.Where(anchor => anchor.SheetIndex == index && _formulas.ContainsKey(anchor)).ToList();
        if (anchors.Count > 0) RecalculateFrom(anchors);
    }

    private void OnRowVisibilityChanged(Worksheet sheet)
    {
        if (_writing > 0 || _workbook.IndexOf(sheet) < 0) return;

        _visibilitySensitive.RemoveWhere(cell => !_formulas.ContainsKey(cell));
        if (_visibilitySensitive.Count == 0 && _volatile.Count == 0) return;

        RecalculateFrom([.. _visibilitySensitive]);
    }

    private void OnTablesChanged(object? sender, EventArgs e)
    {
        _columns.Reset(_workbook);
        if (_writing > 0 || !AutomaticCalculation) return;
        var readers = _tableReaders.Where(_formulas.ContainsKey).ToList();
        foreach (var reader in readers) RegisterDependencies(reader, _formulas[reader]);
        readers.AddRange(_spills.Anchors.Where(anchor => _formulas.ContainsKey(anchor) && !readers.Contains(anchor)));
        if (readers.Count == 0) return;
        RecalculateFrom(readers);
    }

    private List<SheetCell> FollowColumnRenames(List<SheetCell> touched)
    {
        var renames = _columns.Follow(_workbook, touched);
        if (renames.Count == 0) return touched;
        _tableOwners.Rename(_workbook, renames);
        foreach (var reader in _tableReaders.Where(_formulas.ContainsKey).ToList())
        {
            if (SheetAt(reader.SheetIndex) is not { } sheet || sheet[reader.Address].Formula is not { } formula) continue;
            var renamer = new Operations.TableReferenceRenamer(renames);
            if (renamer.Rewrite(formula) is not { } rewritten || !renamer.Changed) continue;
            sheet[reader.Address] = sheet[reader.Address] with { Formula = rewritten };
            var ast = _parser.Parse(rewritten);
            _formulas[reader] = ast;
            RegisterDependencies(reader, ast);
            touched.Add(reader);
        }
        return touched;
    }

    private void Notify()
    {
        if (_writing == 0 && _batchDepth == 0) Recalculated?.Invoke(this, EventArgs.Empty);
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
            owner = _spills.Claim(cell);
            Synchronize(cell, sheet[address]);
        }
        finally
        {
            _writing--;
        }

        RecalculateFrom(owner is { } anchor ? [cell, anchor] : [cell]);
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

    public IReadOnlyDictionary<SheetCell, CellRange> SpillRanges => _spills.Ranges;

    public bool IsSpilledInto(SheetCell cell) => _spills.IsSpilledInto(cell);
    public IFunctionRegistry Functions => _functions;
    public IReadOnlySet<SheetCell> CircularCells => _circular;

    private const int MaxOnDemandDepth = 256;

    internal int SpillBottom(int sheetIndex) => _spills.Bottom(sheetIndex);

    internal int SpillRight(int sheetIndex) => _spills.Right(sheetIndex);

    private RecalculationPass? _pass;

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

            var multiValued = _spills.IsMultiValued(result, context);
            var produced = _spills.Materialize(cell, result, context, pass.Spilled);
            if (pass.SelfReads.Remove(cell))
            {
                _circular.Add(cell);
                produced = CellValue.FromError(CellError.Circular);
            }
            _values[cell] = produced;
            WriteBack(cell, produced);
            RecordArrayUse(cell, context.UsedArrays || multiValued);
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
        var last = new Dictionary<SheetCell, (EvalValue Result, WorkbookEvaluationContext Context)>();
        for (var step = 0; step < settings.MaxIterations; step++)
        {
            var largestChange = 0.0;
            foreach (var cell in cells)
            {
                var before = _values[cell];
                var context = new WorkbookEvaluationContext(this, cell);
                var result = _evaluator.EvaluateRaw(_formulas[cell], context);
                last[cell] = (result, context);
                var after = _coercion.ToScalar(result, context);
                _values[cell] = after;
                largestChange = Math.Max(largestChange, Change(before, after));
            }
            if (largestChange <= settings.MaxChange) break;
        }

        foreach (var cell in cells)
        {
            if (last.TryGetValue(cell, out var final))
            {
                var multiValued = _spills.IsMultiValued(final.Result, final.Context);
                if (multiValued) _values[cell] = _spills.Materialize(cell, final.Result, final.Context, _pass!.Spilled);
                else _spills.Release(cell, _pass!.Spilled);
                RecordArrayUse(cell, final.Context.UsedArrays || multiValued);
            }
            WriteBack(cell, _values[cell]);
        }
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
        Notify();
    }

    private void SetFormulaCore(SheetCell cell, string formula)
    {
        var sheet = SheetAt(cell.SheetIndex) ?? throw new ArgumentOutOfRangeException(nameof(cell));

        Track(cell, sheet[cell.Address], sheet[cell.Address].Value, formula);

        var owner = _spills.Claim(cell);
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
        Notify();
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
        Notify();
    }

    private void SetValueCore(SheetCell cell, CellValue value)
    {
        var sheet = SheetAt(cell.SheetIndex) ?? throw new ArgumentOutOfRangeException(nameof(cell));

        Track(cell, sheet[cell.Address], value, null);

        var owner = _spills.Claim(cell);
        sheet.SetValue(cell.Address, value);

        _spills.Release(cell);
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
        Notify();
    }

    private void RecalculateAllCore()
    {
        HasPendingCalculation = false;

        _values.Clear();
        _circular.Clear();
        _graph.Clear();
        _spills.Reset();

        _formulas.Clear();
        _aggregates.Clear();
        _volatile.Clear();
        _visibilitySensitive.Clear();
        _tableReaders.Clear();
        _columns.Reset(_workbook);
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
        _spills.ClearStale();
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
        Notify();
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

        var touched = FollowColumnRenames(changed.ToList());

        if (!AutomaticCalculation)
        {
            HasPendingCalculation = true;
            return;
        }

        _volatile.RemoveWhere(cell => !_formulas.ContainsKey(cell));
        var affected = _graph.TransitiveDependents([.. touched, .. _volatile]).ToHashSet();
        affected.UnionWith(_volatile);

        var unblocked = _spills.UnblockedBy(touched);
        if (unblocked.Count > 0)
        {
            affected.UnionWith(unblocked);
            affected.UnionWith(_graph.TransitiveDependents(unblocked));
        }

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

    private void WriteBack(SheetCell cell, CellValue value)
    {
        var sheet = SheetAt(cell.SheetIndex);
        if (sheet is null) return;
        var inferred = _formulas.TryGetValue(cell, out var ast)
            ? _formats.Infer(ast, new FormatLookup(this, cell, _evaluator, _coercion, _hints))
            : 0;
        sheet[cell.Address] = sheet[cell.Address] with { Value = value, InferredFormatId = inferred };
    }

    private void RegisterDependencies(SheetCell cell, FormulaNode ast)
    {
        var collected = _dependencies.Collect(ast, cell);
        Mark(_volatile, cell, collected.Volatile);
        Mark(_visibilitySensitive, cell, collected.VisibilitySensitive);
        Mark(_tableReaders, cell, collected.ReadsTables);
        _graph.SetPrecedents(cell, collected.Cells, collected.Ranges);
    }

    private void Mark(HashSet<SheetCell> cells, SheetCell cell, bool member)
    {
        if (member) cells.Add(cell);
        else cells.Remove(cell);
    }

    private readonly HashSet<SheetCell> _volatile = [];

    private readonly HashSet<SheetCell> _visibilitySensitive = [];

    private readonly HashSet<SheetCell> _tableReaders = [];

    internal IReadOnlyList<string> SheetsBetween(string firstSheet, string lastSheet)
    {
        var first = ResolveSheet(firstSheet, -1);
        var last = ResolveSheet(lastSheet, -1);
        if (first < 0 || last < 0) return [];
        return [.. Enumerable.Range(Math.Min(first, last), Math.Abs(last - first) + 1).Select(index => _workbook.Sheets[index].Name)];
    }

    internal int ResolveSheet(string? name, int current)
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

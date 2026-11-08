using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions;

public sealed class ArgumentReader(IReadOnlyList<EvalValue> arguments, IEvaluationContext context, ValueCoercion coercion)
{
    private CellError _error = CellError.None;
    private HashSet<int>? _scalarReads;
    private HashSet<int>? _arrayReads;
    private bool _readsEverything;

    public IReadOnlySet<int> LiftablePositions =>
        _scalarReads is null || _readsEverything ? new HashSet<int>()
        : _arrayReads is null ? _scalarReads
        : [.. _scalarReads.Except(_arrayReads)];

    private void ReadAsScalar(int index)
    {
        if (index >= arguments.Count) return;
        if ((!context.ImplicitIntersection && arguments[index] is RangeValue { Range.IsSingleCell: false })
            || arguments[index] is ArrayValue { RowCount: > 1 } or ArrayValue { ColumnCount: > 1 })
            (_scalarReads ??= []).Add(index);
    }

    private void ReadAsArray(int index) => (_arrayReads ??= []).Add(index);

    public int Count => arguments.Count;
    public IEvaluationContext Context => context;
    public bool HasError => _error != CellError.None;

    public CellValue Error => CellValue.FromError(_error);

    public bool Has(int index) => index < arguments.Count && arguments[index] is not MissingValue;

    public double Number(int index)
    {
        if (index >= arguments.Count) { Fail(CellError.Value); return 0; }
        ReadAsScalar(index);
        if (!coercion.TryScalarNumber(arguments[index], context, out var value, out var error))
        {
            Fail(error);
            return 0;
        }
        return value;
    }

    public double Number(int index, double fallback) => Has(index) ? Number(index) : fallback;

    public int Integer(int index) => (int)System.Math.Truncate(Number(index));
    public int Integer(int index, int fallback) => Has(index) ? Integer(index) : fallback;

    public bool Boolean(int index, bool fallback) => Has(index) ? Number(index) != 0 : fallback;

    public string Text(int index)
    {
        if (index >= arguments.Count) { Fail(CellError.Value); return string.Empty; }
        ReadAsScalar(index);
        var value = coercion.ToScalar(arguments[index], context);
        if (value.IsError) { Fail(value.Error); return string.Empty; }
        return context.NumberText.Of(value);
    }

    public string Text(int index, string fallback) => Has(index) ? Text(index) : fallback;

    public string TextOf(CellValue value) => context.NumberText.Of(value);

    public CellValue Value(int index)
    {
        if (index >= arguments.Count) return CellValue.Empty;
        ReadAsScalar(index);
        return coercion.ToScalar(arguments[index], context);
    }

    public EvalValue Raw(int index)
    {
        ReadAsArray(index);
        return index < arguments.Count ? arguments[index] : MissingValue.Instance;
    }

    public IEnumerable<CellValue> Values(int index) => coercion.Enumerate(Raw(index), context);

    public bool TryNumbersOf(int index, out List<double> numbers, out CellError error) =>
        coercion.TryCollectNumbers([Raw(index)], context, out numbers, out error);

    public IEnumerable<CellValue> AllValues()
    {
        _readsEverything = true;
        return arguments.SelectMany(a => coercion.Enumerate(a, context));
    }

    public bool TryAllNumbers(out List<double> numbers)
    {
        _readsEverything = true;
        if (coercion.TryCollectNumbers(arguments, context, out numbers, out var error)) return true;
        Fail(error);
        return false;
    }

    public void Fail(CellError error)
    {
        if (_error == CellError.None) _error = error;
    }
}

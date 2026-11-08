using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions;

public sealed class FunctionBuilder(string category)
{
    private readonly ValueCoercion _coercion = new();

    private readonly ArrayLifting _lifting = new();

    private readonly List<FunctionDefinition> _functions = [];

    public IReadOnlyList<FunctionDefinition> Build() => _functions;

    public FunctionBuilder Numeric1(string name, Func<double, CellValue> body) =>
    Lifted(name, 1, 1, _ => true, r =>
    {
        var x = r.Number(0);
        return r.HasError ? r.Error : body(x);
    });

    public FunctionBuilder Numeric2(string name, Func<double, double, CellValue> body) =>
    Lifted(name, 2, 2, _ => true, r =>
    {
        var x = r.Number(0);
        var y = r.Number(1);
        return r.HasError ? r.Error : body(x, y);
    });

    public FunctionBuilder Numeric2Optional(string name, double fallback, Func<double, double, CellValue> body) =>
    Lifted(name, 1, 2, _ => true, r =>
    {
        var x = r.Number(0);
        var y = r.Number(1, fallback);
        return r.HasError ? r.Error : body(x, y);
    });

    public FunctionBuilder Constant(string name, Func<CellValue> body) =>
    Raw(name, 0, 0, _ => body());

    public FunctionBuilder Aggregate(string name, Func<List<double>, CellValue> body, int minimumArguments = 1) =>
    Raw(name, minimumArguments, int.MaxValue, r =>
        r.TryAllNumbers(out var numbers) ? body(numbers) : r.Error);

    public FunctionBuilder Text1(string name, Func<string, CellValue> body) =>
    Lifted(name, 1, 1, _ => true, r =>
    {
        var text = r.Text(0);
        return r.HasError ? r.Error : body(text);
    });

    public FunctionBuilder Raw(
    string name, int minimumArguments, int maximumArguments,
    Func<ArgumentReader, CellValue> body, bool handlesErrors = false) =>
    Register(name, minimumArguments, maximumArguments, handlesErrors,
             reader => EvalValue.Scalar(body(reader)));

    public FunctionBuilder Lifted(
    string name, int minimumArguments, int maximumArguments, Func<int, bool> isScalarPosition, Func<ArgumentReader, CellValue> body) =>
    RegisterLifted(name, minimumArguments, maximumArguments, isScalarPosition, body);

    public FunctionBuilder Lifted<TState>(
    string name, int minimumArguments, int maximumArguments, Func<int, bool> isScalarPosition,
    Func<ArgumentReader, TState> prepare, Func<ArgumentReader, TState, CellValue> body)
    {
        _functions.Add(new FunctionDefinition
        {
            Name = name,
            MinArguments = minimumArguments,
            MaxArguments = maximumArguments,
            Category = category,
            Implementation = (arguments, context) =>
            {
                var state = prepare(new ArgumentReader(arguments, context, _coercion));
                return _lifting.Apply(arguments, context, isScalarPosition,
                    element => Bounded(EvalValue.Scalar(body(new ArgumentReader(element, context, _coercion), state))));
            },
        });
        return this;
    }

    public FunctionBuilder ArrayResult(
    string name, int minimumArguments, int maximumArguments, Func<ArgumentReader, EvalValue> body, bool handlesErrors = false) =>
    Register(name, minimumArguments, maximumArguments, handlesErrors, body);

    private FunctionBuilder Register(
        string name, int minimumArguments, int maximumArguments, bool handlesErrors,
        Func<ArgumentReader, EvalValue> body)
    {
        _functions.Add(new FunctionDefinition
        {
            Name = name,
            MinArguments = minimumArguments,
            MaxArguments = maximumArguments,
            Category = category,
            HandlesErrors = handlesErrors,
            Implementation = (arguments, context) =>
            {
                var reader = new ArgumentReader(arguments, context, _coercion);
                var result = Bounded(body(reader));
                var liftable = reader.LiftablePositions;
                return liftable.Count == 0
                    ? result
                    : _lifting.Apply(arguments, context, liftable.Contains, element => Bounded(body(new ArgumentReader(element, context, _coercion))));
            },
        });
        return this;
    }

    private FunctionBuilder RegisterLifted(
        string name, int minimumArguments, int maximumArguments, Func<int, bool> isScalarPosition,
        Func<ArgumentReader, CellValue> body)
    {
        _functions.Add(new FunctionDefinition
        {
            Name = name,
            MinArguments = minimumArguments,
            MaxArguments = maximumArguments,
            Category = category,
            Implementation = (arguments, context) => _lifting.Apply(arguments, context, isScalarPosition,
                element => Bounded(EvalValue.Scalar(body(new ArgumentReader(element, context, _coercion))))),
        });
        return this;
    }

    private EvalValue Bounded(EvalValue result) =>
        result is ScalarValue { Value: { Kind: ValueKind.Text } text } && !ResultLimits.TextFits(text.AsText().Length)
            ? EvalValue.Error(CellError.Value)
            : result;

    public FunctionBuilder RawHandlingErrors(
    string name, int minimumArguments, int maximumArguments, Func<ArgumentReader, CellValue> body) =>
    Raw(name, minimumArguments, maximumArguments, body, handlesErrors: true);

    public static CellValue Number(double value) =>
    double.IsNaN(value) || double.IsInfinity(value)
        ? CellValue.FromError(CellError.Number)
        : CellValue.Number(value);

    public static CellValue OutOfDomain() => CellValue.FromError(CellError.Number);

    public static CellValue Invalid() => CellValue.FromError(CellError.Value);

    public static CellValue DivideByZero() => CellValue.FromError(CellError.DivideByZero);
}

using PulseOffice.Sheets.Engine.Parsing;
using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.SpecialForms;

public sealed class LambdaForm : ISpecialForm
{
    private const string OptionalPrefix = "_xlop.";

    public string Name => "LAMBDA";
    public int MinArguments => 1;
    public int MaxArguments => int.MaxValue;

    public EvalValue Evaluate(
        IReadOnlyList<FormulaNode> arguments, EvaluationScope scope,
        IEvaluationContext context, IEvaluationServices services)
    {
        var parameters = new List<string>(arguments.Count - 1);
        var required = 0;

        for (var i = 0; i < arguments.Count - 1; i++)
        {
            if (arguments[i] is not NameNode parameter) return EvalValue.Error(CellError.Value);
            var optional = parameter.Name.StartsWith(OptionalPrefix, StringComparison.OrdinalIgnoreCase);
            if (!optional && required < i) return EvalValue.Error(CellError.Value);
            if (!optional) required++;
            parameters.Add(parameter.Name);
        }

        return new LambdaValue(parameters, arguments[^1], scope) { Required = required };
    }
}

public sealed class LetForm : ISpecialForm
{
    public string Name => "LET";
    public int MinArguments => 1;
    public int MaxArguments => int.MaxValue;

    public EvalValue Evaluate(
        IReadOnlyList<FormulaNode> arguments, EvaluationScope scope,
        IEvaluationContext context, IEvaluationServices services)
    {
        if (arguments.Count % 2 == 0) return EvalValue.Error(CellError.Value);

        var local = scope.Extend(arguments.Count / 2);

        for (var i = 0; i + 1 < arguments.Count - 1; i += 2)
        {
            if (arguments[i] is not NameNode name) return EvalValue.Error(CellError.Value);

            var value = services.Evaluate(arguments[i + 1], local, context);
            if (value is ScalarValue { Value.IsError: true } error) return error;

            local.Bind(name.Name, value);
        }

        return services.Evaluate(arguments[^1], local, context);
    }
}

public sealed class IsOmittedForm : ISpecialForm
{
    public string Name => "ISOMITTED";
    public int MinArguments => 1;
    public int MaxArguments => 1;
    public string Category => "Informazione";

    public EvalValue Evaluate(
        IReadOnlyList<FormulaNode> arguments, EvaluationScope scope,
        IEvaluationContext context, IEvaluationServices services)
    {
        if (arguments[0] is MissingArgumentNode)
            return EvalValue.Scalar(CellValue.Boolean(true));

        if (arguments[0] is NameNode name)
            return EvalValue.Scalar(CellValue.Boolean(
                !scope.TryResolve(name.Name, out var bound) || bound is MissingValue));

        return EvalValue.Scalar(CellValue.Boolean(false));
    }
}

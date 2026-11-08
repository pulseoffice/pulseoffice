using PulseOffice.Sheets.Engine.Parsing;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions;

public interface IEvaluationServices
{
    EvalValue Evaluate(FormulaNode node, EvaluationScope scope, IEvaluationContext context);

    EvalValue Invoke(LambdaValue lambda, IReadOnlyList<EvalValue> arguments, IEvaluationContext context);

    PulseOffice.Sheets.Values.CellValue LastResult { get; }
}

public interface ISpecialForm
{
    string Name { get; }

    int MinArguments { get; }
    int MaxArguments { get; }
    string Category => "Logica";

    EvalValue Evaluate(
        IReadOnlyList<FormulaNode> arguments,
        EvaluationScope scope,
        IEvaluationContext context,
        IEvaluationServices services);
}

public interface ISpecialFormRegistry
{
    ISpecialForm? Find(string name);
    IReadOnlyCollection<ISpecialForm> All { get; }
}

public sealed class SpecialFormRegistry : ISpecialFormRegistry
{
    private readonly Dictionary<string, ISpecialForm> _forms;

    public SpecialFormRegistry(IEnumerable<ISpecialForm> forms)
    {
        ArgumentNullException.ThrowIfNull(forms);

        _forms = new Dictionary<string, ISpecialForm>(StringComparer.OrdinalIgnoreCase);
        foreach (var form in forms)
            if (!_forms.TryAdd(form.Name, form))
                throw new InvalidOperationException($"Duplicate special form: '{form.Name}'.");
    }

    public ISpecialForm? Find(string name) => _forms.GetValueOrDefault(name);
    public IReadOnlyCollection<ISpecialForm> All => _forms.Values;
}

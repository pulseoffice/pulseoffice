using PulseOffice.Sheets.Engine.Parsing;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.SpecialForms;

public sealed class CurrentForm : ISpecialForm
{
    public string Name => "CURRENT";
    public int MinArguments => 0;
    public int MaxArguments => 0;
    public string Category => "Informazione";

    public EvalValue Evaluate(
        IReadOnlyList<FormulaNode> arguments, EvaluationScope scope,
        IEvaluationContext context, IEvaluationServices services) =>
        EvalValue.Scalar(services.LastResult);
}

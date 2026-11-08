namespace PulseOffice.Sheets.Engine.Evaluation.Functions;

public delegate EvalValue FunctionImplementation(
    IReadOnlyList<EvalValue> arguments, IEvaluationContext context);

public sealed record FunctionDefinition
{
    public required string Name { get; init; }
    public required int MinArguments { get; init; }

    public required int MaxArguments { get; init; }

    public required FunctionImplementation Implementation { get; init; }

    public string Category { get; init; } = "Generale";
    public string? Description { get; init; }

    public bool HandlesErrors { get; init; }

    public bool IsOpenXmlOnly { get; init; }

    public bool AcceptsArgumentCount(int count) => count >= MinArguments && count <= MaxArguments;
}

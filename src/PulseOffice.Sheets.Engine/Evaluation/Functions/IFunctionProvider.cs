namespace PulseOffice.Sheets.Engine.Evaluation.Functions;

public interface IFunctionProvider
{
    string Category { get; }

    IEnumerable<FunctionDefinition> GetFunctions();
}

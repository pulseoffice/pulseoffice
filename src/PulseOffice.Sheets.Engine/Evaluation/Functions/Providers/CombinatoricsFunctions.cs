using PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;
using M = System.Math;
using F = PulseOffice.Sheets.Engine.Evaluation.Functions.FunctionBuilder;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class CombinatoricsFunctions : IFunctionProvider
{
    public string Category => "Matematica";

    private const int FactorialLimit = 171;

    public IEnumerable<FunctionDefinition> GetFunctions() => new FunctionBuilder(Category)
        .Numeric1("FACT", x => x < 0 || x >= FactorialLimit
            ? F.OutOfDomain()
            : F.Number(SpreadsheetMath.Factorial((int)M.Floor(x))))

        .Numeric1("FACTDOUBLE", x => x < 0
            ? F.OutOfDomain()
            : F.Number(SpreadsheetMath.DoubleFactorial((int)M.Floor(x))))

        .Numeric2("COMBIN", (n, k) => n < 0 || k < 0 || k > n
            ? F.OutOfDomain()
            : F.Number(SpreadsheetMath.Combinations(M.Floor(n), M.Floor(k))))

        .Numeric2("COMBINA", (n, k) => n < 0 || k < 0
            ? F.OutOfDomain()
            : F.Number(SpreadsheetMath.Combinations(M.Floor(n) + M.Floor(k) - 1, M.Floor(k))))

        .Numeric2("PERMUT", (n, k) => n < 0 || k < 0 || k > n
            ? F.OutOfDomain()
            : F.Number(SpreadsheetMath.Combinations(M.Floor(n), M.Floor(k))
                       * SpreadsheetMath.Factorial((int)M.Floor(k))))

        .Numeric2("PERMUTATIONA", (n, k) => n < 0 || k < 0
            ? F.OutOfDomain()
            : F.Number(M.Pow(M.Floor(n), M.Floor(k))))

        .Build();
}

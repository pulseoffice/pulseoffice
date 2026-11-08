using PulseOffice.Sheets.Values;
using M = System.Math;
using F = PulseOffice.Sheets.Engine.Evaluation.Functions.FunctionBuilder;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class TrigonometryFunctions : IFunctionProvider
{
    public string Category => "Matematica";

    public IEnumerable<FunctionDefinition> GetFunctions() => new FunctionBuilder(Category)
        .Constant("PI", () => CellValue.Number(M.PI))
        .Numeric1("DEGREES", x => F.Number(x * 180 / M.PI))
        .Numeric1("RADIANS", x => F.Number(x * M.PI / 180))

        .Numeric1("SIN", x => F.Number(M.Sin(x)))
        .Numeric1("COS", x => F.Number(M.Cos(x)))
        .Numeric1("TAN", x => F.Number(M.Tan(x)))
        .Numeric1("COT", x => M.Tan(x) == 0 ? F.DivideByZero() : F.Number(1 / M.Tan(x)))
        .Numeric1("SEC", x => M.Cos(x) == 0 ? F.OutOfDomain() : F.Number(1 / M.Cos(x)))
        .Numeric1("CSC", x => M.Sin(x) == 0 ? F.DivideByZero() : F.Number(1 / M.Sin(x)))

        .Numeric1("ASIN", x => x is < -1 or > 1 ? F.OutOfDomain() : F.Number(M.Asin(x)))
        .Numeric1("ACOS", x => x is < -1 or > 1 ? F.OutOfDomain() : F.Number(M.Acos(x)))
        .Numeric1("ATAN", x => F.Number(M.Atan(x)))
        .Numeric1("ACOT", x => F.Number(M.PI / 2 - M.Atan(x)))

        .Numeric2("ATAN2", (x, y) => F.Number(M.Atan2(y, x)))

        .Numeric1("SINH", x => F.Number(M.Sinh(x)))
        .Numeric1("COSH", x => F.Number(M.Cosh(x)))
        .Numeric1("TANH", x => F.Number(M.Tanh(x)))
        .Numeric1("COTH", x => x == 0 ? F.DivideByZero() : F.Number(1 / M.Tanh(x)))
        .Numeric1("SECH", x => F.Number(1 / M.Cosh(x)))
        .Numeric1("CSCH", x => x == 0 ? F.DivideByZero() : F.Number(1 / M.Sinh(x)))

        .Numeric1("ASINH", x => F.Number(M.Asinh(x)))
        .Numeric1("ACOSH", x => x < 1 ? F.OutOfDomain() : F.Number(M.Acosh(x)))
        .Numeric1("ATANH", x => x is <= -1 or >= 1 ? F.OutOfDomain() : F.Number(M.Atanh(x)))
        .Numeric1("ACOTH", x => M.Abs(x) <= 1 ? F.OutOfDomain() : F.Number(0.5 * M.Log((x + 1) / (x - 1))))

        .Build();
}

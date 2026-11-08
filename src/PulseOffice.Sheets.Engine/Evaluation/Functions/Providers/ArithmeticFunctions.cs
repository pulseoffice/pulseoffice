using PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;
using PulseOffice.Sheets.Values;
using M = System.Math;
using F = PulseOffice.Sheets.Engine.Evaluation.Functions.FunctionBuilder;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class ArithmeticFunctions : IFunctionProvider
{
    public string Category => "Matematica";

    public IEnumerable<FunctionDefinition> GetFunctions() => new FunctionBuilder(Category)
        .Aggregate("SUM", n => F.Number(n.Sum()))
        .Aggregate("SUMSQ", n => F.Number(n.Sum(x => x * x)))
        .Aggregate("PRODUCT", n => F.Number(n.Count == 0 ? 0 : n.Aggregate(1.0, (x, y) => x * y)))

        .Numeric1("ABS", x => F.Number(M.Abs(x)))
        .Numeric1("SIGN", x => CellValue.Number(M.Sign(x)))
        .Numeric1("INT", x => F.Number(SpreadsheetMath.ApproxFloor(x)))
        .Numeric1("EXP", x => F.Number(M.Exp(x)))
        .Numeric1("SQRT", x => x < 0 ? F.OutOfDomain() : F.Number(M.Sqrt(x)))
        .Numeric1("LN", x => x <= 0 ? F.OutOfDomain() : F.Number(M.Log(x)))
        .Numeric1("LOG10", x => x <= 0 ? F.OutOfDomain() : F.Number(M.Log10(x)))
        .Numeric1("GAMMA", x => F.Number(SpreadsheetMath.Gamma(x)))
        .Numeric1("GAMMALN", x => x <= 0 ? F.OutOfDomain() : F.Number(SpreadsheetMath.LogGamma(x)))
        .Numeric1("GAMMALN.PRECISE", x => x <= 0 ? F.OutOfDomain() : F.Number(SpreadsheetMath.LogGamma(x)))
        .Raw("ERF", 1, 2, r =>
        {
            var lower = r.Number(0);
            var upper = r.Number(1, double.NaN);
            if (r.HasError) return r.Error;
            return F.Number(double.IsNaN(upper)
                ? SpreadsheetMath.Erf(lower)
                : SpreadsheetMath.Erf(upper) - SpreadsheetMath.Erf(lower));
        })
        .Numeric1("ERF.PRECISE", x => F.Number(SpreadsheetMath.Erf(x)))
        .Numeric1("ERFC", x => F.Number(SpreadsheetMath.Erfc(x)))
        .Numeric1("ERFC.PRECISE", x => F.Number(SpreadsheetMath.Erfc(x)))

        .Numeric2("POWER", (b, e) => F.Number(SpreadsheetMath.Power(b, e)))
        .Numeric2("QUOTIENT", (x, y) => y == 0 ? F.DivideByZero() : F.Number(M.Truncate(x / y)))

        .Numeric2("MOD", (x, d) => d == 0 ? F.DivideByZero() : F.Number(x - d * M.Floor(x / d)))

        .Numeric2Optional("LOG", 10, (x, b) =>
            x <= 0 || b <= 0 || b == 1 ? F.OutOfDomain() : F.Number(M.Log(x, b)))

        .Raw("GCD", 1, int.MaxValue, r => Fold(r, SpreadsheetMath.GreatestCommonDivisor, 0))
        .Raw("LCM", 1, int.MaxValue, r => Fold(r, SpreadsheetMath.LeastCommonMultiple, 1))

        .Build();

    private const double ExactIntegerLimit = 9007199254740992d;

    private static CellValue Fold(ArgumentReader r, Func<long, long, long> combine, long seed)
    {
        if (!r.TryAllNumbers(out var numbers)) return r.Error;
        if (numbers.Any(n => n < 0 || n >= ExactIntegerLimit)) return F.OutOfDomain();

        var result = seed;
        try
        {
            foreach (var n in numbers) result = combine(result, (long)M.Truncate(n));
        }
        catch (OverflowException)
        {
            return F.OutOfDomain();
        }
        if (result >= ExactIntegerLimit) return F.OutOfDomain();
        return CellValue.Number(result);
    }
}

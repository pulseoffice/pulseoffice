using PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;
using PulseOffice.Sheets.Values;
using M = System.Math;
using F = PulseOffice.Sheets.Engine.Evaluation.Functions.FunctionBuilder;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class RoundingFunctions : IFunctionProvider
{
    public string Category => "Matematica";

    public IEnumerable<FunctionDefinition> GetFunctions() => new FunctionBuilder(Category)

        .Numeric2Optional("ROUND", 0, (x, d) => F.Number(RoundHalfAwayFromZero(x, Digits(d))))
        .Numeric2Optional("ROUNDUP", 0, (x, d) => F.Number(Truncate(x, Digits(d), towardsInfinity: true)))
        .Numeric2Optional("ROUNDDOWN", 0, (x, d) => F.Number(Truncate(x, Digits(d), towardsInfinity: false)))
        .Numeric2Optional("TRUNC", 0, (x, d) => F.Number(Truncate(x, Digits(d), towardsInfinity: false)))

        .Numeric2("MROUND", (x, multiple) =>
            multiple == 0 ? CellValue.Number(0)
            : x != 0 && M.Sign(x) != M.Sign(multiple) ? F.OutOfDomain()
            : F.Number(M.Round(SpreadsheetMath.ApproxValue(x / multiple), MidpointRounding.AwayFromZero) * multiple))

        .Numeric1("EVEN", x => F.Number(x == 0 ? 0 : M.Sign(x) * M.Ceiling(M.Abs(x) / 2) * 2))
        .Numeric1("ODD", x => F.Number(x == 0 ? 1 : M.Sign(x) * (M.Ceiling((M.Abs(x) - 1) / 2) * 2 + 1)))

        .Raw("CEILING", 1, 3, r => OpenFormulaStep(r, towardsInfinity: true))
        .Raw("FLOOR", 1, 3, r => OpenFormulaStep(r, towardsInfinity: false))
        .Raw("CEILING.MATH", 1, 3, r => MathStep(r, towardsInfinity: true))
        .Raw("FLOOR.MATH", 1, 3, r => MathStep(r, towardsInfinity: false))

        .Numeric2Optional("CEILING.PRECISE", 1, (x, s) => s == 0 ? CellValue.Number(0) : F.Number(PreciseStep(x, s, towardsInfinity: true)))
        .Numeric2Optional("FLOOR.PRECISE", 1, (x, s) => s == 0 ? CellValue.Number(0) : F.Number(PreciseStep(x, s, towardsInfinity: false)))
        .Numeric2Optional("ISO.CEILING", 1, (x, s) => s == 0 ? CellValue.Number(0) : F.Number(PreciseStep(x, s, towardsInfinity: true)))

        .Numeric2("CEILING.XCL", (x, s) => s == 0 || x == 0 ? CellValue.Number(0)
            : x > 0 && s < 0 ? F.OutOfDomain() : F.Number(SpreadsheetMath.ApproxCeiling(x / s) * s))
        .Numeric2("FLOOR.XCL", (x, s) => s == 0 ? (x == 0 ? CellValue.Number(0) : F.DivideByZero())
            : x > 0 && s < 0 ? F.OutOfDomain() : F.Number(SpreadsheetMath.ApproxFloor(x / s) * s))

        .Build();

    private const int MaximumDigits = 400;

    private static int Digits(double digits) =>
        double.IsNaN(digits) ? 0 : (int)M.Clamp(M.Truncate(digits), -MaximumDigits, MaximumDigits);

    private static double RoundHalfAwayFromZero(double value, int digits)
    {
        if (!double.IsFinite(value) || value == 0) return value;
        var negative = value < 0;
        var magnitude = M.Abs(value);
        if (digits >= 0 && (magnitude >= 4503599627370496d || M.Floor(magnitude) == magnitude)) return value;

        var factor = 1.0;
        if (digits != 0)
        {
            if (digits > 0)
                digits = M.Min(digits, 52 - M.ILogB(magnitude));
            factor = M.Pow(10, M.Abs(digits));
            if (factor == 0 || (digits < 0 && double.IsInfinity(factor))) return 0;
            if (double.IsInfinity(factor)) return value;
            magnitude = digits < 0 ? magnitude / factor : magnitude * factor;
            if (!double.IsFinite(magnitude)) return value;
        }

        if (magnitude < 4503599627370496d) magnitude = SpreadsheetMath.ApproxFloor(magnitude + 0.5);
        if (digits != 0) magnitude = digits < 0 ? magnitude * factor : magnitude / factor;
        return negative ? -magnitude : magnitude;
    }

    private static double Truncate(double value, int digits, bool towardsInfinity)
    {
        if (!double.IsFinite(value) || value == 0) return value;
        if (digits > 0 && digits >= 52 - M.ILogB(M.Abs(value))) return value;
        if (digits < 0 && -digits > M.ILogB(M.Abs(value)) / 3 + 1)
            return towardsInfinity ? M.Sign(value) * M.Pow(10, -digits) : 0;
        var factor = M.Pow(10, digits);
        if (!double.IsFinite(value * factor)) return value;
        var scaled = SpreadsheetMath.ApproxValue(value * factor);
        var rounded = towardsInfinity
            ? (value >= 0 ? M.Ceiling(scaled) : M.Floor(scaled))
            : M.Truncate(scaled);
        return rounded / factor;
    }

    private static double PreciseStep(double value, double step, bool towardsInfinity)
    {
        var s = step == 0 ? 1 : M.Abs(step);
        return (towardsInfinity ? SpreadsheetMath.ApproxCeiling(value / s) : SpreadsheetMath.ApproxFloor(value / s)) * s;
    }

    private static CellValue OpenFormulaStep(ArgumentReader r, bool towardsInfinity)
    {
        var value = r.Number(0);
        var step = r.Has(1) ? r.Number(1) : value < 0 ? -1 : 1;
        var awayFromZero = r.Number(2, 0) != 0;
        if (r.HasError) return r.Error;

        if (step == 0 || value == 0) return CellValue.Number(0);
        if (M.Sign(value) != M.Sign(step)) return F.OutOfDomain();

        var quotient = value / step;
        var up = towardsInfinity ^ (!awayFromZero && value < 0);
        return F.Number((up ? SpreadsheetMath.ApproxCeiling(quotient) : SpreadsheetMath.ApproxFloor(quotient)) * step);
    }

    private static CellValue MathStep(ArgumentReader r, bool towardsInfinity)
    {
        var value = r.Number(0);
        var step = r.Number(1, 1);
        var awayFromZero = r.Number(2, 0) != 0;
        if (r.HasError) return r.Error;

        if (step == 0) return CellValue.Number(0);

        var s = M.Abs(step);
        return F.Number(Apply(value / s, towardsInfinity, awayFromZero && value < 0) * s);
    }

    private static double Apply(double quotient, bool towardsInfinity, bool invert)
    {
        var up = towardsInfinity ^ invert;
        return up ? SpreadsheetMath.ApproxCeiling(quotient) : SpreadsheetMath.ApproxFloor(quotient);
    }
}

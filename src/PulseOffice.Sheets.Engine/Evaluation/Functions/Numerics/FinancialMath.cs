using M = System.Math;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;

public static class FinancialMath
{
    public static double GrowthMinusOne(double rate, double periods) =>
        rate > -1 ? ExpMinusOne(periods * LogOnePlus(rate)) : M.Pow(1 + rate, periods) - 1;

    private static double LogOnePlus(double x)
    {
        var u = 1 + x;
        return u == 1 ? x : M.Log(u) * x / (u - 1);
    }

    private static double ExpMinusOne(double x)
    {
        var u = M.Exp(x);
        if (u == 1) return x;
        if (double.IsInfinity(u)) return u;
        var um1 = u - 1;
        return um1 == -1 ? -1 : um1 * x / M.Log(u);
    }

    public static double AnnuityFactor(double rate, double periods, bool dueAtStart)
    {
        if (rate == 0) return periods;
        var factor = -GrowthMinusOne(rate, -periods) / rate;
        return dueAtStart ? factor * (1 + rate) : factor;
    }

    public static double PresentValue(double rate, double periods, double payment, double future, bool dueAtStart)
    {
        if (rate == 0) return -(future + payment * periods);
        return -(future / M.Pow(1 + rate, periods) + payment * AnnuityFactor(rate, periods, dueAtStart));
    }

    public static double FutureValue(double rate, double periods, double payment, double present, bool dueAtStart)
    {
        if (rate == 0) return -(present + payment * periods);
        var growthMinusOne = GrowthMinusOne(rate, periods);
        return -(present * (growthMinusOne + 1) + payment * (dueAtStart ? (1 + rate) : 1) * growthMinusOne / rate);
    }

    public static double Payment(double rate, double periods, double present, double future, bool dueAtStart)
    {
        if (periods == 0) return double.NaN;
        if (rate == 0) return -(present + future) / periods;

        var growthMinusOne = GrowthMinusOne(rate, periods);
        if (growthMinusOne == 0) return -(present + future) / periods;
        var payment = -rate * (present * (growthMinusOne + 1) + future) / growthMinusOne;
        return dueAtStart ? payment / (1 + rate) : payment;
    }

    public static double? Periods(double rate, double payment, double present, double future, bool dueAtStart)
    {
        if (rate == 0)
            return payment == 0 ? null : -(present + future) / payment;

        var adjusted = dueAtStart ? payment * (1 + rate) : payment;
        var numerator = adjusted / rate - future;
        var denominator = present + adjusted / rate;

        if (denominator == 0 || numerator / denominator <= 0) return null;
        return M.Log(numerator / denominator) / M.Log(1 + rate);
    }

    public static double? Rate(
    double periods, double payment, double present, double future, bool dueAtStart, double guess = 0.1)
    {
        double Residual(double rate) => FutureValue(rate, periods, payment, present, dueAtStart) - future;

        return SolveRate(Residual, guess);
    }

    public static double? SolveRate(Func<double, double> residual, double guess)
    {
        if (Newton(residual, guess) is { } nearGuess) return nearGuess;

        var low = -0.999999;
        var high = M.Max(guess * 10, 1.0);
        var valueLow = residual(low);
        var valueHigh = residual(high);

        var expansions = 0;
        while (!(valueLow * valueHigh <= 0) && expansions++ < 64)
        {
            high *= 2;
            valueHigh = residual(high);
        }
        if (!(valueLow * valueHigh <= 0)) return null;

        for (var i = 0; i < 200; i++)
        {
            var middle = (low + high) / 2;
            var value = residual(middle);
            if (M.Abs(value) < 1e-10 || high - low < 1e-12) return Polish(residual, middle);

            if (valueLow * value <= 0) high = middle;
            else { low = middle; valueLow = value; }
        }
        return Polish(residual, (low + high) / 2);
    }

    private static double? Newton(Func<double, double> residual, double guess)
    {
        var rate = guess;
        for (var i = 0; i < 50; i++)
        {
            if (!(rate > -1) || !double.IsFinite(rate)) return null;
            var value = residual(rate);
            if (!double.IsFinite(value)) return null;
            var step = M.Max(M.Abs(rate), 1) * 1e-7;
            var slope = (residual(rate + step) - residual(rate - step)) / (2 * step);
            if (slope == 0 || !double.IsFinite(slope)) return null;
            var next = rate - value / slope;
            if (M.Abs(next - rate) <= 1e-12 * M.Max(M.Abs(next), 1))
                return next > -1 && double.IsFinite(next) ? next : null;
            rate = next;
        }
        return null;
    }

    public static double Polish(Func<double, double> residual, double root)
    {
        for (var i = 0; i < 20; i++)
        {
            var step = M.Max(M.Abs(root), 1) * 1e-7;
            var value = residual(root);
            var slope = (residual(root + step) - residual(root - step)) / (2 * step);
            if (value == 0 || slope == 0 || !double.IsFinite(slope)) return root;
            var next = root - value / slope;
            if (!double.IsFinite(next)) return root;
            if (M.Abs(next - root) <= 1e-15 * M.Max(M.Abs(next), 1e-300)) return next;
            if (M.Abs(residual(next)) > M.Abs(value)) return root;
            root = next;
        }
        return root;
    }

    public static double NetPresentValue(double rate, IReadOnlyList<double> cashFlows)
    {
        var total = 0.0;
        for (var i = 0; i < cashFlows.Count; i++)
            total += cashFlows[i] / M.Pow(1 + rate, i + 1);
        return total;
    }

    public static double? InternalRateOfReturn(IReadOnlyList<double> cashFlows, double guess = 0.1)
    {
        if (cashFlows.Count < 2 || cashFlows.All(c => c >= 0) || cashFlows.All(c => c <= 0)) return null;

        var later = cashFlows.Skip(1).ToList();
        double Npv(double rate) => cashFlows[0] + NetPresentValue(rate, later);

        return SolveRate(Npv, guess);
    }

    public static double StraightLineDepreciation(double cost, double salvage, double life) =>
    life == 0 ? double.NaN : (cost - salvage) / life;

    public static double SumOfYearsDigits(double cost, double salvage, double life, double period) =>
    (cost - salvage) * (life - period + 1) * 2 / (life * (life + 1));

    public static double DoubleDecliningPeriod(double cost, double salvage, double life, double period, double factor)
    {
        var rate = factor / life;
        double previous;
        if (rate >= 1)
        {
            rate = 1;
            previous = period == 1 ? cost : 0;
        }
        else previous = cost * M.Pow(1 - rate, period - 1);
        var next = cost * M.Pow(1 - rate, period);
        return M.Max(next < salvage ? previous - salvage : previous - next, 0);
    }

    public static double VariableDecliningBalanceWithoutSwitch(double cost, double salvage, double life, double start, double end, double factor)
    {
        var wholeStart = SpreadsheetMath.ApproxFloor(start);
        var wholeEnd = SpreadsheetMath.ApproxCeiling(end);
        var total = 0.0;
        for (var i = wholeStart + 1; i <= wholeEnd; i++)
        {
            var term = DoubleDecliningPeriod(cost, salvage, life, i, factor);
            if (i == wholeStart + 1) term *= M.Min(end, wholeStart + 1) - start;
            else if (i == wholeEnd) term *= end + 1 - wholeEnd;
            total += term;
        }
        return total;
    }

    public static double VariableDecliningBalance(double cost, double salvage, double life, double start, double end, double factor)
    {
        var wholeStart = SpreadsheetMath.ApproxFloor(start);
        var wholeEnd = SpreadsheetMath.ApproxCeiling(end);
        var part = 0.0;
        if (SpreadsheetMath.ApproxValue(start) != wholeStart)
        {
            var remaining = cost - SwitchingDepreciation(cost, salvage, life, life, wholeStart, factor);
            part += (start - wholeStart) * SwitchingDepreciation(remaining, salvage, life, life - wholeStart, 1, factor);
        }
        if (SpreadsheetMath.ApproxValue(end) != wholeEnd)
        {
            var remaining = cost - SwitchingDepreciation(cost, salvage, life, life, wholeEnd - 1, factor);
            part += (wholeEnd - end) * SwitchingDepreciation(remaining, salvage, life, life - wholeEnd + 1, 1, factor);
        }
        var depreciated = cost - SwitchingDepreciation(cost, salvage, life, life, wholeStart, factor);
        return SwitchingDepreciation(depreciated, salvage, life, life - wholeStart, wholeEnd - wholeStart, factor) - part;
    }

    private static double SwitchingDepreciation(double cost, double salvage, double life, double remainingLife, double period, double factor)
    {
        var periods = (long)SpreadsheetMath.ApproxCeiling(period);
        var depreciable = cost - salvage;
        var switched = false;
        var straight = 0.0;
        var total = 0.0;
        for (long i = 1; i <= periods; i++)
        {
            double term;
            if (!switched)
            {
                var declining = DoubleDecliningPeriod(cost, salvage, life, i, factor);
                straight = depreciable / (remainingLife - (i - 1));
                if (straight > declining)
                {
                    term = straight;
                    switched = true;
                }
                else
                {
                    term = declining;
                    depreciable -= declining;
                }
            }
            else term = straight;

            if (i == periods) term *= period + 1 - SpreadsheetMath.ApproxCeiling(period);
            total += term;
        }
        return total;
    }
}

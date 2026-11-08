using M = System.Math;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;

public static class OptionPricing
{
    public static double ProbabilityOfHitting(
    double spot, double volatility, double drift, double time, double barrier)
    {
        if (time <= 0 || volatility <= 0 || spot <= 0 || barrier <= 0) return double.NaN;

        var logRatio = M.Log(barrier / spot);
        var variance = volatility * volatility;
        var mu = drift - variance / 2;
        var sigmaRootT = volatility * M.Sqrt(time);

        var first = SpreadsheetMath.StandardNormalCdf((-logRatio + mu * time) / sigmaRootT);
        var exponent = 2 * mu * logRatio / variance;
        var second = M.Exp(exponent)
                     * SpreadsheetMath.StandardNormalCdf((-logRatio - mu * time) / sigmaRootT);

        var probability = barrier > spot ? 1 - first + second : first + second;
        return M.Clamp(probability, 0, 1);
    }

    public static double ProbabilityInMoney(
    double spot, double volatility, double drift, double time, double lower, double upper)
    {
        if (time <= 0 || volatility <= 0 || spot <= 0) return double.NaN;

        var mu = drift - volatility * volatility / 2;
        var sigmaRootT = volatility * M.Sqrt(time);

        double Cumulative(double level) =>
            level <= 0 ? 0 : SpreadsheetMath.StandardNormalCdf((M.Log(level / spot) - mu * time) / sigmaRootT);

        if (upper > 0 && lower > 0) return M.Max(Cumulative(upper) - Cumulative(lower), 0);
        if (upper > 0) return Cumulative(upper);
        return 1 - Cumulative(lower);
    }

    public static double TouchValue(
    double spot, double volatility, double rate, double foreignRate,
    double time, double barrier, bool payAtHit)
    {
        var probability = ProbabilityOfHitting(spot, volatility, rate - foreignRate, time, barrier);
        if (double.IsNaN(probability)) return double.NaN;

        return payAtHit ? probability : probability * M.Exp(-rate * time);
    }

    public static double BarrierValue(
    double spot, double volatility, double rate, double foreignRate,
    double time, double strikeLow, double strikeHigh, double barrierLow, double barrierHigh, bool call)
    {
        var plain = BlackScholes(spot, call ? strikeLow : strikeHigh, rate, foreignRate, volatility, time, call);
        if (double.IsNaN(plain)) return double.NaN;

        var barrier = call ? barrierHigh : barrierLow;
        if (barrier <= 0) return plain;

        var survival = 1 - ProbabilityOfHitting(spot, volatility, rate - foreignRate, time, barrier);
        return plain * M.Clamp(survival, 0, 1);
    }

    public static double BlackScholes(
    double spot, double strike, double rate, double foreignRate,
    double volatility, double time, bool call)
    {
        if (spot <= 0 || strike <= 0 || time <= 0 || volatility <= 0) return double.NaN;

        var sigmaRootT = volatility * M.Sqrt(time);
        var d1 = (M.Log(spot / strike) + (rate - foreignRate + volatility * volatility / 2) * time) / sigmaRootT;
        var d2 = d1 - sigmaRootT;

        var discountedSpot = spot * M.Exp(-foreignRate * time);
        var discountedStrike = strike * M.Exp(-rate * time);

        return call
            ? discountedSpot * SpreadsheetMath.StandardNormalCdf(d1)
              - discountedStrike * SpreadsheetMath.StandardNormalCdf(d2)
            : discountedStrike * SpreadsheetMath.StandardNormalCdf(-d2)
              - discountedSpot * SpreadsheetMath.StandardNormalCdf(-d1);
    }
}

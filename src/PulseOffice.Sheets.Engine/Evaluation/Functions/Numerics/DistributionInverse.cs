using M = System.Math;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;

public static class DistributionInverse
{
    private const int MaxIterations = 2000;

    public static double? Invert(Func<double, double> cdf, double probability, double low, double high)
    {
        if (probability is <= 0 or >= 1) return null;

        var expansions = 0;
        while (cdf(high) < probability && expansions++ < 1100) high = high > 0 ? high * 2 : high + 1;
        if (cdf(high) < probability) return null;
        expansions = 0;
        while (low < 0 && cdf(low) > probability && expansions++ < 1100) low *= 2;
        if (low < 0 && cdf(low) > probability) return null;

        for (var i = 0; i < MaxIterations; i++)
        {
            var middle = low / 2 + high / 2;
            if (middle <= low || middle >= high) break;

            var value = cdf(middle);
            if (value == probability) return middle;

            if (value < probability) low = middle;
            else high = middle;
        }
        return M.Abs(cdf(low) - probability) <= M.Abs(cdf(high) - probability) ? low : high;
    }

    public static double? ChiSquare(double probability, double degrees) =>
        degrees < 1 ? null
            : Invert(x => SpreadsheetMath.RegularizedIncompleteGamma(degrees / 2, x / 2), probability, 0, 1000);

    public static double? Beta(double probability, double alpha, double beta) =>
        alpha <= 0 || beta <= 0 ? null
            : Invert(x => SpreadsheetMath.RegularizedIncompleteBeta(alpha, beta, x), probability, 0, 1);

    public static double? Gamma(double probability, double shape, double scale) =>
        shape <= 0 || scale <= 0 ? null
            : Invert(x => SpreadsheetMath.RegularizedIncompleteGamma(shape, x / scale), probability, 0, 1000);

    public static double? StudentTwoTailed(double probability, double degrees)
    {
        if (degrees < 1 || probability is <= 0 or > 1) return null;

        double RightTail(double t) =>
            1 - 0.5 * SpreadsheetMath.RegularizedIncompleteBeta(degrees / 2, 0.5, degrees / (degrees + t * t));

        return Invert(RightTail, 1 - probability / 2, 0, 100);
    }

    public static double? StudentOneTailed(double probability, double degrees)
    {
        if (degrees < 1) return null;

        double Cdf(double t)
        {
            var tail = 0.5 * SpreadsheetMath.RegularizedIncompleteBeta(degrees / 2, 0.5, degrees / (degrees + t * t));
            return t >= 0 ? 1 - tail : tail;
        }

        return Invert(Cdf, probability, -100, 100);
    }

    public static double? Fisher(double probability, double degrees1, double degrees2) =>
        degrees1 < 1 || degrees2 < 1 ? null
            : Invert(x => SpreadsheetMath.RegularizedIncompleteBeta(
                        degrees1 / 2, degrees2 / 2, degrees1 * x / (degrees1 * x + degrees2)),
                     probability, 0, 1000);
}

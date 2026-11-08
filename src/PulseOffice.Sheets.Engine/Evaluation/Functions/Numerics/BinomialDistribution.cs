using M = System.Math;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;

public static class BinomialDistribution
{
    private const double ExactCombinationLimit = 1000;

    private const double NormalApproximationVariance = 1e8;

    public static double Mass(double successes, double trials, double probability)
    {
        if (probability == 0) return successes == 0 ? 1 : 0;
        if (probability == 1) return successes == trials ? 1 : 0;
        if (M.Min(successes, trials - successes) <= ExactCombinationLimit)
        {
            var exact = SpreadsheetMath.Combinations(trials, successes)
                        * M.Pow(probability, successes) * M.Pow(1 - probability, trials - successes);
            if (double.IsFinite(exact) && exact > 0) return exact;
        }
        return M.Exp(SpreadsheetMath.LogGamma(trials + 1) - SpreadsheetMath.LogGamma(successes + 1)
                     - SpreadsheetMath.LogGamma(trials - successes + 1)
                     + successes * M.Log(probability) + (trials - successes) * M.Log(1 - probability));
    }

    public static double Cumulative(double successes, double trials, double probability)
    {
        if (successes < 0) return 0;
        if (successes >= trials || probability == 0) return 1;
        if (probability == 1) return 0;
        var variance = trials * probability * (1 - probability);
        if (variance > NormalApproximationVariance)
            return SpreadsheetMath.StandardNormalCdf((successes + 0.5 - trials * probability) / M.Sqrt(variance));
        return SpreadsheetMath.RegularizedIncompleteBeta(trials - successes, successes + 1, 1 - probability);
    }

    public static double Range(double first, double last, double trials, double probability) =>
        first == last
            ? Mass(first, trials, probability)
            : Cumulative(last, trials, probability) - Cumulative(first - 1, trials, probability);
}

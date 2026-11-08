using M = System.Math;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;

public static class NegativeBinomialDistribution
{
    public static double Mass(double failures, double successes, double probability)
    {
        if (probability == 1) return failures == 0 ? 1 : 0;
        var exact = SpreadsheetMath.Combinations(failures + successes - 1, successes - 1)
                    * M.Pow(probability, successes) * M.Pow(1 - probability, failures);
        if (double.IsFinite(exact) && exact > 0) return exact;
        return M.Exp(SpreadsheetMath.LogCombinations(failures + successes - 1, successes - 1)
                     + successes * M.Log(probability) + failures * M.Log(1 - probability));
    }
}

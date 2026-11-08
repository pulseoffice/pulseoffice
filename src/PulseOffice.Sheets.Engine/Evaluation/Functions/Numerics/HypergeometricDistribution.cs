using M = System.Math;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;

public static class HypergeometricDistribution
{
    private const double NegligibleShare = 1e-17;

    private const double ExactPopulationLimit = 1000;

    public static double Mass(double successes, double drawn, double populationSuccesses, double population) =>
        population <= ExactPopulationLimit
            ? SpreadsheetMath.Combinations(populationSuccesses, successes)
              * SpreadsheetMath.Combinations(population - populationSuccesses, drawn - successes)
              / SpreadsheetMath.Combinations(population, drawn)
            : M.Exp(SpreadsheetMath.LogCombinations(populationSuccesses, successes)
              + SpreadsheetMath.LogCombinations(population - populationSuccesses, drawn - successes)
              - SpreadsheetMath.LogCombinations(population, drawn));

    public static double Cumulative(double successes, double drawn, double populationSuccesses, double population)
    {
        var lowest = M.Max(0, drawn - (population - populationSuccesses));
        var highest = M.Min(drawn, populationSuccesses);
        if (successes >= highest) return 1;
        if (successes < lowest) return 0;

        var mode = M.Floor((drawn + 1) * (populationSuccesses + 1) / (population + 2));
        if (successes < mode)
            return M.Min(1, TailSum(successes, lowest, -1, drawn, populationSuccesses, population));
        return M.Max(0, 1 - TailSum(successes + 1, highest, 1, drawn, populationSuccesses, population));
    }

    private static double TailSum(double from, double limit, int step, double drawn, double populationSuccesses, double population)
    {
        var total = 0.0;
        for (var k = from; step > 0 ? k <= limit : k >= limit; k += step)
        {
            var term = Mass(k, drawn, populationSuccesses, population);
            total += term;
            if (term <= total * NegligibleShare) break;
        }
        return total;
    }
}

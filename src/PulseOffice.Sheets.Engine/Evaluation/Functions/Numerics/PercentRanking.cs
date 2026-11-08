using M = System.Math;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;

public static class PercentRanking
{
    public static double? Position(IReadOnlyList<double> sorted, double target)
    {
        if (sorted.Count == 0 || target < sorted[0] || target > sorted[^1]) return null;
        var below = 0;
        while (below < sorted.Count && sorted[below] < target) below++;
        if (below < sorted.Count && sorted[below] == target) return below;
        var low = sorted[below - 1];
        var high = sorted[below];
        return below - 1 + (target - low) / (high - low);
    }

    public static double Truncate(double value, int significance)
    {
        var scale = M.Pow(10, significance);
        return SpreadsheetMath.ApproxFloor(value * scale) / scale;
    }
}

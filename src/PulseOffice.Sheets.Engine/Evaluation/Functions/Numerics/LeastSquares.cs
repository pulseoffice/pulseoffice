using M = System.Math;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;

public sealed record LeastSquaresFit(
    double[] Coefficients, double Intercept, double[] CoefficientErrors, double InterceptError,
    double RSquared, double StandardError, double FStatistic, double DegreesOfFreedom,
    double RegressionSumOfSquares, double ResidualSumOfSquares);

public static class LeastSquares
{
    private const double DependenceTolerance = 1e-10;

    public static LeastSquaresFit? Fit(IReadOnlyList<double> y, IReadOnlyList<double[]> x, bool withIntercept)
    {
        var observations = y.Count;
        var regressors = x.Count == 0 ? 0 : x[0].Length;
        if (observations == 0 || regressors + (withIntercept ? 1 : 0) == 0 || x.Count != observations) return null;

        var kept = IndependentRegressors(x, regressors, withIntercept);
        var keptRegressors = kept.Count;
        var parameters = keptRegressors + (withIntercept ? 1 : 0);
        if (parameters == 0 || observations < parameters) return null;

        var design = new double[observations, parameters];
        for (var i = 0; i < observations; i++)
        {
            for (var j = 0; j < keptRegressors; j++) design[i, j] = x[i][kept[j]];
            if (withIntercept) design[i, keptRegressors] = 1;
        }

        if (Solve(design, y, parameters) is not var (beta, inverseDiagonal)) return null;

        var mean = y.Average();
        double residual = 0, regression = 0;
        for (var i = 0; i < observations; i++)
        {
            var predicted = 0.0;
            for (var a = 0; a < parameters; a++) predicted += design[i, a] * beta[a];
            residual += (y[i] - predicted) * (y[i] - predicted);
            regression += withIntercept ? (predicted - mean) * (predicted - mean) : predicted * predicted;
        }

        var freedom = observations - parameters;
        var variance = freedom > 0 ? residual / freedom : double.NaN;
        var errors = Enumerable.Range(0, parameters).Select(a => M.Sqrt(inverseDiagonal[a] * variance)).ToArray();
        var coefficients = new double[regressors];
        var coefficientErrors = new double[regressors];
        for (var j = 0; j < keptRegressors; j++)
        {
            coefficients[kept[j]] = beta[j];
            coefficientErrors[kept[j]] = errors[j];
        }

        var total = regression + residual;
        return new LeastSquaresFit(
            coefficients, withIntercept ? beta[keptRegressors] : 0,
            coefficientErrors, withIntercept ? errors[keptRegressors] : double.NaN,
            total == 0 ? 1 : regression / total, M.Sqrt(variance),
            keptRegressors == 0 ? double.NaN : residual == 0 ? double.PositiveInfinity : regression / keptRegressors / variance, freedom,
            regression, residual);
    }

    private static List<int> IndependentRegressors(IReadOnlyList<double[]> x, int regressors, bool withIntercept)
    {
        var observations = x.Count;
        var basis = new List<double[]>();
        if (withIntercept) basis.Add(Enumerable.Repeat(1 / M.Sqrt(observations), observations).ToArray());

        var kept = new List<int>();
        for (var j = 0; j < regressors; j++)
        {
            var column = new double[observations];
            for (var i = 0; i < observations; i++) column[i] = x[i][j];
            var original = Norm(column);
            if (original == 0 || !double.IsFinite(original)) continue;

            for (var pass = 0; pass < 2; pass++)
                foreach (var direction in basis)
                {
                    var projection = 0.0;
                    for (var i = 0; i < observations; i++) projection += direction[i] * column[i];
                    for (var i = 0; i < observations; i++) column[i] -= projection * direction[i];
                }

            var remaining = Norm(column);
            if (remaining <= DependenceTolerance * original) continue;
            for (var i = 0; i < observations; i++) column[i] /= remaining;
            basis.Add(column);
            kept.Add(j);
        }
        return kept;
    }

    private static double Norm(double[] vector)
    {
        var largest = 0.0;
        foreach (var value in vector) largest = M.Max(largest, M.Abs(value));
        if (largest == 0 || !double.IsFinite(largest)) return largest;
        var sum = 0.0;
        foreach (var value in vector) sum += (value / largest) * (value / largest);
        return largest * M.Sqrt(sum);
    }

    private static (double[] Beta, double[] InverseDiagonal)? Solve(double[,] design, IReadOnlyList<double> y, int parameters)
    {
        var rows = design.GetLength(0);
        var a = (double[,])design.Clone();
        var b = y.ToArray();
        for (var column = 0; column < parameters; column++)
        {
            var norm = 0.0;
            for (var row = column; row < rows; row++) norm = double.Hypot(norm, a[row, column]);
            if (norm == 0 || !double.IsFinite(norm)) return null;
            var alpha = a[column, column] > 0 ? -norm : norm;
            var v = new double[rows];
            for (var row = column; row < rows; row++) v[row] = a[row, column];
            v[column] -= alpha;
            var vv = 0.0;
            for (var row = column; row < rows; row++) vv += v[row] * v[row];
            if (vv == 0) continue;

            for (var k = column; k < parameters; k++)
            {
                var dot = 0.0;
                for (var row = column; row < rows; row++) dot += v[row] * a[row, k];
                var factor = 2 * dot / vv;
                for (var row = column; row < rows; row++) a[row, k] -= factor * v[row];
            }
            var projection = 0.0;
            for (var row = column; row < rows; row++) projection += v[row] * b[row];
            var bFactor = 2 * projection / vv;
            for (var row = column; row < rows; row++) b[row] -= bFactor * v[row];
        }

        var beta = new double[parameters];
        for (var row = parameters - 1; row >= 0; row--)
        {
            var sum = b[row];
            for (var k = row + 1; k < parameters; k++) sum -= a[row, k] * beta[k];
            beta[row] = sum / a[row, row];
        }

        var inverseR = new double[parameters, parameters];
        for (var column = parameters - 1; column >= 0; column--)
        {
            inverseR[column, column] = 1 / a[column, column];
            for (var row = column - 1; row >= 0; row--)
            {
                var sum = 0.0;
                for (var k = row + 1; k <= column; k++) sum += a[row, k] * inverseR[k, column];
                inverseR[row, column] = -sum / a[row, row];
            }
        }
        var diagonal = new double[parameters];
        for (var row = 0; row < parameters; row++)
            for (var k = row; k < parameters; k++) diagonal[row] += inverseR[row, k] * inverseR[row, k];
        return (beta, diagonal);
    }
}

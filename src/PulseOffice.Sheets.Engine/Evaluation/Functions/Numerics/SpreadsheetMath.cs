using M = System.Math;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;

public static class SpreadsheetMath
{
    public static double Factorial(int n)
    {
        var result = 1.0;
        for (var i = 2; i <= n && double.IsFinite(result); i++) result *= i;
        return result;
    }

    public static double DoubleFactorial(int n)
    {
        var result = 1.0;
        for (var i = n; i > 1 && double.IsFinite(result); i -= 2) result *= i;
        return result;
    }

    public static double Combinations(double n, double k)
    {
        k = M.Min(k, n - k);
        var result = 1.0;
        for (var i = 1; i <= k && double.IsFinite(result); i++) result = result * (n - k + i) / i;
        return M.Round(result);
    }

    public static double LogCombinations(double n, double k) =>
        LogGamma(n + 1) - LogGamma(k + 1) - LogGamma(n - k + 1);

    public static long GreatestCommonDivisor(long a, long b)
    {
        while (b != 0) (a, b) = (b, a % b);
        return M.Abs(a);
    }

    public static long LeastCommonMultiple(long a, long b) =>
        a == 0 || b == 0 ? 0 : checked(M.Abs(a / GreatestCommonDivisor(a, b) * b));

    public static double Mean(IReadOnlyList<double> values)
    {
        if (values.Count == 0) return double.NaN;
        var origin = values[0];
        var (sum, compensation) = (0.0, 0.0);
        foreach (var v in values)
        {
            var term = (v - origin) - compensation;
            var next = sum + term;
            compensation = (next - sum) - term;
            sum = next;
        }
        return origin + sum / values.Count;
    }

    public static double SumOfSquaredDeviations(IReadOnlyList<double> values)
    {
        var mean = Mean(values);
        var total = 0.0;
        foreach (var v in values) total += (v - mean) * (v - mean);
        return total;
    }

    public static double Variance(IReadOnlyList<double> values, bool population) =>
        SumOfSquaredDeviations(values) / (population ? values.Count : values.Count - 1);

    public static double StandardDeviation(IReadOnlyList<double> values, bool population) =>
        M.Sqrt(Variance(values, population));

    public static double Median(IEnumerable<double> values)
    {
        var sorted = values.ToArray();
        Array.Sort(sorted);
        var middle = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }

    public static double Quantile(IEnumerable<double> values, double fraction)
    {
        var sorted = values.ToArray();
        Array.Sort(sorted);
        if (sorted.Length == 1) return sorted[0];

        var position = fraction * (sorted.Length - 1);
        var lower = (int)M.Floor(position);
        var upper = (int)M.Ceiling(position);
        if (lower == upper) return sorted[lower];
        return sorted[lower] + (position - lower) * (sorted[upper] - sorted[lower]);
    }

    public static double LogGamma(double x)
    {
        double[] c =
        [
            0.99999999999980993, 676.5203681218851, -1259.1392167224028,
            771.32342877765313, -176.61502916214059, 12.507343278686905,
            -0.13857109526572012, 9.9843695780195716e-6, 1.5056327351493116e-7,
        ];

        if (x < 0.5)

            return M.Log(M.PI / M.Sin(M.PI * x)) - LogGamma(1 - x);

        x -= 1;
        var a = c[0];
        var t = x + 7.5;
        for (var i = 1; i < 9; i++) a += c[i] / (x + i);
        return 0.5 * M.Log(2 * M.PI) + (x + 0.5) * M.Log(t) - t + M.Log(a);
    }

    public static double Gamma(double x)
    {
        if (x <= 0 && x == M.Floor(x)) return double.NaN;
        return x < 0.5
            ? M.PI / (M.Sin(M.PI * x) * Gamma(1 - x))
            : M.Exp(LogGamma(x));
    }

    public static double Beta(double a, double b) => M.Exp(LogGamma(a) + LogGamma(b) - LogGamma(a + b));

    private static readonly double[] ErfSmallNumerator =
        [3.16112374387056560e00, 1.13864154151050156e02, 3.77485237685302021e02,
         3.20937758913846947e03, 1.85777706184603153e-1];
    private static readonly double[] ErfSmallDenominator =
        [2.36012909523441209e01, 2.44024637934444173e02, 1.28261652607737228e03,
         2.84423683343917062e03];
    private static readonly double[] ErfMiddleNumerator =
        [5.64188496988670089e-1, 8.88314979438837594e00, 6.61191906371416295e01,
         2.98635138197400131e02, 8.81952221241769090e02, 1.71204761263407058e03,
         2.05107837782607147e03, 1.23033935479799725e03, 2.15311535474403846e-8];
    private static readonly double[] ErfMiddleDenominator =
        [1.57449261107098347e01, 1.17693950891312499e02, 5.37181101862009858e02,
         1.62138957456669019e03, 3.29079923573345963e03, 4.36261909014324716e03,
         3.43936767414372164e03, 1.23033935480374942e03];
    private static readonly double[] ErfLargeNumerator =
        [3.05326634961232344e-1, 3.60344899949804439e-1, 1.25781726111229246e-1,
         1.60837851487422766e-2, 6.58749161529837803e-4, 1.63153871373020978e-2];
    private static readonly double[] ErfLargeDenominator =
        [2.56852019228982242e00, 1.87295284992346725e00, 5.27905102951428412e-1,
         6.05183413124413191e-2, 2.33520497626869185e-3];

    public static double Erf(double x)
    {
        var y = M.Abs(x);
        if (y <= 0.46875) return ErfSmall(x);
        var complement = ErfcPositive(y);
        var result = (0.5 - complement) + 0.5;
        return x < 0 ? -result : result;
    }

    public static double Erfc(double x)
    {
        var y = M.Abs(x);
        if (y <= 0.46875) return 1 - ErfSmall(x);
        var complement = ErfcPositive(y);
        return x < 0 ? 2 - complement : complement;
    }

    private static double ErfSmall(double x)
    {
        var a = ErfSmallNumerator;
        var b = ErfSmallDenominator;
        var square = M.Abs(x) > 1.11e-16 ? x * x : 0;
        var numerator = a[4] * square;
        var denominator = square;
        for (var i = 0; i < 3; i++)
        {
            numerator = (numerator + a[i]) * square;
            denominator = (denominator + b[i]) * square;
        }
        return x * (numerator + a[3]) / (denominator + b[3]);
    }

    private static double ErfcPositive(double y)
    {
        double result;
        if (y <= 4)
        {
            var c = ErfMiddleNumerator;
            var d = ErfMiddleDenominator;
            var numerator = c[8] * y;
            var denominator = y;
            for (var i = 0; i < 7; i++)
            {
                numerator = (numerator + c[i]) * y;
                denominator = (denominator + d[i]) * y;
            }
            result = (numerator + c[7]) / (denominator + d[7]);
        }
        else
        {
            if (y >= 26.543) return 0;
            var p = ErfLargeNumerator;
            var q = ErfLargeDenominator;
            var inverseSquare = 1 / (y * y);
            var numerator = p[5] * inverseSquare;
            var denominator = inverseSquare;
            for (var i = 0; i < 4; i++)
            {
                numerator = (numerator + p[i]) * inverseSquare;
                denominator = (denominator + q[i]) * inverseSquare;
            }
            result = inverseSquare * (numerator + p[4]) / (denominator + q[4]);
            result = (5.6418958354775628695e-1 - result) / y;
        }

        var truncated = M.Truncate(y * 16) / 16;
        var delta = (y - truncated) * (y + truncated);
        return M.Exp(-truncated * truncated) * M.Exp(-delta) * result;
    }

    public static double Power(double value, double exponent)
    {
        if (value >= 0 || exponent == M.Floor(exponent)) return M.Pow(value, exponent);
        var root = 1 / exponent;
        var rounded = M.Round(root);
        return M.Abs(root - rounded) < 1e-12 && M.Abs(rounded % 2) == 1
            ? -M.Pow(-value, exponent)
            : double.NaN;
    }


    public static double ApproxAdd(double left, double right) =>
        ((left < 0 && right > 0) || (left > 0 && right < 0)) && ApproxEqual(left, -right) ? 0 : left + right;

    public static int ApproxCompare(double left, double right) => ApproxEqual(left, right) ? 0 : left.CompareTo(right);

    public static bool ApproxEqual(double left, double right)
    {
        if (left == right) return true;
        if (left == 0 || right == 0 || double.IsNegative(left) != double.IsNegative(right)) return false;
        var difference = M.Abs(left - right);
        if (!double.IsFinite(difference)) return false;
        var smaller = M.Min(M.Abs(left), M.Abs(right));
        var threshold = M.Max(smaller * 3.552713678800501e-15, M.Pow(10, M.Floor(M.Log10(smaller))) * 5e-15);
        if (difference >= threshold) return false;
        return !(IsRepresentableInteger(left) && IsRepresentableInteger(right));
    }

    private static bool IsRepresentableInteger(double value) =>
        M.Abs(value) <= 9007199254740992d && M.Floor(value) == value;

    public static double ApproxValue(double value)
    {
        if (value == 0 || !double.IsFinite(value)) return value;
        return double.Parse(value.ToString("G15", System.Globalization.CultureInfo.InvariantCulture),
                            System.Globalization.CultureInfo.InvariantCulture);
    }

    public static double ApproxFloor(double value) => M.Floor(ApproxValue(value));

    public static double ApproxCeiling(double value) => M.Ceiling(ApproxValue(value));

    public static double ExclusiveQuantile(IEnumerable<double> values, double fraction)
    {
        var sorted = values.ToArray();
        Array.Sort(sorted);
        var count = sorted.Length;
        if (count == 0 || fraction <= 0 || fraction >= 1) return double.NaN;

        var position = fraction * (count + 1);
        if (position < 1 || position > count) return double.NaN;

        var lower = (int)M.Floor(position);
        var weight = position - lower;
        return lower == count ? sorted[count - 1]
            : sorted[lower - 1] + weight * (sorted[lower] - sorted[lower - 1]);
    }

    public static double StandardNormalCdf(double z) => 0.5 * Erfc(-z / M.Sqrt(2));

    public static double StandardNormalPdf(double z) => M.Exp(-0.5 * z * z) / M.Sqrt(2 * M.PI);

    public static double StandardNormalInverse(double p)
    {
        if (p is <= 0 or >= 1) return double.NaN;

        double[] a = [-3.969683028665376e+01, 2.209460984245205e+02, -2.759285104469687e+02,
                       1.383577518672690e+02, -3.066479806614716e+01, 2.506628277459239e+00];
        double[] b = [-5.447609879822406e+01, 1.615858368580409e+02, -1.556989798598866e+02,
                       6.680131188771972e+01, -1.328068155288572e+01];
        double[] c = [-7.784894002430293e-03, -3.223964580411365e-01, -2.400758277161838e+00,
                      -2.549732539343734e+00, 4.374664141464968e+00, 2.938163982698783e+00];
        double[] d = [7.784695709041462e-03, 3.224671290700398e-01, 2.445134137142996e+00,
                      3.754408661907416e+00];

        const double Low = 0.02425, High = 1 - Low;
        double x;

        if (p < Low)
        {
            var q = M.Sqrt(-2 * M.Log(p));
            x = (((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) /
                ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
        }
        else if (p <= High)
        {
            var q = p - 0.5;
            var r = q * q;
            x = (((((a[0] * r + a[1]) * r + a[2]) * r + a[3]) * r + a[4]) * r + a[5]) * q /
                (((((b[0] * r + b[1]) * r + b[2]) * r + b[3]) * r + b[4]) * r + 1);
        }
        else
        {
            var q = M.Sqrt(-2 * M.Log(1 - p));
            x = -(((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) /
                 ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
        }

        var e = StandardNormalCdf(x) - p;
        var u = e * M.Sqrt(2 * M.PI) * M.Exp(x * x / 2);
        return x - u / (1 + x * u / 2);
    }

    private const int ContinuedFractionLimit = 100_000;

    public static double RegularizedIncompleteBeta(double a, double b, double x)
    {
        if (x <= 0) return 0;
        if (x >= 1) return 1;

        var front = M.Exp(LogGamma(a + b) - LogGamma(a) - LogGamma(b)
                          + a * M.Log(x) + b * M.Log(1 - x));

        if (x > (a + 1) / (a + b + 2))
            return 1 - RegularizedIncompleteBeta(b, a, 1 - x);

        const double Tiny = 1e-30;
        var f = 1.0; var c = 1.0; var d = 0.0;

        for (var i = 0; i <= ContinuedFractionLimit; i++)
        {
            var m = i / 2;
            double numerator;

            if (i == 0) numerator = 1.0;
            else if (i % 2 == 0) numerator = m * (b - m) * x / ((a + 2 * m - 1) * (a + 2 * m));
            else numerator = -((a + m) * (a + b + m) * x) / ((a + 2 * m) * (a + 2 * m + 1));

            d = 1 + numerator * d;
            if (M.Abs(d) < Tiny) d = Tiny;
            d = 1 / d;

            c = 1 + numerator / c;
            if (M.Abs(c) < Tiny) c = Tiny;

            var delta = c * d;
            f *= delta;

            if (M.Abs(1 - delta) < 1e-14) break;
        }

        return front * (f - 1) / a;
    }

    public static double RegularizedIncompleteGamma(double a, double x)
    {
        if (x <= 0) return 0;

        if (x < a + 1)
        {
            var sum = 1.0 / a;
            var term = sum;
            for (var n = 1; n < ContinuedFractionLimit; n++)
            {
                term *= x / (a + n);
                sum += term;
                if (M.Abs(term) < M.Abs(sum) * 1e-15) break;
            }
            return sum * M.Exp(-x + a * M.Log(x) - LogGamma(a));
        }

        const double Tiny = 1e-30;
        var b = x + 1 - a;
        var c = 1 / Tiny;
        var d = 1 / b;
        var h = d;

        for (var i = 1; i < ContinuedFractionLimit; i++)
        {
            var an = -i * (i - a);
            b += 2;
            d = an * d + b;
            if (M.Abs(d) < Tiny) d = Tiny;
            c = b + an / c;
            if (M.Abs(c) < Tiny) c = Tiny;
            d = 1 / d;
            var delta = d * c;
            h *= delta;
            if (M.Abs(delta - 1) < 1e-15) break;
        }

        return 1 - M.Exp(-x + a * M.Log(x) - LogGamma(a)) * h;
    }
}

using M = System.Math;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;

public static class BesselFunctions
{
    private const double EulerGamma = 0.57721566490153286;
    private const double MaxIterations = 9_000_000;
    private const double AsymptoticThreshold = 5e6;

    public static double J(double x, int order)
    {
        if (order < 0) return double.NaN;
        if (x == 0) return order == 0 ? 1 : 0;

        var ax = M.Abs(x);
        if (ax > AsymptoticThreshold && order * (double)order < ax)
        {
            var asymptotic = M.Sqrt(2 / (M.PI * ax)) * M.Cos(ax - order * M.PI / 2 - M.PI / 4);
            return x < 0 && order % 2 == 1 ? -asymptotic : asymptotic;
        }
        var reach = M.Max(order, ax);
        var span = reach + 30 + 10 * M.Sqrt(reach);
        if (span > MaxIterations) return double.NaN;
        var start = 2 * (int)(span / 2);
        double following = 0, current = 1, normalisation = 0, wanted = 0;
        for (var k = start; k >= 1; k--)
        {
            var previous = 2 * k / ax * current - following;
            following = current;
            current = previous;
            var index = k - 1;
            if (index == order) wanted = current;
            if (index == 0) normalisation += current;
            else if (index % 2 == 0) normalisation += 2 * current;
            if (M.Abs(current) > 1e250)
            {
                current *= 1e-250;
                following *= 1e-250;
                normalisation *= 1e-250;
                wanted *= 1e-250;
            }
        }
        var value = wanted / normalisation;
        return x < 0 && order % 2 == 1 ? -value : value;
    }

    public static double I(double x, int order)
    {
        if (order < 0) return double.NaN;
        if (x == 0) return order == 0 ? 1 : 0;

        var half = x / 2;
        var term = M.Exp(order * M.Log(M.Abs(half)) - SpreadsheetMath.LogGamma(order + 1));
        if (x < 0 && order % 2 == 1) term = -term;
        var sum = term;
        var square = half * half;
        for (var k = 1; k < 100_000; k++)
        {
            term *= square / ((double)k * (k + order));
            sum += term;
            if (M.Abs(term) < 1e-17 * M.Abs(sum)) break;
        }
        return sum;
    }

    public static double Y(double x, int order)
    {
        if (x <= 0 || order < 0) return double.NaN;
        var previous = Y0(x);
        if (order == 0) return previous;
        var current = Y1(x);
        for (var n = 1; n < order; n++)
        {
            var next = n * (2 / x) * current - previous;
            previous = current;
            current = next;
            if (!double.IsFinite(current)) return double.NaN;
        }
        return current;
    }

    public static double K(double x, int order)
    {
        if (x <= 0 || order < 0) return double.NaN;
        var previous = K0(x);
        if (order == 0) return previous;
        var current = K1(x);
        for (var n = 1; n < order; n++)
        {
            var next = previous + n * (2 / x) * current;
            previous = current;
            current = next;
            if (!double.IsFinite(current)) return double.NaN;
        }
        return current;
    }

    private static double K0(double x)
    {
        if (x <= 2)
        {
            var half = x / 2;
            var y = half * half;
            return -M.Log(half) * I(x, 0)
                   + (-0.57721566 + y * (0.42278420 + y * (0.23069756 + y * (0.3488590e-1
                   + y * (0.262698e-2 + y * (0.10750e-3 + y * 0.74e-5))))));
        }
        var t = 2 / x;
        return M.Exp(-x) / M.Sqrt(x) * (1.25331414 + t * (-0.7832358e-1 + t * (0.2189568e-1
               + t * (-0.1062446e-1 + t * (0.587872e-2 + t * (-0.251540e-2 + t * 0.53208e-3))))));
    }

    private static double K1(double x)
    {
        if (x <= 2)
        {
            var half = x / 2;
            var y = half * half;
            return M.Log(half) * I(x, 1)
                   + (1 + y * (0.15443144 + y * (-0.67278579 + y * (-0.18156897 + y * (-0.1919402e-1
                   + y * (-0.110404e-2 + y * -0.4686e-4)))))) / x;
        }
        var t = 2 / x;
        return M.Exp(-x) / M.Sqrt(x) * (1.25331414 + t * (0.23498619 + t * (-0.3655620e-1
               + t * (0.1504268e-1 + t * (-0.780353e-2 + t * (0.325614e-2 + t * -0.68245e-3))))));
    }

    private static double Y0(double x)
    {
        if (x > AsymptoticThreshold) return M.Sqrt(1 / M.PI / x) * (M.Sin(x) - M.Cos(x));
        var u = M.Log(x / 2) + EulerGamma;
        var deltaU = 0.0;
        var gBar = -2 / x;
        var g = -1 / gBar;
        var fBar = -g;
        var signAlpha = 1.0;
        for (var k = 2.0; k < MaxIterations; k++)
        {
            var odd = (k - 1) % 2;
            var mBar = 2 * odd * fBar;
            double alpha;
            if (odd == 0) alpha = 0;
            else
            {
                alpha = signAlpha * (4 / k);
                signAlpha = -signAlpha;
            }
            var gBarDeltaU = fBar * alpha - g * deltaU - mBar * u;
            gBar = mBar - 2 * k / x + g;
            deltaU = gBarDeltaU / gBar;
            u += deltaU;
            g = -1 / gBar;
            fBar *= g;
            if (M.Abs(deltaU) <= M.Abs(u) * 1e-15) return u * 2 / M.PI;
        }
        return double.NaN;
    }

    private static double Y1(double x)
    {
        if (x > AsymptoticThreshold) return -M.Sqrt(1 / M.PI / x) * (M.Sin(x) + M.Cos(x));
        var u = 1 / x;
        var fBar = -1.0;
        var alpha = 1 - EulerGamma - M.Log(x / 2);
        var gBar = -2 / x;
        var deltaU = -alpha / gBar;
        u += deltaU;
        var g = -1 / gBar;
        fBar *= g;
        var signAlpha = -1.0;
        for (var k = 2.0; k < MaxIterations; k++)
        {
            var odd = (k - 1) % 2;
            var mBar = 2 * odd * fBar;
            var q = (k - 1) / 2;
            if (odd == 0)
            {
                alpha = signAlpha * (1 / q + 1 / (q + 1));
                signAlpha = -signAlpha;
            }
            else alpha = 0;
            var gBarDeltaU = fBar * alpha - g * deltaU - mBar * u;
            gBar = mBar - 2 * k / x + g;
            deltaU = gBarDeltaU / gBar;
            u += deltaU;
            g = -1 / gBar;
            fBar *= g;
            if (M.Abs(deltaU) <= M.Abs(u) * 1e-15) return -u * 2 / M.PI;
        }
        return double.NaN;
    }
}

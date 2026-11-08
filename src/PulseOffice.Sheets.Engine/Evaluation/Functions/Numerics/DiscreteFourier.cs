using System.Numerics;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;

public static class DiscreteFourier
{
    private const int DirectLimit = 64;

    public static Complex[] TransformUnscaled(IReadOnlyList<Complex> samples, bool inverse)
    {
        var n = samples.Count;
        var sign = inverse ? 1.0 : -1.0;
        return n <= DirectLimit ? Direct(samples, sign) : Bluestein(samples, sign);
    }

    private static Complex[] Direct(IReadOnlyList<Complex> samples, double sign)
    {
        var n = samples.Count;
        var result = new Complex[n];
        for (var k = 0; k < n; k++)
        {
            var accumulator = Complex.Zero;
            for (var t = 0; t < n; t++)
                accumulator += samples[t] * Twiddle(sign, (long)k * t % n, n);
            result[k] = accumulator;
        }
        return result;
    }

    private static Complex[] Bluestein(IReadOnlyList<Complex> samples, double sign)
    {
        var n = samples.Count;
        var size = 1;
        while (size < 2 * n - 1) size <<= 1;

        var chirp = new Complex[n];
        for (var k = 0; k < n; k++)
            chirp[k] = Twiddle(sign, (long)k * k % (2L * n), 2L * n);

        var a = new Complex[size];
        var b = new Complex[size];
        for (var k = 0; k < n; k++) a[k] = samples[k] * chirp[k];
        b[0] = Complex.Conjugate(chirp[0]);
        for (var k = 1; k < n; k++) b[k] = b[size - k] = Complex.Conjugate(chirp[k]);

        Radix2(a, false);
        Radix2(b, false);
        for (var i = 0; i < size; i++) a[i] *= b[i];
        Radix2(a, true);

        var result = new Complex[n];
        for (var k = 0; k < n; k++) result[k] = a[k] / size * chirp[k];
        return result;
    }

    private static void Radix2(Complex[] values, bool inverse)
    {
        var n = values.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            var bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) (values[i], values[j]) = (values[j], values[i]);
        }

        for (var length = 2; length <= n; length <<= 1)
        {
            var half = length / 2;
            for (var start = 0; start < n; start += length)
                for (var k = 0; k < half; k++)
                {
                    var factor = Twiddle(inverse ? 1.0 : -1.0, k, length);
                    var even = values[start + k];
                    var odd = values[start + k + half] * factor;
                    values[start + k] = even + odd;
                    values[start + k + half] = even - odd;
                }
        }
    }

    private static Complex Twiddle(double sign, long numerator, long denominator)
    {
        var angle = sign * 2 * System.Math.PI * numerator / denominator;
        return new Complex(System.Math.Cos(angle), System.Math.Sin(angle));
    }
}

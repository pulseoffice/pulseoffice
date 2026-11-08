using PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;
using PulseOffice.Sheets.Values;
using M = System.Math;
using F = PulseOffice.Sheets.Engine.Evaluation.Functions.FunctionBuilder;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class DistributionFunctions : IFunctionProvider
{
    public string Category => "Statistica";

    public IEnumerable<FunctionDefinition> GetFunctions()
    {
        var builder = new FunctionBuilder(Category);

        RegisterNormal(builder);
        RegisterStudentAndChiSquare(builder);
        RegisterFisher(builder);
        RegisterDiscrete(builder);
        RegisterContinuous(builder);
        RegisterRegression(builder);

        return builder.Build();
    }

    private static void RegisterNormal(FunctionBuilder builder)
    {
        builder
            .Numeric1("NORMSDIST", z => F.Number(SpreadsheetMath.StandardNormalCdf(z)))
            .Numeric1("NORMSINV", p => p is <= 0 or >= 1
                ? F.OutOfDomain() : F.Number(SpreadsheetMath.StandardNormalInverse(p)))
            .Numeric1("NORM.S.INV", p => p is <= 0 or >= 1
                ? F.OutOfDomain() : F.Number(SpreadsheetMath.StandardNormalInverse(p)))
            .Numeric1("GAUSS", z => F.Number(SpreadsheetMath.StandardNormalCdf(z) - 0.5))
            .Numeric1("PHI", z => F.Number(SpreadsheetMath.StandardNormalPdf(z)));

        builder.Raw("NORM.S.DIST", 2, 2, r =>
        {
            var z = r.Number(0);
            var cumulative = r.Boolean(1, true);
            if (r.HasError) return r.Error;
            return F.Number(cumulative ? SpreadsheetMath.StandardNormalCdf(z) : SpreadsheetMath.StandardNormalPdf(z));
        });

        foreach (var (name, minimum) in (ReadOnlySpan<(string, int)>)[("NORMDIST", 3), ("NORM.DIST", 4)])
            builder.Raw(name, minimum, 4, r =>
            {
                var x = r.Number(0);
                var mean = r.Number(1);
                var deviation = r.Number(2);
                var cumulative = r.Boolean(3, true);
                if (r.HasError) return r.Error;
                if (deviation <= 0) return F.OutOfDomain();

                var z = (x - mean) / deviation;
                return F.Number(cumulative
                    ? SpreadsheetMath.StandardNormalCdf(z)
                    : SpreadsheetMath.StandardNormalPdf(z) / deviation);
            });

        foreach (var name in (string[])["NORMINV", "NORM.INV"])
            builder.Raw(name, 3, 3, r =>
            {
                var p = r.Number(0);
                var mean = r.Number(1);
                var deviation = r.Number(2);
                if (r.HasError) return r.Error;
                if (deviation <= 0 || p is <= 0 or >= 1) return F.OutOfDomain();
                return F.Number(mean + deviation * SpreadsheetMath.StandardNormalInverse(p));
            });

        builder.Raw("STANDARDIZE", 3, 3, r =>
        {
            var x = r.Number(0);
            var mean = r.Number(1);
            var deviation = r.Number(2);
            if (r.HasError) return r.Error;
            return deviation <= 0 ? F.OutOfDomain() : F.Number((x - mean) / deviation);
        });

        foreach (var name in (string[])["CONFIDENCE", "CONFIDENCE.NORM"])
            builder.Raw(name, 3, 3, r =>
            {
                var alpha = r.Number(0);
                var deviation = r.Number(1);
                var size = r.Number(2);
                if (r.HasError) return r.Error;
                if (alpha is <= 0 or >= 1 || deviation <= 0 || size < 1) return F.OutOfDomain();
                return F.Number(SpreadsheetMath.StandardNormalInverse(1 - alpha / 2) * deviation / M.Sqrt(size));
            });

        foreach (var (name, minimum) in (ReadOnlySpan<(string, int)>)[("LOGNORMDIST", 1), ("LOGNORM.DIST", 3)])
            builder.Raw(name, minimum, 4, r =>
            {
                var x = r.Number(0);
                var mean = r.Number(1, 0);
                var deviation = r.Number(2, 1);
                var cumulative = r.Boolean(3, true);
                if (r.HasError) return r.Error;
                if (x <= 0 || deviation <= 0) return F.OutOfDomain();

                var z = (M.Log(x) - mean) / deviation;
                return F.Number(cumulative
                    ? SpreadsheetMath.StandardNormalCdf(z)
                    : SpreadsheetMath.StandardNormalPdf(z) / (x * deviation));
            });

        foreach (var (name, minimum) in (ReadOnlySpan<(string, int)>)[("LOGINV", 1), ("LOGNORM.INV", 3)])
            builder.Raw(name, minimum, 3, r =>
            {
                var p = r.Number(0);
                var mean = r.Number(1, 0);
                var deviation = r.Number(2, 1);
                if (r.HasError) return r.Error;
                if (p is <= 0 or >= 1 || deviation <= 0) return F.OutOfDomain();
                return F.Number(M.Exp(mean + deviation * SpreadsheetMath.StandardNormalInverse(p)));
            });
    }

    private static void RegisterStudentAndChiSquare(FunctionBuilder builder)
    {
        builder.Raw("TDIST", 3, 3, r =>
        {
            var t = r.Number(0);
            var degrees = M.Floor(r.Number(1));
            var tails = r.Integer(2);
            if (r.HasError) return r.Error;
            if (t < 0 || degrees < 1 || tails is < 1 or > 2) return F.OutOfDomain();

            var p = StudentRightTail(t, degrees);
            return F.Number(tails == 1 ? p : 2 * p);
        });

        builder.Raw("T.DIST", 3, 3, r =>
        {
            var t = r.Number(0);
            var degrees = M.Floor(r.Number(1));
            var cumulative = r.Boolean(2, true);
            if (r.HasError) return r.Error;
            if (degrees < 1) return F.OutOfDomain();

            return F.Number(cumulative ? 1 - StudentRightTail(M.Abs(t), degrees) * (t < 0 ? -1 : 1) - (t < 0 ? 1 : 0)
                                       : StudentDensity(t, degrees));
        });

        builder.Raw("T.DIST.RT", 2, 2, r =>
        {
            var t = r.Number(0);
            var degrees = M.Floor(r.Number(1));
            if (r.HasError) return r.Error;
            return degrees < 1 ? F.OutOfDomain() : F.Number(t >= 0 ? StudentRightTail(t, degrees) : 1 - StudentRightTail(-t, degrees));
        });

        builder.Raw("T.DIST.2T", 2, 2, r =>
        {
            var t = r.Number(0);
            var degrees = M.Floor(r.Number(1));
            if (r.HasError) return r.Error;
            return t < 0 || degrees < 1 ? F.OutOfDomain() : F.Number(2 * StudentRightTail(t, degrees));
        });

        builder.Raw("CHISQDIST", 2, 3, r =>
        {
            var x = r.Number(0);
            var degrees = M.Floor(r.Number(1));
            var cumulative = r.Boolean(2, true);
            if (r.HasError) return r.Error;
            if (degrees < 1) return F.OutOfDomain();
            if (x < 0) return CellValue.Number(0);
            return F.Number(cumulative ? SpreadsheetMath.RegularizedIncompleteGamma(degrees / 2, x / 2) : GammaDensity(x, degrees / 2, 2));
        });

        foreach (var name in (string[])["CHIDIST", "CHISQ.DIST.RT"])
            builder.Raw(name, 2, 3, r =>
            {
                var x = r.Number(0);
                var degrees = M.Floor(r.Number(1));
                if (r.HasError) return r.Error;
                if (x < 0 || degrees < 1) return F.OutOfDomain();
                return F.Number(1 - SpreadsheetMath.RegularizedIncompleteGamma(degrees / 2, x / 2));
            });

        builder.Raw("CHISQ.DIST", 3, 3, r =>
        {
            var x = r.Number(0);
            var degrees = M.Floor(r.Number(1));
            var cumulative = r.Boolean(2, true);
            if (r.HasError) return r.Error;
            if (x < 0 || degrees < 1) return F.OutOfDomain();

            if (cumulative) return F.Number(SpreadsheetMath.RegularizedIncompleteGamma(degrees / 2, x / 2));

            return F.Number(GammaDensity(x, degrees / 2, 2));
        });
    }

    private static void RegisterFisher(FunctionBuilder builder)
    {
        builder
            .Numeric1("FISHER", x => x is <= -1 or >= 1
                ? F.OutOfDomain() : F.Number(0.5 * M.Log((1 + x) / (1 - x))))
            .Numeric1("FISHERINV", y => F.Number(M.Tanh(y)));

        foreach (var name in (string[])["FDIST", "F.DIST.RT"])
            builder.Raw(name, 3, 3, r =>
            {
                var x = r.Number(0);
                var d1 = M.Floor(r.Number(1));
                var d2 = M.Floor(r.Number(2));
                if (r.HasError) return r.Error;
                if (x < 0 || d1 < 1 || d2 < 1) return F.OutOfDomain();
                return F.Number(1 - FisherCdf(x, d1, d2));
            });

        builder.Raw("F.DIST", 3, 4, r =>
        {
            var x = r.Number(0);
            var d1 = M.Floor(r.Number(1));
            var d2 = M.Floor(r.Number(2));
            var cumulative = r.Boolean(3, true);
            if (r.HasError) return r.Error;
            if (x < 0 || d1 < 1 || d2 < 1) return F.OutOfDomain();
            if (!cumulative) return x == 0 ? F.Number(d1 < 2 ? double.PositiveInfinity : d1 == 2 ? 1 : 0) : F.Number(FisherDensity(x, d1, d2));
            return F.Number(FisherCdf(x, d1, d2));
        });
    }

    private static void RegisterDiscrete(FunctionBuilder builder)
    {
        foreach (var name in (string[])["BINOMDIST", "BINOM.DIST"])
            builder.Raw(name, 4, 4, r =>
            {
                var successes = M.Floor(r.Number(0));
                var trials = M.Floor(r.Number(1));
                var probability = r.Number(2);
                var cumulative = r.Boolean(3, true);
                if (r.HasError) return r.Error;
                if (successes < 0 || successes > trials || probability is < 0 or > 1) return F.OutOfDomain();

                return F.Number(cumulative
                    ? BinomialDistribution.Cumulative(successes, trials, probability)
                    : BinomialDistribution.Mass(successes, trials, probability));
            });

        foreach (var name in (string[])["POISSON", "POISSON.DIST"])
            builder.Raw(name, 2, 3, r =>
            {
                var x = M.Floor(r.Number(0));
                var mean = r.Number(1);
                var cumulative = r.Boolean(2, true);
                if (r.HasError) return r.Error;
                if (x < 0 || mean < 0) return F.OutOfDomain();

                if (!cumulative)
                    return F.Number(mean == 0 ? (x == 0 ? 1 : 0) : M.Exp(-mean + x * M.Log(mean) - SpreadsheetMath.LogGamma(x + 1)));

                return F.Number(1 - SpreadsheetMath.RegularizedIncompleteGamma(x + 1, mean));
            });

        foreach (var (name, minimum) in ((string, int)[])[("HYPGEOMDIST", 4), ("HYPGEOM.DIST", 5)])
            builder.Raw(name, minimum, 5, r =>
            {
                var drawnSuccesses = M.Floor(r.Number(0));
                var drawn = M.Floor(r.Number(1));
                var populationSuccesses = M.Floor(r.Number(2));
                var population = M.Floor(r.Number(3));
                var cumulative = r.Boolean(4, false);
                if (r.HasError) return r.Error;
                if (drawnSuccesses < 0 || drawn < 0 || populationSuccesses < 0 || drawn > population || populationSuccesses > population
                    || drawnSuccesses > drawn || drawnSuccesses > populationSuccesses || drawn - drawnSuccesses > population - populationSuccesses)
                    return F.OutOfDomain();

                return F.Number(cumulative
                    ? HypergeometricDistribution.Cumulative(drawnSuccesses, drawn, populationSuccesses, population)
                    : HypergeometricDistribution.Mass(drawnSuccesses, drawn, populationSuccesses, population));
            });

        foreach (var name in (string[])["CRITBINOM", "BINOM.INV"])
            builder.Raw(name, 3, 3, r =>
            {
                var trials = M.Floor(r.Number(0));
                var probability = r.Number(1);
                var alpha = r.Number(2);
                if (r.HasError) return r.Error;
                if (trials < 0 || probability is < 0 or > 1 || alpha is < 0 or > 1) return F.OutOfDomain();
                if (alpha >= 1) return CellValue.Number(probability == 0 ? 0 : trials);

                var (low, high) = (0.0, trials);
                while (low < high)
                {
                    var middle = M.Floor((low + high) / 2);
                    if (BinomialDistribution.Cumulative(middle, trials, probability) >= alpha) high = middle;
                    else low = middle + 1;
                }
                return CellValue.Number(low);
            });

        builder.Raw("NEGBINOMDIST", 3, 3, r =>
        {
            var failures = M.Floor(r.Number(0));
            var successes = M.Floor(r.Number(1));
            var probability = r.Number(2);
            if (r.HasError) return r.Error;
            if (failures < 0 || successes < 1 || probability is <= 0 or > 1) return F.OutOfDomain();

            return F.Number(NegativeBinomialDistribution.Mass(failures, successes, probability));
        });
    }

    private static void RegisterContinuous(FunctionBuilder builder)
    {
        foreach (var name in (string[])["EXPONDIST", "EXPON.DIST"])
            builder.Raw(name, 3, 3, r =>
            {
                var x = r.Number(0);
                var lambda = r.Number(1);
                var cumulative = r.Boolean(2, true);
                if (r.HasError) return r.Error;
                if (x < 0 || lambda <= 0) return F.OutOfDomain();
                return F.Number(cumulative ? 1 - M.Exp(-lambda * x) : lambda * M.Exp(-lambda * x));
            });

        foreach (var name in (string[])["WEIBULL", "WEIBULL.DIST"])
            builder.Raw(name, 4, 4, r =>
            {
                var x = r.Number(0);
                var shape = r.Number(1);
                var scale = r.Number(2);
                var cumulative = r.Boolean(3, true);
                if (r.HasError) return r.Error;
                if (x < 0 || shape <= 0 || scale <= 0) return F.OutOfDomain();

                var ratio = M.Pow(x / scale, shape);
                return F.Number(cumulative
                    ? 1 - M.Exp(-ratio)
                    : shape / M.Pow(scale, shape) * M.Pow(x, shape - 1) * M.Exp(-ratio));
            });

        foreach (var name in (string[])["GAMMADIST", "GAMMA.DIST"])
            builder.Raw(name, 3, 4, r =>
            {
                var x = r.Number(0);
                var shape = r.Number(1);
                var scale = r.Number(2);
                var cumulative = r.Boolean(3, true);
                if (r.HasError) return r.Error;
                if (x < 0 || shape <= 0 || scale <= 0) return F.OutOfDomain();

                if (cumulative) return F.Number(SpreadsheetMath.RegularizedIncompleteGamma(shape, x / scale));
                return F.Number(GammaDensity(x, shape, scale));
            });

        builder.Raw("BETADIST", 3, 6, r => BetaDistribution(r, cumulativeAt: 5, lowerAt: 3));
        builder.Raw("BETA.DIST", 4, 6, r => BetaDistribution(r, cumulativeAt: 3, lowerAt: 4));
    }

    private static CellValue BetaDistribution(ArgumentReader r, int cumulativeAt, int lowerAt)
    {
        var x = r.Number(0);
        var alpha = r.Number(1);
        var beta = r.Number(2);
        var cumulative = r.Boolean(cumulativeAt, true);
        var lower = r.Number(lowerAt, 0);
        var upper = r.Number(lowerAt + 1, 1);
        if (r.HasError) return r.Error;
        if (alpha <= 0 || beta <= 0 || upper <= lower || x < lower || x > upper) return F.OutOfDomain();

        var scaled = (x - lower) / (upper - lower);
        if (cumulative) return F.Number(SpreadsheetMath.RegularizedIncompleteBeta(alpha, beta, scaled));
        var logDensity = (alpha - 1) * M.Log(scaled) + (beta - 1) * M.Log(1 - scaled)
                       + SpreadsheetMath.LogGamma(alpha + beta) - SpreadsheetMath.LogGamma(alpha) - SpreadsheetMath.LogGamma(beta);
        return F.Number(M.Exp(logDensity) / (upper - lower));
    }

    private static double GammaDensity(double x, double shape, double scale)
    {
        if (x == 0) return shape < 1 ? double.PositiveInfinity : shape == 1 ? 1 / scale : 0;
        return M.Exp((shape - 1) * M.Log(x) - x / scale - shape * M.Log(scale) - SpreadsheetMath.LogGamma(shape));
    }

    private static void RegisterRegression(FunctionBuilder builder) =>
        builder.Numeric1("SQRTPI", x => x < 0 ? F.OutOfDomain() : F.Number(M.Sqrt(x * M.PI)));

    private static double StudentDensity(double t, double degrees) =>
        M.Exp(SpreadsheetMath.LogGamma((degrees + 1) / 2) - SpreadsheetMath.LogGamma(degrees / 2))
        / M.Sqrt(degrees * M.PI) * M.Pow(1 + t * t / degrees, -(degrees + 1) / 2);

    private static double StudentRightTail(double t, double degrees) =>
    0.5 * SpreadsheetMath.RegularizedIncompleteBeta(degrees / 2, 0.5, degrees / (degrees + t * t));

    private static double FisherCdf(double x, double d1, double d2) =>
        SpreadsheetMath.RegularizedIncompleteBeta(d1 / 2, d2 / 2, d1 * x / (d1 * x + d2));

    private static double FisherDensity(double x, double d1, double d2)
    {
        var numerator = M.Pow(d1 * x, d1) * M.Pow(d2, d2) / M.Pow(d1 * x + d2, d1 + d2);
        return M.Sqrt(numerator) / (x * SpreadsheetMath.Beta(d1 / 2, d2 / 2));
    }
}

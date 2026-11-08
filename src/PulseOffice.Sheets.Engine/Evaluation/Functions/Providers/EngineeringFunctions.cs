using System.Numerics;
using PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;
using PulseOffice.Sheets.Values;
using M = System.Math;
using F = PulseOffice.Sheets.Engine.Evaluation.Functions.FunctionBuilder;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class EngineeringFunctions : IFunctionProvider
{
    public string Category => "Ingegneria";

    private const int MaxPlaces = 10;

    public IEnumerable<FunctionDefinition> GetFunctions()
    {
        var builder = new FunctionBuilder(Category);

        RegisterBaseConversions(builder);
        RegisterComplexArithmetic(builder);
        RegisterComplexFunctions(builder);
        RegisterBessel(builder);

        builder
            .Numeric2Optional("DELTA", 0, (a, b) => CellValue.Number(a == b ? 1 : 0))
            .Numeric2Optional("GESTEP", 0, (value, step) => CellValue.Number(value >= step ? 1 : 0))
            .Aggregate("MULTINOMIAL", numbers =>
            {
                var terms = numbers.Select(System.Math.Truncate).ToList();
                if (terms.Any(n => n < 0)) return F.OutOfDomain();
                var result = 1.0;
                var total = 0.0;
                foreach (var n in terms)
                {
                    total += n;
                    result *= SpreadsheetMath.Combinations(total, n);
                }
                return F.Number(result);
            })
            .Raw("SERIESSUM", 4, 4, r =>
            {
                var x = r.Number(0);
                var start = r.Number(1);
                var step = r.Number(2);
                if (r.HasError) return r.Error;

                var total = 0.0;
                var index = 0;
                foreach (var value in r.Values(3))
                {
                    if (value.IsError) return value;
                    if (value.Kind != ValueKind.Number) continue;
                    total += value.AsNumber() * M.Pow(x, start + index * step);
                    index++;
                }
                return F.Number(total);
            });

        return builder.Build();
    }

    private static void RegisterBaseConversions(FunctionBuilder builder)
    {
        (string Name, int From, int To)[] conversions =
        [
            ("BIN2DEC", 2, 10), ("BIN2OCT", 2, 8), ("BIN2HEX", 2, 16),
            ("OCT2DEC", 8, 10), ("OCT2BIN", 8, 2), ("OCT2HEX", 8, 16),
            ("DEC2BIN", 10, 2), ("DEC2OCT", 10, 8), ("DEC2HEX", 10, 16),
            ("HEX2DEC", 16, 10), ("HEX2BIN", 16, 2), ("HEX2OCT", 16, 8),
        ];

        foreach (var (name, from, to) in conversions)
            builder.Raw(name, 1, to == 10 ? 1 : 2, r =>
            {
                var text = from == 10 ? FormatDecimalInput(r) : r.Text(0).Trim();
                var places = r.Has(1) ? r.Integer(1) : (int?)null;
                if (r.HasError) return r.Error;

                var value = ParseSigned(text, from);
                if (value is null) return F.OutOfDomain();

                if (to == 10) return CellValue.Number(value.Value);
                return FormatSigned(value.Value, to, places);
            });
    }

    private static string FormatDecimalInput(ArgumentReader r) =>
    ((long)M.Truncate(r.Number(0))).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static long? ParseSigned(string text, int radix)
    {
        if (text.Length == 0) return 0;

        if (radix == 10)
            return long.TryParse(text, out var decimalValue)
                   && decimalValue is >= -549_755_813_888 and <= 549_755_813_887
                ? decimalValue : null;

        if (text.Length > MaxPlaces) return null;

        var magnitude = RadixConverter.FromBase(text, radix);
        if (magnitude is null) return null;

        var limit = (long)M.Pow(radix, MaxPlaces);
        var value = (long)magnitude.Value;

        return value >= limit / 2 ? value - limit : value;
    }

    private static CellValue FormatSigned(long value, int radix, int? places)
    {
        var limit = (long)M.Pow(radix, MaxPlaces);
        var half = limit / 2;

        if (value >= half || value < -half) return F.OutOfDomain();

        if (value < 0)

            return CellValue.Text(RadixConverter.ToBase(value + limit, radix));

        var text = RadixConverter.ToBase(value, radix);
        if (places is null) return CellValue.Text(text);
        if (places < text.Length || places > MaxPlaces) return F.OutOfDomain();

        return CellValue.Text(text.PadLeft(places.Value, '0'));
    }

    private static void RegisterComplexArithmetic(FunctionBuilder builder)
    {
        builder.Raw("COMPLEX", 2, 3, r =>
        {
            var real = r.Number(0);
            var imaginary = r.Number(1);
            var suffix = r.Text(2, "i");
            if (r.HasError) return r.Error;
            if (suffix is not ("i" or "j")) return F.Invalid();
            return CellValue.Text(ComplexNotation.Format(new Complex(real, imaginary), suffix));
        });

        builder.Raw("IMSUM", 1, int.MaxValue, r => Accumulate(r, (a, b) => a + b, Complex.Zero));
        builder.Raw("IMPRODUCT", 1, int.MaxValue, r => Accumulate(r, (a, b) => a * b, Complex.One));

        builder.Raw("IMSUB", 2, 2, r => Binary(r, (a, b) => a - b));
        builder.Raw("IMDIV", 2, 2, r => Binary(r, (a, b) => b == Complex.Zero ? Complex.NaN : a / b));
        builder.Raw("IMPOWER", 2, 2, r =>
        {
            var value = ParseArgument(r, 0);
            var exponent = r.Number(1);
            if (r.HasError || value is null) return r.HasError ? r.Error : F.Invalid();
            if (value.Value.Value == Complex.Zero && exponent <= 0) return F.OutOfDomain();
            return Shown(Complex.Pow(value.Value.Value, exponent), value.Value.Suffix);
        });
    }

    private static void RegisterComplexFunctions(FunctionBuilder builder)
    {
        (string Name, Func<Complex, double> Body)[] scalars =
        [
            ("IMREAL", z => z.Real),
            ("IMAGINARY", z => z.Imaginary),
            ("IMABS", Complex.Abs),
            ("IMARGUMENT", z => z == Complex.Zero ? double.NaN : z.Phase),
        ];

        foreach (var (name, body) in scalars)
            builder.Raw(name, 1, 1, r =>
            {
                var value = ParseArgument(r, 0);
                if (r.HasError) return r.Error;
                return value is null ? F.OutOfDomain() : F.Number(body(value.Value.Value));
            });

        (string Name, Func<Complex, Complex> Body)[] transforms =
        [
            ("IMCONJUGATE", Complex.Conjugate),
            ("IMEXP", Complex.Exp),
            ("IMLN", Complex.Log),
            ("IMLOG10", z => new Complex(M.Log10(Complex.Abs(z)), z.Phase / M.Log(10))),
            ("IMLOG2", z => new Complex(M.Log2(Complex.Abs(z)), z.Phase / M.Log(2))),
            ("IMSQRT", Complex.Sqrt),
            ("IMSIN", Complex.Sin),
            ("IMCOS", Complex.Cos),
            ("IMTAN", Complex.Tan),
            ("IMSINH", Complex.Sinh),
            ("IMCOSH", Complex.Cosh),
            ("IMCOT", z => Complex.Cos(z) / Complex.Sin(z)),
            ("IMSEC", z => Complex.One / Complex.Cos(z)),
            ("IMCSC", z => Complex.One / Complex.Sin(z)),
            ("IMSECH", z => Complex.One / Complex.Cosh(z)),
            ("IMCSCH", z => Complex.One / Complex.Sinh(z)),
        ];

        foreach (var (name, body) in transforms)
            builder.Raw(name, 1, 1, r =>
            {
                var value = ParseArgument(r, 0);
                if (r.HasError) return r.Error;
                if (value is null) return F.OutOfDomain();
                var result = body(value.Value.Value);
                return Shown(result, value.Value.Suffix);
            });
    }

    private static void RegisterBessel(FunctionBuilder builder)
    {
        (string Name, Func<double, int, double> Body)[] bessel =
        [
            ("BESSELJ", BesselFunctions.J),
            ("BESSELI", BesselFunctions.I),
            ("BESSELY", BesselFunctions.Y),
            ("BESSELK", BesselFunctions.K),
        ];

        foreach (var (name, body) in bessel)
            builder.Numeric2(name, (x, order) =>
            {
                if (order < 0) return F.OutOfDomain();
                return F.Number(body(x, (int)M.Floor(order)));
            });
    }

    private static ComplexText? ParseArgument(ArgumentReader r, int index)
    {
        var text = r.Text(index);
        return r.HasError ? null : ComplexNotation.Parse(text);
    }

    private static CellValue Accumulate(ArgumentReader r, Func<Complex, Complex, Complex> combine, Complex seed)
    {
        var result = seed;
        char? suffix = null;
        for (var i = 0; i < r.Count; i++)
            foreach (var item in r.Values(i))
            {
                if (item.IsError) return item;
                if (item.IsEmpty) continue;
                if (ComplexNotation.Parse(r.TextOf(item)) is not { } value) return F.OutOfDomain();
                if (!Agree(ref suffix, value.Suffix)) return F.Invalid();
                result = combine(result, value.Value);
            }
        return Shown(result, suffix);
    }

    private static bool Agree(ref char? suffix, char? other)
    {
        if (other is null) return true;
        if (suffix is not null && suffix != other) return false;
        suffix = other;
        return true;
    }

    private static CellValue Shown(Complex value, char? suffix) =>
        double.IsFinite(value.Real) && double.IsFinite(value.Imaginary)
            ? CellValue.Text(ComplexNotation.Format(value, suffix == 'j' ? "j" : "i"))
            : F.OutOfDomain();

    private static CellValue Binary(ArgumentReader r, Func<Complex, Complex, Complex> combine)
    {
        var left = ParseArgument(r, 0);
        var right = ParseArgument(r, 1);
        if (r.HasError) return r.Error;
        if (left is null || right is null) return F.OutOfDomain();

        char? suffix = null;
        if (!Agree(ref suffix, left.Value.Suffix) || !Agree(ref suffix, right.Value.Suffix)) return F.Invalid();
        return Shown(combine(left.Value.Value, right.Value.Value), suffix);
    }
}

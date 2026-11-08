using System.Globalization;
using PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;
using PulseOffice.Sheets.Values;
using M = System.Math;
using F = PulseOffice.Sheets.Engine.Evaluation.Functions.FunctionBuilder;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class CompatibilityFunctions(IRangeMaterializer ranges, IRandomSource random, IFormatCodeRenderer formats, NumericText numbers, ILocalizedFormatTokens localizedFormats, IUnitConversion? units = null) : IFunctionProvider
{
    private readonly IUnitConversion _units = units ?? new UnitConversion();

    private readonly ByteUnits _bytes = new();

    public string Category => "Compatibilità";

    public IEnumerable<FunctionDefinition> GetFunctions()
    {
        var builder = new FunctionBuilder(Category);

        RegisterQuantiles(builder);
        RegisterByteVariants(builder);
        RegisterTextHelpers(builder);
        RegisterMiscellany(builder);

        return builder.Build();
    }

    private void RegisterQuantiles(FunctionBuilder builder)
    {
        builder.Raw("PERCENTILE.INC", 2, 2, r => Quantile(r, exclusive: false, quarters: false));
        builder.Raw("PERCENTILE.EXC", 2, 2, r => Quantile(r, exclusive: true, quarters: false));
        builder.Raw("QUARTILE.INC", 2, 2, r => Quantile(r, exclusive: false, quarters: true));
        builder.Raw("QUARTILE.EXC", 2, 2, r => Quantile(r, exclusive: true, quarters: true));

        foreach (var (name, exclusive) in ((string, bool)[])[("PERCENTRANK.INC", false), ("PERCENTRANK.EXC", true)])
            builder.Raw(name, 2, 3, r => RegressionFunctions.PercentRank(r, Series(r, 0), exclusive));
    }

    private void RegisterByteVariants(FunctionBuilder builder)
    {
        (string Name, int Minimum, int Maximum)[] variants =
        [
            ("LENB", 1, 1), ("LEFTB", 1, 2), ("RIGHTB", 1, 2), ("MIDB", 3, 3),
            ("FINDB", 2, 3), ("SEARCHB", 2, 3), ("REPLACEB", 4, 4),
        ];

        foreach (var (name, minimum, maximum) in variants)
            builder.Raw(name, minimum, maximum, r =>
            {
                var text = r.Text(0);
                if (r.HasError) return r.Error;

                var result = name switch
                {
                    "LENB" => CellValue.Number(_bytes.Length(text)),
                    "LEFTB" => Counted(r.Integer(1, 1), count => CellValue.Text(_bytes.Left(text, count))),
                    "RIGHTB" => Counted(r.Integer(1, 1), count => CellValue.Text(_bytes.Right(text, count))),
                    "MIDB" => r.Integer(1) < 1 ? F.Invalid() : Counted(r.Integer(2), count => CellValue.Text(_bytes.Middle(text, r.Integer(1), count))),
                    "FINDB" => Found(_bytes.Find(r.Text(1), r.Integer(2, 1), (haystack, from) => haystack.IndexOf(text, from, StringComparison.Ordinal))),
                    "SEARCHB" => Found(_bytes.Find(r.Text(1), r.Integer(2, 1), (haystack, from) => haystack.IndexOf(text, from, StringComparison.OrdinalIgnoreCase))),
                    _ => r.Integer(1) < 1 ? F.Invalid() : Counted(r.Integer(2), count => CellValue.Text(_bytes.Replace(text, r.Integer(1), count, r.Text(3)))),
                };
                return r.HasError ? r.Error : result;
            });

        static CellValue Counted(int count, Func<int, CellValue> body) => count < 0 ? F.Invalid() : body(count);

        static CellValue Found(int? position) => position is { } found ? CellValue.Number(found) : F.Invalid();
    }

    private void RegisterTextHelpers(FunctionBuilder builder)
    {
        builder.Raw("TEXT", 2, 2, r =>
        {
            var value = r.Value(0);
            var format = localizedFormats.ToCanonical(r.Text(1));
            if (r.HasError) return r.Error;

            if (value.Kind == ValueKind.Text && numbers.TryRead(value.AsText(), out var parsed))
                value = CellValue.Number(parsed);
            if (value.Kind == ValueKind.Number && format.Trim().Equals("General", StringComparison.OrdinalIgnoreCase))
                return CellValue.Text(r.TextOf(value));
            return CellValue.Text(formats.Render(value.IsEmpty ? CellValue.Text(string.Empty) : value, format));
        });

        builder.Raw("TEXTBEFORE", 2, 6, r => Split(r, before: true));
        builder.Raw("TEXTAFTER", 2, 6, r => Split(r, before: false));

        builder.RawHandlingErrors("SWITCH", 3, int.MaxValue, r =>
        {
            var subject = r.Value(0);
            if (subject.IsError) return subject;

            for (var i = 1; i + 1 < r.Count; i += 2)
                if (AreEqual(subject, r.Value(i), r.Context.TextRules)) return r.Value(i + 1);

            return r.Count % 2 == 0 ? r.Value(r.Count - 1) : CellValue.FromError(CellError.NotAvailable);
        });

        static CellValue Split(ArgumentReader r, bool before)
        {
            var text = r.Text(0);
            var separators = r.Values(1).Where(value => !value.IsEmpty).Select(r.TextOf).Where(separator => separator.Length > 0).ToList();
            var occurrence = r.Integer(2, 1);
            var ignoreCase = r.Integer(3, 0) == 1;
            var matchEnd = r.Integer(4, 0) == 1;
            if (r.HasError) return r.Error;
            if (separators.Count == 0 || occurrence == 0 || M.Abs(occurrence) > text.Length) return F.Invalid();

            var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var matches = new List<(int Index, int Length)>();
            for (var index = 0; index < text.Length;)
            {
                var found = separators.Select(separator => (Index: text.IndexOf(separator, index, comparison), separator.Length))
                    .Where(candidate => candidate.Index >= 0).OrderBy(candidate => candidate.Index).ThenByDescending(candidate => candidate.Length)
                    .FirstOrDefault((-1, 0));
                if (found.Item1 < 0) break;
                matches.Add((found.Item1, found.Item2));
                index = found.Item1 + M.Max(found.Item2, 1);
            }
            if (matchEnd)
            {
                if (occurrence > 0) matches.Add((text.Length, 0));
                else matches.Insert(0, (0, 0));
            }

            var position = occurrence > 0 ? occurrence - 1 : matches.Count + occurrence;
            if (position < 0 || position >= matches.Count)
                return r.Has(5) ? r.Value(5) : CellValue.FromError(CellError.NotAvailable);

            var (start, length) = matches[position];
            return CellValue.Text(before ? text[..start] : text[(start + length)..]);
        }

        static bool AreEqual(CellValue a, CellValue b, TextRules rules)
        {
            if (a.IsError || b.IsError) return false;
            if (a.IsNumeric && b.IsNumeric) return a.AsNumber() == b.AsNumber();
            return string.Equals(a.ToString(), b.ToString(), rules.Comparison);
        }
    }

    private void RegisterMiscellany(FunctionBuilder builder)
    {
        builder
            .Raw("CONVERT", 3, 3, r =>
            {
                var value = r.Number(0);
                var from = r.Text(1);
                var to = r.Text(2);
                if (r.HasError) return r.Error;

                return _units.Convert(value, from, to) is { } converted
                    ? F.Number(converted) : CellValue.FromError(CellError.NotAvailable);
            })

            .Numeric1("NEG", x => F.Number(-x))
            .Aggregate("RAWSUBTRACT", values => F.Number(values.Count == 0 ? 0 : values.Skip(1).Aggregate(values[0], (difference, value) => difference - value)), minimumArguments: 2)

            .Numeric2("ROUNDSIG", (value, digits) =>
            {
                if (digits < 1) return F.OutOfDomain();
                if (value == 0) return CellValue.Number(0);

                var magnitude = (int)M.Floor(M.Log10(M.Abs(value))) + 1;
                var decimals = (int)digits - magnitude;
                var factor = M.Pow(10, decimals);
                return F.Number(M.Round(value * factor, MidpointRounding.AwayFromZero) / factor);
            })

            .Aggregate("SKEWP", values =>
            {
                if (values.Count < 3) return F.DivideByZero();

                var mean = SpreadsheetMath.Mean(values);
                var deviation = SpreadsheetMath.StandardDeviation(values, population: true);
                if (deviation == 0) return F.DivideByZero();

                return F.Number(values.Sum(v => M.Pow((v - mean) / deviation, 3)) / values.Count);
            })

            .Raw("NEGBINOM.DIST", 4, 4, r =>
            {
                var failures = M.Floor(r.Number(0));
                var successes = M.Floor(r.Number(1));
                var probability = r.Number(2);
                var cumulative = r.Boolean(3, true);
                if (r.HasError) return r.Error;
                if (failures < 0 || successes < 1 || probability is <= 0 or > 1) return F.OutOfDomain();

                return F.Number(cumulative
                    ? SpreadsheetMath.RegularizedIncompleteBeta(successes, failures + 1, probability)
                    : NegativeBinomialDistribution.Mass(failures, successes, probability));
            })

            .Raw("TYPE", 1, 1, r => r.Raw(0) is ArrayValue || r.Raw(0) is RangeValue { Range.IsSingleCell: false }
                ? CellValue.Number(64)
                : r.Value(0).Kind switch
            {
                ValueKind.Number or ValueKind.Empty => CellValue.Number(1),
                ValueKind.Text => CellValue.Number(2),
                ValueKind.Boolean => CellValue.Number(4),
                ValueKind.Error => CellValue.Number(16),
                _ => CellValue.Number(1),
            }, handlesErrors: true)

            .Constant("RAND.NV", () => CellValue.Number(random.NextDouble()))
            .Numeric2("RANDBETWEEN.NV", (low, high) => M.Ceiling(low) > M.Ceiling(high)
                ? F.OutOfDomain() : CellValue.Number(random.NextInt64((long)M.Ceiling(low), (long)M.Ceiling(high))))

            .Raw("MVALUE", 1, 1, r =>
            {
                var value = r.Value(0);
                return value.IsNumeric ? CellValue.Number(value.AsNumber()) : F.Invalid();
            });
    }

    private CellValue Quantile(ArgumentReader r, bool exclusive, bool quarters)
    {
        var values = Series(r, 0);
        var argument = r.Number(1);
        if (r.HasError) return r.Error;
        if (values.Count == 0) return F.OutOfDomain();

        var fraction = quarters ? M.Floor(argument) / 4 : argument;
        if (fraction is < 0 or > 1) return F.OutOfDomain();

        if (!exclusive) return F.Number(SpreadsheetMath.Quantile(values, fraction));

        var n = values.Count;
        if (fraction < 1.0 / (n + 1) || fraction > n / (double)(n + 1)) return F.OutOfDomain();

        values.Sort();
        var position = M.Clamp(fraction * (n + 1) - 1, 0, n - 1);
        var lower = (int)M.Floor(position);
        var upper = (int)M.Ceiling(position);

        return F.Number(lower == upper
            ? values[lower]
            : values[lower] + (position - lower) * (values[upper] - values[lower]));
    }

    private List<double> Series(ArgumentReader r, int index)
    {
        var values = new List<double>();
        foreach (var value in ranges.Materialize(r.Raw(index), r.Context).InReadingOrder())
        {
            if (value.IsError) { r.Fail(value.Error); return values; }
            if (value.Kind == ValueKind.Number) values.Add(value.AsNumber());
        }
        return values;
    }
}

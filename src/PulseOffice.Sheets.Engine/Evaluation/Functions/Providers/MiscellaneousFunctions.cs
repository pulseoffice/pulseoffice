using System.Globalization;
using PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;
using PulseOffice.Sheets.Values;
using M = System.Math;
using F = PulseOffice.Sheets.Engine.Evaluation.Functions.FunctionBuilder;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class MiscellaneousFunctions(IDateSystem dates, IRangeMaterializer ranges, NumericText numbers) : IFunctionProvider
{
    private const int MaximumDollarDecimals = 127;
    private const int MaximumRoundingDecimals = 15;

    private readonly JapaneseWidth _width = new();

    public string Category => "Varie";

    public IEnumerable<FunctionDefinition> GetFunctions() => new FunctionBuilder(Category)
        .Raw("DOLLAR", 1, 2, r =>
        {
            var value = r.Number(0);
            var decimals = r.Integer(1, 2);
            if (r.HasError) return r.Error;
            if (decimals > MaximumDollarDecimals) return F.Invalid();

            var scale = M.Pow(10, -(double)decimals);
            var rounded = decimals >= 0
                ? M.Round(value, M.Min(decimals, MaximumRoundingDecimals), MidpointRounding.AwayFromZero)
                : double.IsFinite(scale) ? M.Round(value / scale, MidpointRounding.AwayFromZero) * scale : 0;

            return CellValue.Text(rounded.ToString(
                "C" + M.Max(decimals, 0).ToString(CultureInfo.InvariantCulture), numbers.Culture));
        })

        .Raw("NUMBERVALUE", 1, 3, r =>
        {
            var text = r.Text(0);
            var decimalText = r.Text(1, numbers.Culture.NumberFormat.NumberDecimalSeparator);
            var groupText = r.Text(2, numbers.Culture.NumberFormat.NumberGroupSeparator);
            if (r.HasError) return r.Error;
            if (decimalText.Length == 0 || (r.Has(2) && groupText.Length == 0)) return F.Invalid();

            var decimalSeparator = decimalText[0];
            char? groupSeparator = groupText.Length == 0 ? null : groupText[0];
            if (groupSeparator == decimalSeparator)
            {
                if (r.Has(2)) return F.Invalid();
                groupSeparator = decimalSeparator == ',' ? '.' : ',';
            }

            return ReadNumberValue(text, decimalSeparator, groupSeparator) is { } value ? F.Number(value) : F.Invalid();
        })

        .Text1("ENCODEURL", text => CellValue.Text(Uri.EscapeDataString(text)))

        .Text1("ROT13", text => CellValue.Text(new string(text.Select(Rot13).ToArray())))

        .Raw("ARRAYTOTEXT", 1, 2, r =>
        {
            var grid = ranges.Materialize(r.Raw(0), r.Context);
            var strict = r.Integer(1, 0);
            if (r.HasError) return r.Error;
            if (strict is not (0 or 1)) return F.Invalid();
            return CellValue.Text(strict == 1 ? StrictArrayText(grid, r) : ConciseArrayText(grid, r));
        })

        .Text1("ASC", text => CellValue.Text(_width.ToHalfWidth(text)))
        .Text1("JIS", text => CellValue.Text(_width.ToFullWidth(text)))

        .Numeric1("EASTERSUNDAY", year =>
        {
            var y = (int)M.Floor(year);
            if (y is < 1583 or > 9956) return F.OutOfDomain();
            return F.Number(M.Floor(dates.ToSerial(EasterSunday(y))));
        })

        .Raw("WEEKS", 2, 3, r => Difference(r, (start, end) => M.Floor((end - start).TotalDays / 7)))
        .Raw("MONTHS", 2, 3, r => Difference(r, (start, end) =>
            (end.Year - start.Year) * 12 + end.Month - start.Month - (end.Day < start.Day ? 1 : 0)))
        .Raw("YEARS", 2, 3, r => Difference(r, (start, end) =>
        {
            var years = end.Year - start.Year;
            if (end.Month < start.Month || (end.Month == start.Month && end.Day < start.Day)) years--;
            return years;
        }))

        .Raw("AREAS", 1, 1, r => r.Raw(0) switch
        {
            ReferenceListValue list => CellValue.Number(list.Areas.Count),
            RangeValue => CellValue.Number(1),
            _ => CellValue.FromError(CellError.Value),
        })

        .Raw("PROB", 3, 4, r =>
        {
            var values = Series(r, 0);
            var probabilities = Series(r, 1);
            var lower = r.Number(2);
            var upper = r.Number(3, lower);
            if (r.HasError) return r.Error;
            if (values.Count != probabilities.Count || values.Count == 0) return CellValue.FromError(CellError.NotAvailable);

            var total = probabilities.Sum();

            if (M.Abs(total - 1) > 1e-9) return F.OutOfDomain();

            var selected = 0.0;
            for (var i = 0; i < values.Count; i++)
                if (values[i] >= lower && values[i] <= upper) selected += probabilities[i];

            return F.Number(selected);
        })

        .Build();

    private CellValue Difference(ArgumentReader r, Func<DateTime, DateTime, double> compute)
    {
        var start = dates.FromSerial(r.Number(0));
        var end = dates.FromSerial(r.Number(1));
        return r.HasError ? r.Error : F.Number(compute(start, end));
    }

    private List<double> Series(ArgumentReader r, int index)
    {
        var grid = ranges.Materialize(r.Raw(index), r.Context);
        var values = new List<double>();

        foreach (var value in grid.InReadingOrder())
        {
            if (value.IsError) { r.Fail(value.Error); return values; }
            if (value.Kind == ValueKind.Number) values.Add(value.AsNumber());
        }
        return values;
    }

    private static double? ReadNumberValue(string text, char decimalSeparator, char? groupSeparator)
    {
        var builder = new System.Text.StringBuilder(text.Length);
        var seenDecimal = false;
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character)) continue;
            if (character == decimalSeparator)
            {
                if (seenDecimal) return null;
                seenDecimal = true;
                builder.Append('.');
            }
            else if (character == groupSeparator)
            {
                if (seenDecimal) return null;
            }
            else builder.Append(character);
        }

        var normalized = builder.ToString();
        var percents = normalized.Length - normalized.TrimEnd('%').Length;
        normalized = normalized[..^percents];
        if (normalized.Length == 0) return percents == 0 ? 0 : null;

        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value)
            ? value / M.Pow(100, percents)
            : null;
    }

    private static char Rot13(char c) => c switch
    {
        >= 'a' and <= 'z' => (char)('a' + (c - 'a' + 13) % 26),
        >= 'A' and <= 'Z' => (char)('A' + (c - 'A' + 13) % 26),
        _ => c,
    };

    private static DateTime EasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;

        var month = (h + l - 7 * m + 114) / 31;
        var day = (h + l - 7 * m + 114) % 31 + 1;

        return new DateTime(year, month, day);
    }

    private static string ConciseArrayText(ValueGrid grid, ArgumentReader reader) =>
        string.Join(", ", Enumerable.Range(0, grid.RowCount)
            .SelectMany(row => Enumerable.Range(0, grid.ColumnCount).Select(column => ArrayItemText(grid[row, column], reader, quoted: false))));

    private static string StrictArrayText(ValueGrid grid, ArgumentReader reader) =>
        "{" + string.Join(";", Enumerable.Range(0, grid.RowCount)
            .Select(row => string.Join(",", Enumerable.Range(0, grid.ColumnCount).Select(column => ArrayItemText(grid[row, column], reader, quoted: true))))) + "}";

    private static string ArrayItemText(CellValue value, ArgumentReader reader, bool quoted) => value.Kind switch
    {
        ValueKind.Error => value.ErrorText,
        ValueKind.Boolean => value.AsBoolean() ? "TRUE" : "FALSE",
        ValueKind.Text when quoted => "\"" + value.AsText().Replace("\"", "\"\"", StringComparison.Ordinal) + "\"",
        ValueKind.Empty => string.Empty,
        _ => reader.TextOf(value),
    };
}

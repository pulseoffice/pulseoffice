using PulseOffice.Sheets.Engine.Evaluation.Functions.Numerics;
using PulseOffice.Sheets.Values;
using M = System.Math;
using F = PulseOffice.Sheets.Engine.Evaluation.Functions.FunctionBuilder;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class NumberSystemFunctions : IFunctionProvider
{
    public string Category => "Matematica";

    private const long BitLimit = (1L << 48) - 1;

    private const int MaximumLength = ushort.MaxValue;

    private const double ExactIntegerLimit = 9007199254740992d;

    private const int MaximumShift = 53;

    public IEnumerable<FunctionDefinition> GetFunctions() => new FunctionBuilder(Category)
        .Raw("BASE", 2, 3, r =>
        {
            var value = r.Number(0);
            var radix = r.Integer(1);
            var minimumLength = r.Integer(2, 0);
            if (r.HasError) return r.Error;
            if (radix is < 2 or > 36 || value is < 0 or >= ExactIntegerLimit || minimumLength is < 0 or >= MaximumLength) return F.OutOfDomain();

            return CellValue.Text(RadixConverter.ToBase((long)value, radix).PadLeft(minimumLength, '0'));
        })

        .Raw("DECIMAL", 2, 2, r =>
        {
            var text = r.Text(0).Trim();
            var radix = r.Integer(1);
            if (r.HasError) return r.Error;
            if (radix is < 2 or > 36) return F.OutOfDomain();

            return RadixConverter.FromBase(text, radix) switch
            {
                null => F.OutOfDomain(),
                >= ExactIntegerLimit => F.OutOfDomain(),
                var value => CellValue.Number(value.Value),
            };
        })

        .Raw("ROMAN", 1, 2, r =>
        {
            var value = (int)M.Floor(r.Number(0));
            var mode = (int)M.Floor(r.Number(1, 0));
            if (r.HasError) return r.Error;
            return value is < 0 or > 3999 || mode is < 0 or > 4
                ? F.Invalid()
                : CellValue.Text(RomanNumerals.Format(value, mode));
        })

        .Text1("ARABIC", text => RomanNumerals.Parse(text.Trim().ToUpperInvariant()) is { } value
            ? CellValue.Number(value)
            : F.Invalid())

        .Numeric2("BITAND", (a, b) => Bitwise(a, b, (x, y) => x & y))
        .Numeric2("BITOR", (a, b) => Bitwise(a, b, (x, y) => x | y))
        .Numeric2("BITXOR", (a, b) => Bitwise(a, b, (x, y) => x ^ y))
        .Numeric2("BITLSHIFT", (value, shift) => Shift(value, shift))
        .Numeric2("BITRSHIFT", (value, shift) => Shift(value, -shift))

        .Build();

    private static CellValue Bitwise(double a, double b, Func<long, long, long> operation) =>
        !IsBitOperand(a) || !IsBitOperand(b)
            ? F.OutOfDomain()
            : CellValue.Number(operation((long)a, (long)b));

    private static CellValue Shift(double value, double shift)
    {
        if (!IsBitOperand(value) || M.Abs(shift) > MaximumShift) return F.OutOfDomain();

        var amount = (int)M.Truncate(shift);
        var result = amount >= 0 ? value * M.Pow(2, amount) : M.Floor(value / M.Pow(2, -amount));

        return result > BitLimit ? F.OutOfDomain() : CellValue.Number(result);
    }

    private static bool IsBitOperand(double value) => value is >= 0 and <= BitLimit && value == M.Floor(value);
}

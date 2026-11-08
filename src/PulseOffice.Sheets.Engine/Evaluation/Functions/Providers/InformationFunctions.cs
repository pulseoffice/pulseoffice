using PulseOffice.Sheets.Values;
using M = System.Math;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions.Providers;

public sealed class InformationFunctions : IFunctionProvider
{
    public string Category => "Informazione";

    public IEnumerable<FunctionDefinition> GetFunctions()
    {
        var builder = new FunctionBuilder(Category)
            .Constant("NA", () => CellValue.FromError(CellError.NotAvailable))
            .Numeric1("ISEVEN", x => CellValue.Boolean((long)M.Truncate(x) % 2 == 0))
            .Numeric1("ISODD", x => CellValue.Boolean(M.Abs((long)M.Truncate(x) % 2) == 1));

        var predicates = new Dictionary<string, Func<CellValue, bool>>(StringComparer.Ordinal)
        {
            ["ISBLANK"] = v => v.IsEmpty,
            ["ISERROR"] = v => v.IsError,
            ["ISERR"] = v => v.IsError && v.Error != CellError.NotAvailable,
            ["ISNA"] = v => v.IsError && v.Error == CellError.NotAvailable,
            ["ISNUMBER"] = v => v.Kind == ValueKind.Number,
            ["ISTEXT"] = v => v.Kind == ValueKind.Text,
            ["ISNONTEXT"] = v => v.Kind != ValueKind.Text,
            ["ISLOGICAL"] = v => v.Kind == ValueKind.Boolean,
        };

        foreach (var (name, predicate) in predicates)
            builder.RawHandlingErrors(name, 1, 1, r => CellValue.Boolean(predicate(r.Value(0))));

        builder.RawHandlingErrors("ERROR.TYPE", 1, 1, r => ErrorCode(r.Value(0)));
        builder.RawHandlingErrors("ERRORTYPE", 1, 1, r => ErrorTypeCode(r.Value(0)));

        builder.RawHandlingErrors("N", 1, 1, r =>
        {
            var value = r.Value(0);
            if (value.IsError) return value;
            return value.IsNumeric ? CellValue.Number(value.AsNumber()) : CellValue.Number(0);
        });

        return builder.Build();
    }

    private static CellValue ErrorTypeCode(CellValue value) => value.Error switch
    {
        CellError.Null => CellValue.Number(521),
        CellError.DivideByZero => CellValue.Number(532),
        CellError.Value => CellValue.Number(519),
        CellError.Circular => CellValue.Number(522),
        CellError.Reference => CellValue.Number(524),
        CellError.Name => CellValue.Number(525),
        CellError.Number => CellValue.Number(503),
        CellError.NotAvailable => CellValue.Number(32767),
        _ => CellValue.FromError(CellError.NotAvailable),
    };

    private static CellValue ErrorCode(CellValue value) => value.Error switch
    {
        CellError.Null => CellValue.Number(1),
        CellError.DivideByZero => CellValue.Number(2),
        CellError.Value or CellError.Circular => CellValue.Number(3),
        CellError.Reference => CellValue.Number(4),
        CellError.Name => CellValue.Number(5),
        CellError.Number => CellValue.Number(6),
        CellError.NotAvailable => CellValue.Number(7),
        CellError.Spill => CellValue.Number(9),
        CellError.Calc => CellValue.Number(14),
        _ => CellValue.FromError(CellError.NotAvailable),
    };
}

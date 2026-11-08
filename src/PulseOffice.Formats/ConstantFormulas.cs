using PulseOffice.Sheets.Values;

namespace PulseOffice.Formats;

public sealed class ConstantFormulas
{
    public bool IsConstant(CellValue value, string? formula)
    {
        if (formula is null) return false;
        var body = formula.TrimStart('=').Trim();
        return value.Kind switch
        {
            ValueKind.Error => string.Equals(body, value.ErrorText, StringComparison.OrdinalIgnoreCase),
            ValueKind.Boolean => string.Equals(body, value.AsBoolean() ? "TRUE()" : "FALSE()", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }
}

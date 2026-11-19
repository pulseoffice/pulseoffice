using PulseOffice.Sheets.Values;

namespace PulseOffice.Sheets.Pivot;

public static class PivotValues
{
    public static string KeyOf(CellValue value) => value.Kind switch
    {
        ValueKind.Empty => string.Empty,
        ValueKind.Text => value.AsText(),
        ValueKind.Boolean => value.AsBoolean() ? TrueKey : FalseKey,
        ValueKind.Error => value.ErrorText,
        _ => value.AsNumber().ToString("R", System.Globalization.CultureInfo.InvariantCulture),
    };

    public const string TrueKey = "TRUE";

    public const string FalseKey = "FALSE";

    public static bool? BooleanOf(string key) => key switch
    {
        TrueKey => true,
        FalseKey => false,
        _ => null,
    };
}
